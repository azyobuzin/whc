using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WagahighChoices.Toa.Messages;
using WagahighChoices.Toa.Messages.ClientToServer;
using WagahighChoices.Toa.Messages.ServerToClient;
using WagahighChoices.Toa.Utils;
using ZeroFormatter.Internal;

namespace WagahighChoices.Toa
{
    public class ToaServer : IDisposable
    {
        private bool _disposed;
        private readonly TcpListener _listener;
        private readonly List<Client> _clients = new List<Client>();
        private CommandExecutor _executor;
        private readonly AsyncLock _clientsLock = new AsyncLock();

        private ToaServer(int port)
        {
            this._listener = TcpListener.Create(port);
            this._listener.Start();
            Log.WriteMessage("Listening " + port);
            this.Accept();
        }

        public static ToaServer Start(int port)
        {
            return new ToaServer(port);
        }

        public async void SetExecutor(CommandExecutor executor)
        {
            if (this._executor != null) throw new InvalidOperationException();

            using (await this._clientsLock.EnterAsync().ConfigureAwait(false))
            {
                this._executor = executor;

                foreach (var client in this._clients)
                {
                    SendReadyAsync(client)
                        .ContinueWith(
                            (t, state) => Log.LogException(t.Exception, (Client)state),
                            client,
                            CancellationToken.None,
                            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                            TaskScheduler.Default
                        )
                        .Forget(); // メッセージ送信自体はロック外で良い
                }
            }
        }

        private static Task SendReadyAsync(Client client) => client.SendMessageAsync(ReadyMessage.Default);

        private async void Accept()
        {
            while (!this._disposed)
            {
                Client client;

                try
                {
                    client = new Client(await this._listener.AcceptTcpClientAsync().ConfigureAwait(false));
                }
                catch (Exception ex)
                {
                    Log.LogException(ex);
                    continue;
                }

                Log.WriteMessage("Accepted: " + client.RemoteEndPoint);

                using (await this._clientsLock.EnterAsync().ConfigureAwait(false))
                {
                    if (this._executor != null)
                    {
                        try
                        {
                            await SendReadyAsync(client).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Log.LogException(ex, client);
                            client.Dispose(); // エラーが発生したクライアントはなかったことにする
                            continue;
                        }
                    }

                    this._clients.Add(client);
                }

                this.ClientWorker(client);
            }
        }

        private async void ClientWorker(Client client)
        {
            using (client)
            {
                var dataBuffer = new byte[32];

                while (true) // 通信失敗時に break
                {
                    ClientMessageHeader header;

                    try
                    {
                        header = await client.ReadHeaderAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Log.LogException(ex, client);
                        break;
                    }

                    if (!header.Succeeded) break;

                    if (dataBuffer.Length < header.DataLength)
                        dataBuffer = new byte[GetBufferSize(header.DataLength)];

                    try
                    {
                        if (!await client.ReadDataAsync(dataBuffer, 0, header.DataLength).ConfigureAwait(false))
                            break;
                    }
                    catch (Exception ex)
                    {
                        Log.LogException(ex, client);
                        break;
                    }

                    ToaMessage replyMessage;

                    if (this._executor == null)
                    {
                        replyMessage = new ReplyErrorMessage(header.MessageId, ServerErrorCode.NotReady);
                    }
                    else
                    {
                        try
                        {
                            replyMessage = await HandleMessage(
                                header.MessageId,
                                header.MessageCode,
                                new ArraySegment<byte>(dataBuffer, 0, header.DataLength)
                            ).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Log.LogException(ex, client);
                            replyMessage = new ReplyErrorMessage(header.MessageId, ServerErrorCode.ServerError);
                        }
                    }

                    if (replyMessage != null)
                    {
                        try
                        {
                            await client.SendMessageAsync(replyMessage).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Log.LogException(ex, client);
                            break;
                        }
                    }
                }

                Log.WriteMessage("Disconnecting: " + client.RemoteEndPoint);

                using (await this._clientsLock.EnterAsync().ConfigureAwait(false))
                {
                    this._clients.Remove(client);
                }
            }
        }

        private static int GetBufferSize(int minLength)
        {
            Debug.Assert(minLength > 0);

            var v = (uint)minLength;
            // http://graphics.stanford.edu/~seander/bithacks.html#RoundUpPowerOf2
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            var len = (int)v;
            return len < minLength ? int.MaxValue : len;
        }

        private async Task<ToaMessage> HandleMessage(int messageId, ClientToServerMessageCode messageCode, ArraySegment<byte> data)
        {
            throw new NotImplementedException(); // TODO
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this._disposed) return;

            this._disposed = true;
            this._listener.Stop();

            if (disposing)
            {
                foreach (var client in this._clients)
                    client.Dispose();

                this._clientsLock.Dispose();
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    internal sealed class Client : IDisposable
    {
        private readonly TcpClient _tcpClient;
        private readonly AsyncLock _writeLock = new AsyncLock();

        [ThreadStatic]
        private static byte[] s_headerBuffer;

        public Client(TcpClient tcpClient)
        {
            this._tcpClient = tcpClient;
        }

        public EndPoint RemoteEndPoint => this._tcpClient.Client.RemoteEndPoint;

        public async Task<ClientMessageHeader> ReadHeaderAsync()
        {
            var buf = s_headerBuffer ?? (s_headerBuffer = new byte[MessageConstants.ClientMessageHeaderLength]);

            var bytesRead = await this._tcpClient.GetStream()
                .ReadExactAsync(buf, 0, MessageConstants.ClientMessageHeaderLength)
                .ConfigureAwait(false);

            var result = new ClientMessageHeader();

            if (bytesRead == MessageConstants.ClientMessageHeaderLength)
            {
                result.MessageId = BinaryUtil.ReadInt32(ref buf, 0);
                result.MessageCode = (ClientToServerMessageCode)buf[4];
                result.DataLength = BinaryUtil.ReadInt32(ref buf, 5);
                result.Succeeded = true;
            }

            return result;
        }

        public async Task<bool> ReadDataAsync(byte[] buffer, int offset, int count)
        {
            var bytesRead = await this._tcpClient.GetStream().ReadExactAsync(buffer, offset, count).ConfigureAwait(false);
            return bytesRead == count;
        }

        public async Task SendMessageAsync(ToaMessage message)
        {
            var buf = new byte[32];
            buf[0] = message.MessageCode;
            var dataLen = message.Serialize(ref buf, 5);
            BinaryUtil.WriteInt32Unsafe(ref buf, 1, dataLen);

            var bufLen = MessageConstants.ServerMessageHeaderLength + dataLen;
            Log.WriteMessage("Sending " + bufLen + " bytes to " + this.RemoteEndPoint);

            using (await this._writeLock.EnterAsync().ConfigureAwait(false))
            {
                await this._tcpClient.GetStream().WriteAsync(buf, 0, bufLen).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            this._tcpClient.Dispose();
            this._writeLock.Dispose();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    internal struct ClientMessageHeader
    {
        public bool Succeeded;
        public int MessageId;
        public ClientToServerMessageCode MessageCode;
        public int DataLength;
    }
}
