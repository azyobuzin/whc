use std::error::Error;
use futures::{Future, Poll};

pub fn boxed_err<F, E>(f: F) -> BoxedErr<F>
    where F: Future<Error = E>, E: Error + 'static
{
    BoxedErr { inner: f }
}

#[derive(Clone, Debug)]
pub struct BoxedErr<F> { inner: F }

impl<F, E> Future for BoxedErr<F>
    where F: Future<Error = E>, E: Error + 'static
{
    type Item = <F as Future>::Item;
    type Error = Box<Error>;

    fn poll(&mut self) -> Poll<Self::Item, Self::Error> {
        match self.inner.poll() {
            Ok(x) => Ok(x),
            Err(x) => Err(Box::new(x)),
        }
    }
}
