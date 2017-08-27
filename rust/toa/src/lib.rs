#![feature(nonzero)]
#![allow(dead_code)] // 邪魔なので一旦

extern crate core;
pub extern crate futures;
#[macro_use]
extern crate lazy_static;
pub extern crate tokio_core;
extern crate void;

pub mod future_helper;
pub mod wagahigh;
pub mod server;
pub mod sigint_helper;
