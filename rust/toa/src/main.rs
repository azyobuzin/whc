#![feature(allocator_api, unique)]
#![allow(dead_code)] // 邪魔なので一旦

#[macro_use]
extern crate clap;
extern crate futures;
extern crate tokio_core;

pub mod future_helper;
pub mod wagahigh;
pub mod server;
pub mod sigint_helper;

use std::error::Error;
use clap::{App, Arg};
use futures::Future;
use futures::future;
use future_helper::*;
use wagahigh::*;

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

    let process_future = {
        let find_future: Box<Future<Item = WagahighProcess, Error = Box<Error>>> =
            match matches.value_of_os("directory") {
                // TODO: この辺の unwrap を future に入れてしまいたい
                Some(x) => Box::new(boxed_err(start_wagahigh(x, &handle).unwrap())),
                None => Box::new(future::ok(find_wagahigh().unwrap()))
            };
        
    };

    unimplemented!()
}
