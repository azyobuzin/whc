use core::nonzero::Zeroable;
use std::collections::HashMap;
use std::ffi;
use std::fmt;
use std::io;
use std::mem;
use std::os::windows::prelude::*;
use std::os::windows::process::ExitStatusExt;
use std::process;
use std::ptr;
use std::slice;
use futures::{Async, Future, Poll};
use futures::sync::oneshot;
use super::gdi32;
use super::kernel32;
use super::user32;
use super::winapi::*;

/// `GetLastError` はどう転んでも安全なので unsafe 外し
pub fn get_last_error() -> DWORD {
    unsafe { kernel32::GetLastError() }
}

fn err_if_zero<T: Zeroable>(v: T) -> io::Result<T> {
    if v.is_zero() {
        Err(io::Error::last_os_error())
    } else {
        Ok(v)
    }
}

fn err_if_zero2<T: Zeroable>(v: T, func_name: &str) -> io::Result<T> {
    if v.is_zero() {
        Err(io::Error::new(io::ErrorKind::Other, func_name))
    } else {
        Ok(v)
    }
}

pub fn find_main_window(process_id: u32) -> io::Result<HWND> {
    struct LParamData {
        process_id: u32,
        main_window_handle: HWND,
    }

    extern "system" fn enum_proc(hwnd: HWND, lparam: LPARAM) -> BOOL {
        let lparam = unsafe { &mut *(lparam as *mut LParamData) };

        let wnd_pid = unsafe {
            let mut rpid: DWORD = 0;
            user32::GetWindowThreadProcessId(hwnd, &mut rpid as LPDWORD);
            rpid
        };

        if wnd_pid == lparam.process_id {
            let owner = unsafe {
                user32::GetWindow(hwnd, GW_OWNER)
            };

            if owner.is_null() {
                // オーナーウィンドウがない → 普通のウィンドウ
                lparam.main_window_handle = hwnd;
                return FALSE;
            }
        }

        TRUE
    }

    let mut lparam = LParamData {
        process_id,
        main_window_handle: ptr::null_mut(),
    };

    let enum_result = unsafe {
        user32::EnumWindows(Some(enum_proc), &mut lparam as *mut LParamData as LPARAM)
    };

    if enum_result == FALSE {
        let error_code = get_last_error();
        if error_code != 0 {
            // エラーコード 0 は lpEnumFunc が FALSE を返しただけ
            return Err(io::Error::from_raw_os_error(error_code as i32));
        }
    }

    Ok(lparam.main_window_handle)
}

pub fn wait_process_async(process_id: u32) -> io::Result<ProcessFuture> {
    let process_handle = open_process(process_id, SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION)?;

    let (sender, receiver) = oneshot::channel();
    let tx_ptr = Box::into_raw(Box::new(sender));

    let wait_handle = register_wait(
        process_handle,
        Some(process_wait_callback),
        tx_ptr as PVOID,
        INFINITE
    )?;

    Ok(ProcessFuture { wait_handle, receiver })
}

#[derive(Debug)]
pub struct ProcessFuture {
    wait_handle: WaitHandle,
    receiver: oneshot::Receiver<()>,
}

impl Future for ProcessFuture {
    type Item = process::ExitStatus;
    type Error = io::Error;

    fn poll(&mut self) -> Poll<Self::Item, Self::Error> {
        match self.receiver.poll() {
            Ok(Async::Ready(_)) => {
                let mut exit_code: DWORD = unsafe { mem::uninitialized() };
                err_if_zero(unsafe {
                    kernel32::GetExitCodeProcess(self.wait_handle.process_handle, &mut exit_code as LPDWORD)
                })?;
                Ok(Async::Ready(process::ExitStatus::from_raw(exit_code as u32)))
            }
            Ok(Async::NotReady) => Ok(Async::NotReady),
            Err(_) => unreachable!(), // cancel は呼び出さない
        }
    }
}

#[derive(Debug)]
struct ProcessHandle(HANDLE);

impl Drop for ProcessHandle {
    fn drop(&mut self) {
        unsafe { kernel32::CloseHandle(self.0); }
    }
}

#[derive(Debug)]
struct WaitHandle {
    wait_object: HANDLE,
    process_handle: HANDLE,
}

