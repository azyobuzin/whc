using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WagahighChoices.Ashe
{
    internal class Logger
    {
        private readonly object _lockObj = new object();
        private SearchDirector _searchDirector;
        private readonly List<BufferEntry> _buffer = new List<BufferEntry>();

        public async Task SetSearchDirectorAsync(SearchDirector searchDirector)
        {
            BufferEntry[] records;

            lock (this._lockObj)
            {
                this._searchDirector = searchDirector;
                records = this._buffer.ToArray();
                this._buffer.Clear();
            }

            if (records.Length == 0) return;

            try
            {
                foreach (var record in records)
                {
                    await searchDirector.LogAsync(record.Message, record.IsError, record.Timestamp).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Console.Error.WriteLine("[{0}] Log Error: {1}", DateTime.Now, ex);
                }
                catch { }
            }
        }

        private async void Log(string message, bool isError)
        {
            var timestamp = DateTimeOffset.Now;

            try
            {
                Console.WriteLine("[{0:G}] {1}", timestamp, message);
            }
            catch { }

            SearchDirector searchDirector;
            lock (this._lockObj)
            {
                searchDirector = this._searchDirector;
                if (searchDirector == null)
                {
                    // searchDirector 設定前ならば、保存しておいて、後で流す
                    this._buffer.Add(new BufferEntry(message, isError, timestamp));
                    return;
                }
            }

            try
            {
                await searchDirector.LogAsync(message, isError, timestamp).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                try
                {
                    Console.Error.WriteLine("[{0}] Log Error: {1}", DateTime.Now, ex);
                }
                catch { }
            }
        }

        public void Info(string message) => this.Log("Info: " + message, false);

        public void Error(string message) => this.Log("Error: " + message, true);

        private sealed class BufferEntry
        {
            public string Message { get; }
            public bool IsError { get; }
            public DateTimeOffset Timestamp { get; }

            public BufferEntry(string message, bool isError, DateTimeOffset timestamp)
            {
                this.Message = message;
                this.IsError = isError;
                this.Timestamp = timestamp;
            }
        }
    }
}
