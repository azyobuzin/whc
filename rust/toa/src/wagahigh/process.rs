use std::error::Error;
use std::fmt;
use std::io;
use std::process;
use std::path::{Path, PathBuf};
use std::ptr;
use std::time;
use futures::{Future, Stream};
use tokio_core;
use winapi::*;
use super::windows_helper::*;

pub trait WagahighProcess {
    fn process_id(&self) -> u32;
    fn window_handle(&self) -> HWND;
    fn directory(&self) -> &Path;

    fn wait_async(&self) -> Result<ProcessFuture, WindowsError> {
        wait_process_async(self.process_id())
    }
}

#[derive(Debug)]
pub struct ChildWagahighProcess {
    child: process::Child,
    window_handle: HWND,
    directory: PathBuf,
}

impl ChildWagahighProcess {
    pub fn new<P>(child: process::Child, window_handle: HWND, directory: P) -> Self
        where P: Into<PathBuf>
    {
        ChildWagahighProcess { child, window_handle, directory: directory.into() }
    }
}

impl WagahighProcess for ChildWagahighProcess {
    fn process_id(&self) -> u32 { self.child.id() }
    fn window_handle(&self) -> HWND { self.window_handle }
    fn directory(&self) -> &Path { &self.directory }
}

impl Drop for ChildWagahighProcess {
    fn drop(&mut self) {
        self.child.kill().ok();
    }
}

#[derive(Clone, Debug)]
pub struct ExternalWagahighProcess {
    process_id: u32,
    window_handle: HWND,
    directory: PathBuf
}

impl ExternalWagahighProcess {
    pub fn new<P>(process_id: u32, window_handle: HWND, directory: P) -> Self
        where P: Into<PathBuf>
    {
        ExternalWagahighProcess { process_id, window_handle, directory: directory.into() }
    }

    pub fn from_process(process_id: u32) -> Result<ExternalWagahighProcess, StartWagahighError> {
        let mut exe_path = PathBuf::from(get_process_path(process_id)?);
        exe_path.pop();

        match find_main_window(process_id) {
            Ok(hwnd) if hwnd.is_null() => Err(StartWagahighError::WindowNotFound),
            Ok(hwnd) => Ok(Self::new(process_id, hwnd, exe_path)),
            Err(x) => Err(x.into()),
        }
    }
}

impl WagahighProcess for ExternalWagahighProcess {
    fn process_id(&self) -> u32 { self.process_id }
    fn window_handle(&self) -> HWND { self.window_handle }
    fn directory(&self) -> &Path { &self.directory }
}

#[derive(Debug)]
pub enum StartWagahighError {
    WindowNotFound,
    WindowsError(WindowsError),
}

impl fmt::Display for StartWagahighError {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        match *self {
            StartWagahighError::WindowNotFound => f.write_str("window not found"),
            StartWagahighError::WindowsError(ref x) => fmt::Display::fmt(x, f),
        }
    }
}

impl Error for StartWagahighError {
    fn description(&self) -> &str {
        match *self {
            StartWagahighError::WindowNotFound => "window not found",
            StartWagahighError::WindowsError(ref x) => x.description(),
        }
    }
}

impl From<WindowsError> for StartWagahighError {
    fn from(x: WindowsError) -> Self {
        StartWagahighError::WindowsError(x)
    }
}

#[inline]
pub fn start_wagahigh<P: AsRef<Path>>(directory: P, handle: &tokio_core::reactor::Handle)
    -> io::Result<Box<Future<Item = ChildWagahighProcess, Error = StartWagahighError>>>
{
    // 型引数 P に依存しないところを切り出し
    fn create_future(directory: &Path, handle: &tokio_core::reactor::Handle)
        -> io::Result<Box<Future<Item = ChildWagahighProcess, Error = StartWagahighError>>>
    {
        const DELAY_MILLIS: u64 = 500;
        const MAX_NUM_TRIAL: u64 = 20;

        let exe_path = directory.join("ワガママハイスペック.exe");
        let mut child = process::Command::new(exe_path)
            .arg("-forcelog=clear")
            .current_dir(directory)
            .spawn()?;
        let pid = child.id();

        let directory = directory.to_path_buf();
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
                    Ok((Some(hwnd), _)) => Ok(ChildWagahighProcess::new(child, hwnd, directory)),
                    Ok((None, _)) => {
                        child.kill().ok();
                        Err(StartWagahighError::WindowNotFound)
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

#[derive(Debug)]
pub enum FindWagahighError {
    ProcessNotFound,
    WindowNotFound,
    WindowsError(WindowsError),
}

impl fmt::Display for FindWagahighError {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        match *self {
            FindWagahighError::ProcessNotFound => f.write_str("process not found"),
            FindWagahighError::WindowNotFound => f.write_str("window not found"),
            FindWagahighError::WindowsError(ref x) => fmt::Display::fmt(x, f),
        }
    }
}

impl Error for FindWagahighError {
    fn description(&self) -> &str {
        match *self {
            FindWagahighError::ProcessNotFound => "process not found",
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

impl From<StartWagahighError> for FindWagahighError {
    fn from(x: StartWagahighError) -> Self {
        match x {
            StartWagahighError::WindowNotFound => FindWagahighError::WindowNotFound,
            StartWagahighError::WindowsError(e) => FindWagahighError::WindowsError(e),
        }
    }
}

pub fn find_wagahigh() -> Result<ExternalWagahighProcess, FindWagahighError> {
    // ワガママハイスペック.exe
    const EXPECTED_EXE: [WCHAR; 14] = [0x30ef, 0x30ac, 0x30de, 0x30de, 0x30cf, 0x30a4, 0x30b9, 0x30da, 0x30c3, 0x30af, 0x002e, 0x0065, 0x0078, 0x0065];
    for r in ProcessIterator::new()? {
        let p = r?;
        if p.exe_file_raw() == EXPECTED_EXE {
            return Ok(ExternalWagahighProcess::from_process(p.process_id())?);
        }
    }
    Err(FindWagahighError::ProcessNotFound)
}
