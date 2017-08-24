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

type BoxedFuture<T> = Box<Future<Item = T, Error = Box<Error>>>;

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

    let sigint_future = sigint_helper::wait_sigint_async().unwrap()
        .and_then(|_| {
            println!("Terminating");
            Ok(())
        });

    let process_future = {
        let (find_future, kill_when_exit): (BoxedFuture<WagahighProcess>, _) =
            match matches.value_of_os("directory") {
                Some(x) => (Box::new(from_result_boxed(start_wagahigh(x, &handle))), true),
                None => (Box::new(boxed_err(future::result(find_wagahigh()))), false),
            };

        find_future
            .and_then(move |process| from_result_boxed(
                process.wait_async(kill_when_exit)
                    .map(|f| f.and_then(|exit_status| {
                            match exit_status.code() {
                                Some(x) => println!("Exit code: {}", x),
                                None => println!("Terminated"),
                            }
                            Ok(())
                    }))
            ))
    };

    let main_future = sigint_future.select2(process_future)
        .then(|r| match r {
            Ok(_) => Ok(()),
            Err(future::Either::A(_)) => unreachable!(),
            Err(future::Either::B((e, _))) => Err(e),
        });

    if let Err(x) = core.run(main_future) {
        println!("{}", x);
    }
}
