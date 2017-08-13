using System;
using System.Threading;
using System.Threading.Tasks;

namespace WagahighChoices.Toa.Utils
{
    public sealed class AsyncLock : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly Releaser _releaser;

        public AsyncLock()
        {
            this._releaser = new Releaser(this._semaphore);
        }

        public async Task<IDisposable> EnterAsync()
        {
            await this._semaphore.WaitAsync().ConfigureAwait(false);
            return this._releaser;
        }

        public void Dispose()
        {
            this._semaphore.Dispose();
        }

        private class Releaser : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;

            public Releaser(SemaphoreSlim semaphore)
            {
                this._semaphore = semaphore;
            }

            public void Dispose()
            {
                this._semaphore.Release();
            }
        }
    }
}