impl Drop for WaitHandle {
    fn drop(&mut self) {
        unsafe {
            // コールバックが確実に終了してからプロセスハンドルを閉じる
            kernel32::UnregisterWaitEx(self.wait_object, INVALID_HANDLE_VALUE);
            kernel32::CloseHandle(self.process_handle);
        }
    }
}

fn open_process(process_id: u32, desired_access: DWORD) -> io::Result<ProcessHandle> {
    err_if_zero(unsafe {
        kernel32::OpenProcess(desired_access, FALSE, process_id as DWORD)
    }).map(ProcessHandle)
}

fn register_wait(process_handle: ProcessHandle, callback: WAITORTIMERCALLBACK, context: PVOID, timeout_millis: ULONG)
    -> io::Result<WaitHandle>
{
    let mut wait_object: HANDLE = unsafe { mem::uninitialized() };
    err_if_zero(unsafe {
        kernel32::RegisterWaitForSingleObject(
            &mut wait_object as PHANDLE,
            process_handle.0,
            callback,
            context,
            timeout_millis,
            WT_EXECUTEINWAITTHREAD | WT_EXECUTEONLYONCE
        )
    })?;

    let ph = process_handle.0;
    mem::forget(process_handle); // CloseHandle しない
    Ok(WaitHandle { wait_object, process_handle: ph })
}

extern "system" fn process_wait_callback(parameter: PVOID, _timer_or_wait_fired: BOOLEAN) {
    let sender = unsafe { Box::from_raw(parameter as *mut oneshot::Sender<()>) };
    sender.send(()).ok();
}

pub fn get_process_path(process_id: u32) -> io::Result<ffi::OsString> {
    let process_handle = open_process(process_id, PROCESS_QUERY_INFORMATION)?;

    const BUFFER_SIZE: usize = 1024;
    let mut buffer = Vec::<WCHAR>::with_capacity(BUFFER_SIZE);
    unsafe { buffer.set_len(BUFFER_SIZE); }

    let len = err_if_zero(unsafe {
        kernel32::K32GetModuleFileNameExW(
            process_handle.0,
            ptr::null_mut(),
            buffer.as_mut_ptr(),
            BUFFER_SIZE as DWORD
        )
    })?;

    Ok(ffi::OsString::from_wide(&buffer[..(len as usize)]))
}

fn slice_to_zero<T: Zeroable>(s: &[T]) -> &[T] {
    s.iter().position(Zeroable::is_zero)
        .map_or(s, |i| &s[..i])
}

pub struct ProcessInfo {
    entry: PROCESSENTRY32W
}

impl ProcessInfo {
    pub fn process_id(&self) -> u32 {
        self.entry.th32ProcessID as u32
    }

    pub fn exe_file_raw(&self) -> &[WCHAR] {
        slice_to_zero(&self.entry.szExeFile)
    }

    pub fn exe_file(&self) -> ffi::OsString {
        ffi::OsString::from_wide(self.exe_file_raw())
    }
}

impl fmt::Debug for ProcessInfo {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        f.debug_struct("ProcessInfo")
            .field("process_id", &self.process_id())
            .field("exe_file", &self.exe_file())
            .finish()
    }
}

pub struct ProcessIterator {
    snapshot_handle: HANDLE,
    is_first: bool,
}

impl ProcessIterator {
    pub fn new() -> io::Result<Self> {
        let handle = unsafe {
            kernel32::CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0)
        };
        if handle.is_null() || handle == INVALID_HANDLE_VALUE {
            Err(io::Error::last_os_error())
        } else {
            Ok(ProcessIterator {
                snapshot_handle: handle,
                is_first: true,
            })
        }
    }
}

impl Drop for ProcessIterator {
    fn drop(&mut self) {
        unsafe { kernel32::CloseHandle(self.snapshot_handle); }
    }
}

impl Iterator for ProcessIterator {
    type Item = io::Result<ProcessInfo>;

