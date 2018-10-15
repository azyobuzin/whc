using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WagahighChoices.Ashe
{
    internal class Logger
    {
        private readonly object _lockObj = new object();
        private SearchDirector _searchDirector;
        private readonly List<(string, DateTimeOffset)> _buffer = new List<(string, DateTimeOffset)>();

        public async Task SetSearchDirectorAsync(SearchDirector searchDirector)
        {
            (string, DateTimeOffset)[] records;

            lock (this._lockObj)
            {
                this._searchDirector = searchDirector;
                records = this._buffer.ToArray();
                this._buffer.Clear();
            }

            if (records.Length == 0) return;

            try
            {
                foreach (var (message, timestamp) in records)
                {
                    await searchDirector.Log(message, timestamp).ConfigureAwait(false);
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

        private async void Log(string message)
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
                    this._buffer.Add((message, timestamp));
                    return;
                }
            }

            try
            {
                await searchDirector.Log(message, timestamp).ConfigureAwait(false);
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

        public void Info(string message) => this.Log("Info: " + message);

        public void Error(string message) => this.Log("Error: " + message);
    }
}
