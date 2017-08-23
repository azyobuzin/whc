#![feature(allocator_api, unique)]
#![allow(dead_code)] // 邪魔なので一旦

#[macro_use]
extern crate clap;
extern crate futures;
extern crate tokio_core;

pub mod wagahigh;
pub mod server;
pub mod sigint_helper;

use clap::{App, Arg};
use wagahigh::WagahighProcess;

fn main() {
    let matches = App::new("Toa")
        .version(crate_version!())
        .author(crate_authors!("\n"))
        .about(crate_description!())
        .arg(
            Arg::with_name("directory")
                .short("d")
                .long("directory")
                .value_name("PATH")
                .help("ワガママハイスペック.exe が存在するディレクトリ")
        )
        .arg(
            Arg::with_name("port")
                .short("p")
                .long("port")
                .value_name("NUM")
                .help("使用するポート番号")
        )
        .get_matches();

    let mut core = tokio_core::reactor::Core::new().unwrap();
    let handle = core.handle();

    let sigint_future = sigint_helper::wait_sigint_async().unwrap();
    
    unimplemented!()
}

