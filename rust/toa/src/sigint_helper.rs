extern crate ctrlc;

use std::sync;
use std::sync::atomic;
use futures::{Async, Future, Poll};
use futures::task;
use void::Void;

pub fn wait_sigint_async() -> Result<SigintFuture, ctrlc::Error> {
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
    task: sync::RwLock<Option<task::Task>>,
}

#[derive(Debug)]
pub struct SigintFuture {
    inner: sync::Arc<SigintFutureInner>,
}

impl Future for SigintFuture {
    type Item = ();
    type Error = Void;

    fn poll(&mut self) -> Poll<Self::Item, Self::Error> {
        // 効率悪そう
        if let Ok(mut task) = self.inner.task.write() {
            *task = Some(task::current());
        }

        if self.inner.received_signal.load(atomic::Ordering::Relaxed) {
            Ok(Async::Ready(()))
        } else {
            Ok(Async::NotReady)
        }
    }
}