    fn next(&mut self) -> Option<Self::Item> {
        let mut entry =  PROCESSENTRY32W {
            dwSize: mem::size_of::<PROCESSENTRY32W>() as DWORD,
            cntUsage: 0,
            th32ProcessID: 0,
            th32DefaultHeapID: 0,
            th32ModuleID: 0,
            cntThreads: 0,
            th32ParentProcessID: 0,
            pcPriClassBase: 0,
            dwFlags: 0,
            szExeFile: [0; MAX_PATH],
        };

        let success = unsafe {
            let lppe = &mut entry as LPPROCESSENTRY32W;
            if self.is_first {
                (kernel32::Process32FirstW(self.snapshot_handle, lppe))
            }
            else {
                (kernel32::Process32NextW(self.snapshot_handle, lppe))
            }
        };

        let result =
            if success != FALSE {
                Some(Ok(ProcessInfo { entry }))
            } else {
                match get_last_error() {
                    ERROR_NO_MORE_FILES => None,
                    x => Some(Err(io::Error::from_raw_os_error(x as i32)))
                }
            };

        self.is_first = false;
        result
    }
}

pub fn kill_process(process_id: u32) -> io::Result<()> {
    let handle = open_process(process_id, PROCESS_TERMINATE)?;
    let success = unsafe { kernel32::TerminateProcess(handle.0, 1) };
    if success != FALSE { Ok(()) }
    else { Err(io::Error::last_os_error()) }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum CursorType {
    None,
    Arrow,
    Hand,
    No,
    Other(usize),
}

impl Default for CursorType {
    fn default() -> Self { CursorType::None }
}

pub fn get_cursor_type(/*window_handle: HWND*/) -> io::Result<CursorType> {
    
    #[derive(Debug, PartialEq, Eq, Hash)]
    struct SyncCursorHandle(HCURSOR);
    unsafe impl Send for SyncCursorHandle { }
    unsafe impl Sync for SyncCursorHandle { }
    fn load_cursor(cursor_name: LPCWSTR) -> SyncCursorHandle {
        SyncCursorHandle(unsafe {
            user32::LoadCursorW(ptr::null_mut(), cursor_name)
        })
    }

    lazy_static! {
        static ref CURSOR_TYPE_MAP: HashMap<SyncCursorHandle, CursorType> = {
            let mut m = HashMap::with_capacity(4);
            macro_rules! insert { ($k:expr, $v:expr) => (assert!(m.insert($k, $v).is_none())) }
            insert!(SyncCursorHandle(ptr::null_mut()), CursorType::None);
            insert!(load_cursor(IDC_ARROW), CursorType::Arrow);
            insert!(load_cursor(IDC_HAND), CursorType::Hand);
            insert!(load_cursor(IDC_NO), CursorType::No);
            for k in m.keys() { println!("{:?}", k); }
            m
        };
    }

    let mut cursor_info = CURSORINFO {
        cbSize: mem::size_of::<CURSORINFO>() as DWORD,
        flags: 0,
        hCursor: ptr::null_mut(),
        ptScreenPos: POINT { x: 0, y: 0 }
    };

    let success = unsafe {
        GetCursorInfo(&mut cursor_info as PCURSORINFO)
    };

    if success != FALSE {
        Ok(match CURSOR_TYPE_MAP.get(&SyncCursorHandle(cursor_info.hCursor))
        {
            Some(x) => *x,
            None => CursorType::Other(cursor_info.hCursor as usize)
        })
    } else {
        Err(io::Error::last_os_error())
    }

    /*
    fn load_cursor(cursor_name: LPCWSTR) -> HCURSOR {
        unsafe { user32::LoadCursorW(ptr::null_mut(), cursor_name) }
    }

    let current_thread_id = unsafe { kernel32::GetCurrentThreadId() };
    let window_thread_id = unsafe { user32::GetWindowThreadProcessId(window_handle, ptr::null_mut()) };
    let _attach = AttachThreadInput::attach(current_thread_id, window_thread_id)?;
    let cursor_handle = unsafe { user32::GetCursor() };

    Ok(
        if cursor_handle.is_null() { CursorType::None }
        else if cursor_handle == load_cursor(IDC_ARROW) { CursorType::Arrow }
        else if cursor_handle == load_cursor(IDC_HAND) { CursorType::Hand }
        else if cursor_handle == load_cursor(IDC_NO) { CursorType::No }
        else { CursorType::Other(cursor_handle as usize) }
    )
    */
}

pub fn get_cursor_res_name() -> io::Result<ffi::OsString> {
    let mut cursor_info = CURSORINFO {
        cbSize: mem::size_of::<CURSORINFO>() as DWORD,
        flags: 0,
        hCursor: ptr::null_mut(),
        ptScreenPos: POINT { x: 0, y: 0 }
    };

    err_if_zero(unsafe { GetCursorInfo(&mut cursor_info as PCURSORINFO) })?;

    let mut icon_info = ICONINFOEXW {
        cbSize: mem::size_of::<ICONINFOEXW>() as DWORD,
        fIcon: FALSE,
        xHotspot: 0,
        yHotspot: 0,
        hbmMask: ptr::null_mut(),
        hbmColor: ptr::null_mut(),
        wResID: 0,
        szModName: [0; MAX_PATH],
        szResName: [0; MAX_PATH],
    };

    let success = unsafe {
        GetIconInfoExW(cursor_info.hCursor, &mut icon_info as PICONINFOEXW)
    };

    if success != FALSE {
        Ok(ffi::OsString::from_wide(slice_to_zero(&icon_info.szResName)))
    } else {
        Err(io::Error::new(io::ErrorKind::Other, "GetIconInfoExW"))
    }
}

pub fn get_cursor_bitmap() -> io::Result<BitmapBuffer> {
    let mut cursor_info = CURSORINFO {
        cbSize: mem::size_of::<CURSORINFO>() as DWORD,
        flags: 0,
        hCursor: ptr::null_mut(),
        ptScreenPos: POINT { x: 0, y: 0 }
    };

    err_if_zero(unsafe { GetCursorInfo(&mut cursor_info as PCURSORINFO) })?;

    let width = unsafe { user32::GetSystemMetrics(SM_CXCURSOR) };
    let height = unsafe { user32::GetSystemMetrics(SM_CYCURSOR) };

    let dc = DeviceContextFromWindow::get(ptr::null_mut())?;
    let gbmp = GdiBitmap::create(dc.hdc(), width, height)?;

    err_if_zero(unsafe {
        user32::DrawIconEx(gbmp.hdc(), 0, 0, cursor_info.hCursor,
            width, height, 0, ptr::null_mut(), DI_NORMAL)
    })?;

    let mut bmi = BITMAPINFOHEADER {
        biSize: mem::size_of::<BITMAPINFOHEADER>() as DWORD,
        biWidth: width,
        biHeight: height,
        biPlanes: 1,
        biBitCount: 32,
        biCompression: BI_RGB,
        biSizeImage: 0,
        biXPelsPerMeter: 0,
        biYPelsPerMeter: 0,
        biClrUsed: 0,
        biClrImportant: 0,
    };

    let buf_size = (width as usize) * (height as usize) * 4;
    let mut buf = Vec::<u8>::with_capacity(buf_size);
    unsafe { buf.set_len(buf_size); }

    let success = unsafe {
        gdi32::GetDIBits(dc.hdc(), gbmp.hbmp(), 0, height as UINT,
            buf.as_mut_ptr() as LPVOID, &mut bmi as *mut _ as LPBITMAPINFO, DIB_RGB_COLORS)
    };

    match success {
        0 => Err(io::Error::new(io::ErrorKind::Other, "GetDIBits")),
        87 /* ERROR_INVALID_PARAMETER */ => Err(io::Error::new(io::ErrorKind::InvalidInput, "GetDIBits")),
        _ => Ok(BitmapBuffer {
            width: width as u32,
            height: height as u32,
            buffer: buf,
        }),
    }
}

#[derive(Clone, Debug)]
pub struct BitmapBuffer {
    pub width: u32,
    pub height: u32,
    pub buffer: Vec<u8>,
}

#[repr(C)]
#[allow(non_snake_case)]
struct CURSORINFO {
    cbSize: DWORD,
    flags: DWORD,
    hCursor: HCURSOR,
    ptScreenPos: POINT,
}

type PCURSORINFO = *mut CURSORINFO;

#[repr(C)]
#[allow(non_snake_case)]
struct ICONINFOEXW {
    cbSize: DWORD,
    fIcon: BOOL,
    xHotspot: DWORD,
    yHotspot: DWORD,
    hbmMask: HBITMAP,
    hbmColor: HBITMAP,
    wResID: WORD,
    szModName: [WCHAR; MAX_PATH],
    szResName: [WCHAR; MAX_PATH],
}

impl Drop for ICONINFOEXW {
    fn drop(&mut self) {
        if !self.hbmMask.is_null() {
            unsafe { gdi32::DeleteObject(self.hbmMask as HGDIOBJ); }
        }

        if !self.hbmColor.is_null() {
            unsafe { gdi32::DeleteObject(self.hbmColor as HGDIOBJ); }
        }
    }
}

type PICONINFOEXW = *mut ICONINFOEXW;

extern "system" {
    fn GetCursorInfo(pci: PCURSORINFO) -> BOOL;
    fn GetIconInfoExW(hIcon: HICON, piconinfoex: PICONINFOEXW) -> BOOL;
}

#[derive(Debug)]
struct AttachThreadInput {
    attach_thread_id: DWORD,
    attach_to_thread_id: DWORD,
}

impl AttachThreadInput {
    pub fn attach(attach_thread_id: DWORD, attach_to_thread_id: DWORD) -> io::Result<Self> {
        err_if_zero(unsafe {
            user32::AttachThreadInput(attach_thread_id, attach_to_thread_id, TRUE)
        })?;
        Ok(AttachThreadInput { attach_thread_id, attach_to_thread_id })
    }
}

impl Drop for AttachThreadInput {
    fn drop(&mut self) {
        unsafe {
            user32::AttachThreadInput(self.attach_thread_id, self.attach_to_thread_id, FALSE);
        }
    }
}

#[derive(Debug)]
struct DeviceContextFromWindow {
    hwnd: HWND,
    hdc: HDC,
}

impl DeviceContextFromWindow {
    pub fn get(hwnd: HWND) -> io::Result<DeviceContextFromWindow> {
        let hdc = err_if_zero2(unsafe { user32::GetDC(hwnd) }, "GetDC")?;
        Ok(DeviceContextFromWindow { hwnd, hdc })
    }

    pub fn hdc(&self) -> HDC { self.hdc }
}

impl Drop for DeviceContextFromWindow {
    fn drop(&mut self) {
        unsafe { user32::ReleaseDC(self.hwnd, self.hdc); }
    }
}

#[derive(Debug)]
struct GdiBitmap {
    hdc: HDC,
    hbmp: HBITMAP,
}

impl GdiBitmap {
    pub fn create(hdc: HDC, width: c_int, height: c_int) -> io::Result<GdiBitmap> {
        let compat_dc = err_if_zero2(unsafe { gdi32::CreateCompatibleDC(hdc) }, "CreateCompatibleDC")?;

        let hbmp = match err_if_zero2(unsafe { gdi32::CreateCompatibleBitmap(hdc, width, height) }, "CreateCompatibleBitmap") {
            Ok(x) => x,
            Err(x) => {
                unsafe { gdi32::DeleteDC(compat_dc); }
                return Err(x);
            }
        };

        unsafe { gdi32::SelectObject(compat_dc, hbmp as HGDIOBJ); }

        Ok(GdiBitmap { hdc: compat_dc, hbmp })
    }

    pub fn hdc(&self) -> HDC { self.hdc }
    pub fn hbmp(&self) -> HBITMAP { self.hbmp }
}

impl Drop for GdiBitmap {
    fn drop(&mut self) {
        unsafe { 
            gdi32::DeleteDC(self.hdc); // SelectObject してるので先にこっちを消す
            gdi32::DeleteObject(self.hbmp as HGDIOBJ);
        }
    }
}

#[derive(Debug)]
struct Brush {
    hbr: HBRUSH,
}

impl Brush {
    pub fn create_solid_brush(r: u8, g: u8, b: u8, a: u8) -> io::Result<Brush> {
        let color = (r as u32) | (g as u32) << 8 | (b as u32) << 16 | (a as u32) << 24;
        let hbr = err_if_zero2(unsafe { gdi32::CreateSolidBrush(color as COLORREF) }, "CreateSolidBrush")?;
        Ok(Brush { hbr })
    }

    pub fn hbr(&self) -> HBRUSH { self.hbr }
}

impl Drop for Brush {
    fn drop(&mut self) {
        unsafe { gdi32::DeleteObject(self.hbr as HGDIOBJ); }
    }
}

pub fn set_cursor_pos(x: i32, y: i32) -> io::Result<()> {
    err_if_zero(unsafe {
        user32::SetCursorPos(x as c_int, y as c_int)
    })?;
    Ok(())
}
