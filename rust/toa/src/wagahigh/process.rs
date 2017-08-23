use std::error::Error;
use std::ffi;
use std::fmt;
use std::heap::{Alloc, Heap};
use std::io;
use std::mem;
use std::os::windows::prelude::*;
use std::os::windows::process::ExitStatusExt;
use std::process;
use std::path::Path;
use std::ptr;
use std::slice;
use std::time;
use futures::{Async, Future, Poll, Stream};
use futures::sync::oneshot;
use kernel32;
use tokio_core;
use user32;
use winapi::*;
use super::WindowsError;

pub trait WagahighProcess {
    fn process_id(&self) -> u32;
    fn window_handle(&self) -> HWND;
}

#[derive(Debug)]
pub struct ChildWagahighProcess {
    child: process::Child,
    window_handle: HWND,
}

impl ChildWagahighProcess {
    pub fn new(child: process::Child, window_handle: HWND) -> Self {
        ChildWagahighProcess { child, window_handle }
    }
}

impl WagahighProcess for ChildWagahighProcess {
    fn process_id(&self) -> u32 { self.child.id() }
    fn window_handle(&self) -> HWND { self.window_handle }
}

impl Drop for ChildWagahighProcess {
    fn drop(&mut self) {
        self.child.kill().ok();
    }
}

#[derive(Clone, Copy, Debug)]
pub struct ExternalWagahighProcess {
    process_id: u32,
    window_handle: HWND,
}

impl ExternalWagahighProcess {
    pub fn new(process_id: u32, window_handle: HWND) -> Self {
        ExternalWagahighProcess { process_id, window_handle }
    }

    pub fn from_process(process_id: u32) -> Result<ExternalWagahighProcess, FindWagahighError> {
        match find_main_window(process_id) {
            Ok(hwnd) if hwnd.is_null() => Err(FindWagahighError::WindowNotFound),
            Ok(hwnd) => Ok(Self::new(process_id, hwnd)),
            Err(x) => Err(x.into()),
        }
    }
}

impl WagahighProcess for ExternalWagahighProcess {
    fn process_id(&self) -> u32 { self.process_id }
    fn window_handle(&self) -> HWND { self.window_handle }
}

#[derive(Debug)]
pub enum FindWagahighError {
    WindowNotFound,
    WindowsError(WindowsError),
}

impl fmt::Display for FindWagahighError {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        match *self {
            FindWagahighError::WindowNotFound => f.write_str("window not found"),
            FindWagahighError::WindowsError(ref x) => fmt::Display::fmt(x, f),
        }
    }
}

impl Error for FindWagahighError {
    fn description(&self) -> &str {
        match *self {
            FindWagahighError::WindowNotFound => "window not found",
            FindWagahighError::WindowsError(ref x) => x.description(),
        }
    }
}

impl From<WindowsError> for FindWagahighError {
    fn from(x: WindowsError) -> Self {
        FindWagahighError::WindowsError(x)
    }
}

fn find_main_window(process_id: u32) -> Result<HWND, WindowsError> {
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
        let error_code = unsafe { kernel32::GetLastError() };
        if error_code != 0 {
            // エラーコード 0 は lpEnumFunc が FALSE を返しただけ
            return Err(WindowsError::from_error_code("EnumWindows", error_code));
        }
    }

    Ok(lparam.main_window_handle)
}

#[inline]
pub fn start_wagahigh<P: AsRef<Path>>(directory: P, handle: &tokio_core::reactor::Handle)
    -> io::Result<Box<Future<Item = ChildWagahighProcess, Error = FindWagahighError>>>
{
    // 型引数 P に依存しないところを切り出し
    fn create_future(directory: &Path, handle: &tokio_core::reactor::Handle)
        -> io::Result<Box<Future<Item = ChildWagahighProcess, Error = FindWagahighError>>>
    {
        const DELAY_MILLIS: u64 = 500;
        const MAX_NUM_TRIAL: u64 = 20;

        let exe_path = directory.join("ワガママハイスペック.exe");
        let mut child = process::Command::new(exe_path)
            .arg("-forcelog=clear")
            .current_dir(directory)
            .spawn()?;
        let pid = child.id();

        Ok(Box::new(
            tokio_core::reactor::Interval
                ::new_at(time::Instant::now(), time::Duration::from_millis(DELAY_MILLIS), handle)?
                .take(MAX_NUM_TRIAL)
                .then(move |r| match r {
                    Ok(_) => find_main_window(pid),
                    Err(_) => Ok(ptr::null_mut()), // Interval に Err を返すコードなし
                })
                .filter(|hwnd| !hwnd.is_null())
                .into_future()
                .then(move |r| match r {
                    Ok((Some(hwnd), _)) => Ok(ChildWagahighProcess::new(child, hwnd)),
                    Ok((None, _)) => {
                        child.kill().ok();
                        Err(FindWagahighError::WindowNotFound)
                    }
                    Err((x, _)) => {
                        child.kill().ok();
                        Err(x.into())
                    }
                })
        ))
    }

    create_future(directory.as_ref(), handle)
}

pub fn wait_process_async(process_id: u32) -> Result<ProcessFuture, WindowsError> {
    let process_handle = open_process(process_id)?;

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
    type Error = WindowsError;

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
                    Err(WindowsError::from_last_error("GetExitCodeProcess"))
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

fn open_process(process_id: u32) -> Result<ProcessHandle, WindowsError> {
    let handle = unsafe {
        kernel32::OpenProcess(
            SYNCHRONIZE | PROCESS_QUERY_INFORMATION,
            FALSE,
            process_id as DWORD
        )
    };

    if handle.is_null() { 
        Err(WindowsError::from_last_error("OpenProcess"))
    } else {
        Ok(ProcessHandle(handle))
    }
}

fn register_wait(process_handle: ProcessHandle, callback: WAITORTIMERCALLBACK, context: PVOID, timeout_millis: ULONG)
    -> Result<WaitHandle, WindowsError>
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
        Err(WindowsError::from_last_error("RegisterWaitForSingleObject"))
    }
}

extern "system" fn process_wait_callback(parameter: PVOID, _timer_or_wait_fired: BOOLEAN) {
    let sender = unsafe { Box::from_raw(parameter as *mut oneshot::Sender<()>) };
    sender.send(()).ok();
}

pub fn get_process_path(process_id: u32) -> Result<ffi::OsString, WindowsError> {
    let process_handle = open_process(process_id)?;
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
        Err(WindowsError::from_last_error("K32GetModuleFileNameExW"))
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
