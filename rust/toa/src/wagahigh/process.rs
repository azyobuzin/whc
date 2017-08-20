use std::io;
use std::mem;
use std::process;
use std::path::Path;
use std::ptr;
use futures;
use kernel32;
use time;
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

    /// ウィンドウが見つからなければ `Ok(None)`、エラーが発生した場合は `Err` を返す。
    fn from_process(process_id: u32) -> Result<Option<ExternalWagahighProcess>, WindowsError> {
        match find_main_window(process_id) {
            Ok(hwnd) if hwnd.is_null() => Ok(None),
            Ok(hwnd) => Ok(Some(Self::new(process_id, hwnd))),
            Err(x) => Err(x),
        }
    }
}

impl WagahighProcess for ExternalWagahighProcess {
    fn process_id(&self) -> u32 { self.process_id }
    fn window_handle(&self) -> HWND { self.window_handle }
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

// Future じゃなくて OverClock にしたらかっこいい気がしたけど
// 明らかに紛らわしい
#[derive(Debug)]
pub struct StartWagahighFuture {
    child: Option<process::Child>,
    count: u32,
    start_time: Option<time::Tm>,
}

impl futures::Future for StartWagahighFuture {
    type Item = Option<ChildWagahighProcess>;
    type Error = WindowsError;

    fn poll(&mut self) -> futures::Poll<Self::Item, Self::Error> {
        const DELAY_MILLIS: i64 = 500;
        const MAX_NUM_TRIAL: u32 = 20;

        let now = time::now_utc();

        if let Some(start_time) = self.start_time {
            if now - start_time < time::Duration::milliseconds(DELAY_MILLIS) {
                return Ok(futures::Async::NotReady);
            }
        }

        let hwnd;
        if let Some(ref child) = self.child {
            self.start_time = Some(now);
            self.count += 1;

            hwnd = find_main_window(child.id())?;
        } else {
            unreachable!();
        }

        if hwnd.is_null() {
            if self.count >= 20 { Ok(futures::Async::Ready(None)) }
            else { Ok(futures::Async::NotReady) }
        } else {
            Ok(futures::Async::Ready(Some(
                ChildWagahighProcess::new(
                    mem::replace(&mut self.child, None).unwrap(),
                    hwnd
                )
            )))
        }
    }
}

pub fn start_wagahigh<P: AsRef<Path>>(directory: P) -> io::Result<StartWagahighFuture> {
    let exe_path = directory.as_ref().join("ワガママハイスペック.exe");
    let child = process::Command::new(exe_path)
        .arg("-forcelog=clear")
        .current_dir(directory)
        .spawn()?;

    Ok(StartWagahighFuture {
        child: Some(child),
        count: 0,
        start_time: None,
    })
}
