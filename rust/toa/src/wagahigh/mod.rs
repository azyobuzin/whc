use std;
use std::fmt;
use std::ptr;
use kernel32;
use winapi::*;

mod process;
pub use self::process::*;

#[derive(Debug)]
pub struct WindowsError {
    function_name: &'static str,
    error_code: Option<DWORD>,
    message: Option<String>,
}

impl fmt::Display for WindowsError {
    fn fmt(&self, f: &mut fmt::Formatter) -> Result<(), fmt::Error> {
        f.write_str("An error ")?;
        if let Some(ref error_code) = self.error_code {
            write!(f, "{:#x} ", error_code)?;
        }
        write!(f, "occurred in {}", self.function_name)?;
        if let Some(ref message) = self.message {
            write!(f, ": {}", message.trim())?;
        }
        Ok(())
    }
}

impl std::error::Error for WindowsError {
    fn description(&self) -> &str {
        "An error occured in calling Windows API"
    }
}

impl WindowsError {
    pub fn from_function_name(function_name: &'static str) -> Self {
        WindowsError {
            function_name,
            error_code: None,
            message: None,
        }
    }

    pub fn from_error_code(function_name: &'static str, error_code: DWORD) -> Self {
        WindowsError {
            function_name,
            error_code: Some(error_code),
            message: get_error_message(error_code),
        }
    }

    pub fn from_last_error(function_name: &'static str) -> Self {
        let error_code = unsafe { kernel32::GetLastError() };
        Self::from_error_code(function_name, error_code)
    }
}

fn get_error_message(error_code: DWORD) -> Option<String> {
    let mut pbuf: LPWSTR = ptr::null_mut();

    let result_len = unsafe {
        kernel32::FormatMessageW(
            FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_ARGUMENT_ARRAY | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
            ptr::null(),
            error_code,
            0,
            &mut pbuf as *mut LPWSTR as LPWSTR,
            1,
            ptr::null_mut()
        )
    };

    let result = match result_len {
        0 => None,
        result_len => {
            let s = unsafe { std::slice::from_raw_parts(pbuf, result_len as usize) };
            Some(String::from_utf16_lossy(s))
        }
    };

    if !pbuf.is_null() {
        unsafe { kernel32::LocalFree(pbuf as HLOCAL); }
    }

    result
}
