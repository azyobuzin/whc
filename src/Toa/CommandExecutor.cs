using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace WagahighChoices.Toa
{
    public class CommandExecutor : IDisposable
    {
        public WagahighProcess Process { get; private set; }
        private Thread _thread;
        private AutoResetEvent _resetEvent = new AutoResetEvent(false);
        private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();
        private bool _disposed;

        public CommandExecutor(WagahighProcess process)
        {
            this.Process = process;

            this._thread = new Thread(Worker)
            {
                Name = "Toa Worker",
                IsBackground = false,
            };
            this._thread.Start(this);
        }

        private static void Worker(object o)
        {
            var self = (CommandExecutor)o;

            while (!self._disposed)
            {
                while (self._queue.TryDequeue(out var action))
                {
                    action();
                }

                self._resetEvent.WaitOne();
            }
        }

        private Task<T> CreateTask<T>(Func<T> action)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            this._queue.Enqueue(() =>
            {
                try
                {
                    tcs.TrySetResult(action());
                }
                catch (OperationCanceledException)
                {
                    tcs.TrySetCanceled();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
            this._resetEvent.Set();

            return tcs.Task;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this._disposed) return;

            this._disposed = true;
            this._resetEvent.Set();
            this._thread.Join();

            if (disposing)
            {
                this._resetEvent.Dispose();
                this.Process.Dispose();
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~CommandExecutor()
        {
            this.Dispose(false);
        }

        // TODO: コマンド実装
    }
}
