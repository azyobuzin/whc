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

pub fn from_result_boxed<F, I, ER, EF>(r: Result<F, ER>) -> FromResultBoxed<F, ER>
    where F: Future<Item = I, Error = EF>, ER: Error + 'static, EF: Error + 'static
{
    FromResultBoxed { inner: r.map_err(Some) }
}

#[derive(Clone, Debug)]
pub struct FromResultBoxed<F, E> { inner: Result<F, Option<E>> }

impl<F, I, ER, EF> Future for FromResultBoxed<F, ER>
    where F: Future<Item = I, Error = EF>, ER: Error + 'static, EF: Error + 'static
{
    type Item = I;
    type Error = Box<Error>;

    fn poll(&mut self) -> Poll<Self::Item, Self::Error> {
        match self.inner {
            Ok(ref mut x) => match x.poll() {
                Ok(y) => Ok(y),
                Err(y) => Err(Box::new(y)),
            }
            Err(ref mut x) => Err(Box::new(x.take().expect("poll 呼ばれすぎ"))),
        }
    }
}
