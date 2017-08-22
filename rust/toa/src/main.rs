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

use std::sync;
use std::sync::atomic;
use futures::{Async, Future, Poll};

fn main() {
    //let mut core = tokio_core::reactor::Core::new().unwrap();
    //let handle = core.handle();
    //println!("{:?}", wagahigh::open_process(11784));
}

fn wait_sigint_async() -> Result<SigintFuture, ctrlc::Error> {
    let inner = sync::Arc::<SigintFutureInner>::default();
    let inner2 = sync::Arc::clone(&inner);
    ctrlc::set_handler(move || {
        inner2.received_signal.store(true, atomic::Ordering::Relaxed);
        if let Ok(x) = inner2.task.read() {
            if let Some(ref task) = *x {
                task.notify();
            }
        }
    })?;
    Ok(SigintFuture { inner })
}

#[derive(Debug, Default)]
struct SigintFutureInner {
    received_signal: atomic::AtomicBool,
    task: sync::RwLock<Option<futures::task::Task>>,
}

#[derive(Debug)]
struct SigintFuture {
    inner: sync::Arc<SigintFutureInner>,
}

impl Future for SigintFuture {
    type Item = ();
    type Error = ();

    fn poll(&mut self) -> Poll<Self::Item, Self::Error> {
        if let Ok(mut task) = self.inner.task.write() {
            *task = Some(futures::task::current());
        }

        if self.inner.received_signal.load(atomic::Ordering::Relaxed) {
            Ok(Async::Ready(()))
        } else {
            Ok(Async::NotReady)
        }
    }
}
