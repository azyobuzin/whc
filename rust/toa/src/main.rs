#![allow(dead_code)] // 邪魔なので一旦

extern crate futures;
extern crate kernel32;
extern crate tokio_core;
extern crate time;
extern crate user32;
extern crate winapi;

mod wagahigh;
mod server;

fn main() {
    //println!("{:?}", wagahigh::get_error_message(0x00000014).as_ref().map(|x| x.trim()));
}
