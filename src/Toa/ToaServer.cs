using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using WagahighChoices.Toa.Messages;
using WagahighChoices.Toa.Messages.ClientToServer;
using WagahighChoices.Toa.Messages.ServerToClient;
using WagahighChoices.Toa.Utils;

namespace WagahighChoices.Toa
{
    public class ToaServer : IDisposable
    {
        private const int ServerHeaderLength = 5;
        private const int ClientHeaderLength = 9;

        private static readonly ArrayPool<byte> s_pool = ArrayPool<byte>.Shared;

        private bool _disposed;
        private readonly TcpListener _listener;
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private CommandExecutor _executor;
        private readonly AsyncReaderWriterLock _clientsLock = new AsyncReaderWriterLock();

        private ToaServer(int port)
        {
            this._listener = TcpListener.Create(port);
            this._listener.Start();
            this.Accept().Forget();
        }

        public static ToaServer Start(int port)
        {
            return new ToaServer(port);
        }

        private async Task Accept()
        {
            while (!this._disposed)
            {
                TcpClient client;

                try
                {
                    client = await this._listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.LogException(ex);
                    continue;
                }

                Log.WriteMessage("Accepted: " + client.Client.RemoteEndPoint);

                var releaser = await this._clientsLock.WriteLockAsync();
                try
                {
                    if (this._executor != null)
                    {
                        try
                        {
                            await this.SendReadyAsync(client.GetStream()).ConfigureAwait(false);
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
                finally
                {
                    await releaser.ReleaseAsync().ConfigureAwait(false);
                    releaser.Dispose();
                }

                this.ClientWorker(client).Forget();
            }
        }

        private async Task ClientWorker(TcpClient client)
        {
            using (client)
            {
                var stream = client.GetStream();
                var header = new byte[ClientHeaderLength];

                while (true) // 通信失敗時に break
                {
                    int bytesRead;
                    try
                    {
                        bytesRead = await stream.ReadExactAsync(header, 0, ClientHeaderLength).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Log.LogException(ex, client);
                        break;
                    }

                    if (bytesRead != ServerHeaderLength) break;

                    var messageId = SerializationUtils.ReadInt(header, 0);
                    var messageCode = (ClientToServerMessageCode)header[4];
                    var dataLen = SerializationUtils.ReadInt(header, 5);
                    var data = s_pool.Rent(dataLen);

                    ISerializableMessage replyMessage;

                    try
                    {
                        try
                        {
                            bytesRead = await stream.ReadExactAsync(data, 0, dataLen).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Log.LogException(ex, client);
                            break;
                        }

                        if (bytesRead != dataLen) break;


                        if (this._executor == null)
                        {
                            replyMessage = new ReplyErrorMessage(messageId, ServerErrorCode.NotReady);
                        }
                        else
                        {
                            try
                            {
                                replyMessage = await HandleMessage(messageId, messageCode, new ArraySegment<byte>(data, 0, dataLen)).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                Log.LogException(ex, client);
                                replyMessage = new ReplyErrorMessage(messageId, ServerErrorCode.ServerError);
                            }
                        }
                    }
                    finally
                    {
                        s_pool.Return(data);
                    }

                    if (replyMessage != null)
                    {
                        try
                        {
                            await this.SendMessageAsync(stream, replyMessage).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Log.LogException(ex, client);
                            break;
                        }
                    }
                }

                var releaser = await this._clientsLock.WriteLockAsync();
                try
                {
                    this._clients.Remove(client);
                }
                finally
                {
                    await releaser.ReleaseAsync().ConfigureAwait(false);
                    releaser.Dispose();
                }
            }
        }

        private async Task<ISerializableMessage> HandleMessage(int messageId, ClientToServerMessageCode messageCode, ArraySegment<byte> data)
        {
            throw new NotImplementedException(); // TODO
        }

        [SuppressMessage("Usage", "VSTHRD100", Justification = "呼び出し元に async 処理があることを意識させない")]
        public async void SetExecutor(CommandExecutor executor)
        {
            var releaser = await this._clientsLock.ReadLockAsync();
            try
            {
                this._executor = executor;

                await Task.WhenAll(
                    this._clients.Select(client =>
                        this.SendReadyAsync(client.GetStream())
                            .ContinueWith(
                                (t, state) => Log.LogException(t.Exception, (TcpClient)state),
                                client,
                                CancellationToken.None,
                                TaskContinuationOptions.OnlyOnFaulted,
                                TaskScheduler.Default
                            )
                    )
                ).ConfigureAwait(false);
            }
            finally
            {
                await releaser.ReleaseAsync().ConfigureAwait(false);
                releaser.Dispose();
            }
        }

        private async Task SendMessageAsync(Stream client, ISerializableMessage message)
        {
            var len = message.ComputeLength();
            var bufLen = ServerHeaderLength + len;
            var buf = s_pool.Rent(bufLen);

            try
            {
                var ul = (uint)len;
                buf[4] = (byte)(ul >> 24);
                buf[3] = (byte)(ul >> 16);
                buf[2] = (byte)(ul >> 8);
                buf[1] = (byte)ul;
                buf[0] = message.MessageCode;

                if (len > 0)
                {
                    message.Serialize(new ArraySegment<byte>(buf, ServerHeaderLength, len));
                }

                await client.WriteAsync(buf, 0, bufLen).ConfigureAwait(false);
            }
            finally
            {
                s_pool.Return(buf);
            }
        }

        private Task SendReadyAsync(Stream client) => this.SendMessageAsync(client, ReadyMessage.Default);

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
}
