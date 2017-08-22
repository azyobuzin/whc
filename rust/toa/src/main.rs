#![allow(dead_code)] // 邪魔なので一旦

extern crate ctrlc;
extern crate futures;
extern crate gdi32;
extern crate kernel32;
extern crate tokio_core;
extern crate user32;
extern crate winapi;

mod wagahigh;
mod server;

fn main() {
    //let mut core = tokio_core::reactor::Core::new().unwrap();
    //let handle = core.handle();
    //println!("{:?}", wagahigh::open_process(11784));
}
