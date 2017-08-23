use std::ffi;
use std::fmt;
use std::mem;
use std::os::windows::prelude::*;
use kernel32;
use winapi::*;
use super::WindowsError;

pub struct ProcessInfo {
    entry: PROCESSENTRY32W
}

impl ProcessInfo {
    pub fn process_id(&self) -> u32 {
        self.entry.th32ProcessID as u32
    }

    pub fn exe_file(&self) -> ffi::OsString {
        let s = self.entry.szExeFile.iter()
            .position(|&c| c == 0)
            .map_or(
                &self.entry.szExeFile[..],
                |i| &self.entry.szExeFile[..i]
            );
        ffi::OsString::from_wide(s)
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
    pub fn new() -> Result<Self, WindowsError> {
        let handle = unsafe {
            kernel32::CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0)
        };
        if handle.is_null() || handle == INVALID_HANDLE_VALUE {
            Err(WindowsError::from_last_error("CreateToolhelp32Snapshot"))
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
    type Item = Result<ProcessInfo, WindowsError>;

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
            if self.is_first { kernel32::Process32FirstW(self.snapshot_handle, lppe) }
            else { kernel32::Process32NextW(self.snapshot_handle, lppe) }
        };

        let result =
            if success != FALSE {
                Some(Ok(ProcessInfo { entry }))
            } else {
                match unsafe { kernel32::GetLastError() } {
                    ERROR_NO_MORE_FILES => None,
                    x => Some(Err(WindowsError::from_error_code(
                        if self.is_first { "Process32FirstW" }
                        else { "Process32NextW" },
                        x
                    )))
                }
            };

        self.is_first = false;
        result
    }
}
