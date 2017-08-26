use std::ffi;
use std::fmt;
use std::heap::{Alloc, Heap};
use std::io;
use std::mem;
use std::os::windows::prelude::*;
use std::os::windows::process::ExitStatusExt;
use std::process;
use std::ptr;
use std::slice;
use futures::{Async, Future, Poll};
use futures::sync::oneshot;
use super::kernel32;
use super::user32;
use super::winapi::*;

/// `GetLastError` はどう転んでも安全なので unsafe 外し
pub fn get_last_error() -> DWORD {
    unsafe { kernel32::GetLastError() }
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
                let success = unsafe {
                    kernel32::GetExitCodeProcess(self.wait_handle.process_handle, &mut exit_code as LPDWORD)
                };
                if success != FALSE {
                    Ok(Async::Ready(process::ExitStatus::from_raw(exit_code as u32)))
                } else {
                    Err(io::Error::last_os_error())
                }
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
    let handle = unsafe {
        kernel32::OpenProcess(desired_access, FALSE, process_id as DWORD)
    };

    if handle.is_null() { 
        Err(io::Error::last_os_error())
    } else {
        Ok(ProcessHandle(handle))
    }
}

fn register_wait(process_handle: ProcessHandle, callback: WAITORTIMERCALLBACK, context: PVOID, timeout_millis: ULONG)
    -> io::Result<WaitHandle>
{
    let mut wait_object: HANDLE = unsafe { mem::uninitialized() };
    let success = unsafe {
        kernel32::RegisterWaitForSingleObject(
            &mut wait_object as PHANDLE,
            process_handle.0,
            callback,
            context,
            timeout_millis,
            WT_EXECUTEINWAITTHREAD | WT_EXECUTEONLYONCE
        )
    };

    if success != FALSE {
        let ph = process_handle.0;
        mem::forget(process_handle); // CloseHandle しない
        Ok(WaitHandle { wait_object, process_handle: ph })
    } else {
        Err(io::Error::last_os_error())
    }
}

extern "system" fn process_wait_callback(parameter: PVOID, _timer_or_wait_fired: BOOLEAN) {
    let sender = unsafe { Box::from_raw(parameter as *mut oneshot::Sender<()>) };
    sender.send(()).ok();
}

pub fn get_process_path(process_id: u32) -> io::Result<ffi::OsString> {
    let process_handle = open_process(process_id, PROCESS_QUERY_INFORMATION)?;
    let buffer = HeapArray::<WCHAR>::alloc(1024);
    let len = unsafe {
        kernel32::K32GetModuleFileNameExW(
            process_handle.0,
            ptr::null_mut(),
            buffer.as_ptr(),
            buffer.len() as DWORD
        )
    };
    if len > 0 {
        let s = unsafe { &buffer.as_slice()[..(len as usize)] };
        Ok(ffi::OsString::from_wide(s))
    } else {
        Err(io::Error::last_os_error())
    }
}

struct HeapArray<T> {
    p: ptr::Unique<T>,
    n: usize,
}

impl<T> HeapArray<T> {
    fn alloc(n: usize) -> Self {
        let p = Heap.alloc_array(n).unwrap();
        HeapArray { p, n }
    }

    fn as_ptr(&self) -> *mut T {
        self.p.as_ptr()
    }

    fn len(&self) -> usize {
        self.n
    }

    // 未初期化領域も slice にできてしまうので unsafe
    unsafe fn as_slice(&self) -> &[T] {
        slice::from_raw_parts(self.p.as_ptr(), self.n)
    }
}

impl<T> Drop for HeapArray<T> {
    fn drop(&mut self) {
        unsafe {
            Heap.dealloc_array(self.p, self.n).ok();
        }
    }
}

pub struct ProcessInfo {
    entry: PROCESSENTRY32W
}

impl ProcessInfo {
    pub fn process_id(&self) -> u32 {
        self.entry.th32ProcessID as u32
    }

    pub fn exe_file_raw(&self) -> &[WCHAR] {
        self.entry.szExeFile.iter()
            .position(|&c| c == 0)
            .map_or(
                &self.entry.szExeFile[..],
                |i| &self.entry.szExeFile[..i]
            )
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
