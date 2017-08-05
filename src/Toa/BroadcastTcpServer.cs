using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace WagahighChoices.Toa
{
    // これ求めているものじゃないやん！！！！！！！！！！！！！
    public class BroadcastTcpServer : IDisposable
    {
        private readonly object _lockObj = new object();
        private readonly TcpListener _listener;
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private bool _isDisposed;

        public event EventHandler<ClientAcceptedEventArgs> ClientAccepted;

        public BroadcastTcpServer(int port)
        {
            this._listener = TcpListener.Create(port);
        }

        public async void Start()
        {
            this._listener.Start();

            // 以下非同期で実行
            try
            {
                while (true)
                {
                    var client = await this._listener.AcceptTcpClientAsync().ConfigureAwait(false);

                    lock (this._lockObj)
                    {
                        this.ClientAccepted?.Invoke(this, new ClientAcceptedEventArgs(client));
                        this._clients.Add(client);
                    }
                }
            }
            catch
            {
                // Dispose されていたら握りつぶす
                if (!this._isDisposed)
                    throw;
            }
        }

        public void Dispose()
        {
            if (this._isDisposed) return;
            this._isDisposed = true;

            this._listener.Stop();

            foreach (var client in this._clients)
                client.Dispose();
        }
    }

    public class ClientAcceptedEventArgs : EventArgs
    {
        public TcpClient Client { get; }

        public ClientAcceptedEventArgs(TcpClient client)
        {
            this.Client = client;
        }
    }
}
