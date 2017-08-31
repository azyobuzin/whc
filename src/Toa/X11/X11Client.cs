using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WagahighChoices.Toa.X11
{
    public partial class X11Client : IDisposable
    {
        protected Stream Stream { get; }

        private readonly SemaphoreSlim _requestSemaphore = new SemaphoreSlim(1, 1);

        private readonly ConcurrentDictionary<ushort, Action<byte[], byte[], Exception>> _replyActions = new ConcurrentDictionary<ushort, Action<byte[], byte[], Exception>>();

        private ushort _sequenceNumber = 1;

        private SetupResponseData _setup;

        public string ServerVendor { get; private set; }

        public IReadOnlyList<Screen> Screens { get; private set; }

        private IReadOnlyDictionary<uint, VisualType> _visualTypes;

        protected X11Client(Stream stream)
        {
            this.Stream = stream;
        }

        public static async Task<X11Client> ConnectAsync(string host, int display)
        {
            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(host, 6000 + display).ConfigureAwait(false);
            var x11Client = new X11Client(tcpClient.GetStream());
            await x11Client.SetupConnectionAsync().ConfigureAwait(false);
            x11Client.ReceiveWorker();
            return x11Client;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing) this.Stream.Dispose();
        }

        public void Dispose() => this.Dispose(true);

        protected async Task SendRequestAsync(Func<ushort, Task> sendAction)
        {
            await this._requestSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                await sendAction(this._sequenceNumber).ConfigureAwait(false);
                this._sequenceNumber++;
            }
            finally
            {
                this._requestSemaphore.Release();
            }
        }

        protected async Task<T> SendRequestAsync<T>(Func<ushort, Task> sendAction, Func<byte[], byte[], T> replyAction)
        {
            var tcs = new TaskCompletionSource<T>();

            void action(byte[] replyHeader, byte[] replyContent, Exception excpetion)
            {
                if (excpetion != null)
                {
                    tcs.TrySetException(excpetion);
                    return;
                }

                try
                {
                    tcs.TrySetResult(replyAction(replyHeader, replyContent));
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }

            var sequenceNumber = this._sequenceNumber;

            await this._requestSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!this._replyActions.TryAdd(sequenceNumber, action))
                    throw new InvalidOperationException("Duplicated sequence number");

                await sendAction(sequenceNumber).ConfigureAwait(false);
                this._sequenceNumber++;
            }
            catch
            {
                this._replyActions.TryRemove(sequenceNumber, out var _);
            }
            finally
            {
                this._requestSemaphore.Release();
            }

            return await tcs.Task.ConfigureAwait(false);
        }

        private static int ComputePad(int e) => (4 - (e % 4)) % 4;

        private async Task ReadExactAsync(byte[] buffer, int count)
        {
            for (var i = 0; i < count;)
            {
                var bytesRead = await this.Stream.ReadAsync(buffer, i, count - i).ConfigureAwait(false);
                if (bytesRead == 0) throw new EndOfStreamException();
                i += bytesRead;
            }
        }

        private static string ReadString8(byte[] buffer, int index, int count)
        {
            return Encoding.UTF8.GetString(buffer, index, count);
        }

        private async Task SetupConnectionAsync()
        {
            SetupResponseHeader responseHeader;

            using (var rentedArray = ArrayPool.Rent<byte>(SetupRequestDataSize))
            {
                var buf = rentedArray.Array;

                unsafe
                {
                    fixed (byte* p = buf)
                    {
                        *(SetupRequestData*)p = new SetupRequestData()
                        {
                            ByteOrder = BitConverter.IsLittleEndian ? (byte)0x6c : (byte)0x42,
                            ProtocolMajorVersion = 11,
                            ProtocolMinorVersion = 0,
                            LengthOfAuthorizationProtocolName = 0,
                            LengthOfAuthorizationProtocolData = 0,
                        };
                    }
                }

                await this.Stream.WriteAsync(buf, 0, SetupRequestDataSize).ConfigureAwait(false);

                await this.ReadExactAsync(buf, SetupResponseHeaderSize).ConfigureAwait(false);

                unsafe
                {
                    fixed (byte* p = buf)
                    {
                        responseHeader = *(SetupResponseHeader*)p;
                    }
                }
            }

            var additionalDataLength = responseHeader.LengthOfAdditionalData * 4;
            using (var rentedArray = ArrayPool.Rent<byte>(additionalDataLength))
            {
                var buf = rentedArray.Array;
                await this.ReadExactAsync(buf, additionalDataLength).ConfigureAwait(false);

                switch (responseHeader.Status)
                {
                    case 0: // Failed
                        throw new X11Exception(string.Format(
                            "The server (X{0}.{1}) refused the connection: {2}",
                            responseHeader.ProtocolMajorVersion,
                            responseHeader.ProtocolMinorVersion,
                            ReadString8(buf, 0, responseHeader.LengthOfReasonIfFailed)
                        ));
                    case 2: // Authenticate
                        throw new X11Exception("Authentication is required: "
                            + ReadString8(buf, 0, additionalDataLength).TrimEnd('\0'));
                    case 1: // Success
                        HandleAccepted(buf);
                        break;
                    default:
                        throw new X11Exception("Unexpected response status");
                }
            }

            unsafe void HandleAccepted(byte[] additionalData)
            {
                if (additionalDataLength < SetupResponseDataSize)
                    throw new X11Exception("Too small response");

                Screen[] screens;
                var visualTypes = new Dictionary<uint, VisualType>();

                fixed (byte* p = additionalData)
                {
                    this._setup = *(SetupResponseData*)p;

                    this.ServerVendor = ReadString8(additionalData, SetupResponseDataSize, this._setup.LengthOfVendor);

                    screens = new Screen[this._setup.NumberOfScreens];
                    var screenIndex = 0;

                    var offset = SetupResponseDataSize + this._setup.LengthOfVendor
                        + ComputePad(this._setup.LengthOfVendor) + 8 * this._setup.NumberOfFormats;

                    while (screenIndex < this._setup.NumberOfScreens)
                    {
                        if (offset + SetupScreenDataSize > additionalDataLength)
                            throw new X11Exception("Too many screens");

                        var screen = (SetupScreenData*)&p[offset];
                        screens[screenIndex++] = new Screen(screen->Root, screen->WidthInPixels, screen->HeightInPixels);

                        offset += SetupScreenDataSize;

                        for (var i = 0; i < screen->NumberOfAllowedDepths; i++)
                        {
                            if (offset + SetupDepthDataSize > additionalDataLength)
                                throw new X11Exception("Too many screens");

                            var depth = (SetupDepthData*)&p[offset];
                            offset += SetupDepthDataSize;

                            if (offset + VisualTypeSize * depth->NumberOfVisuals > additionalDataLength)
                                throw new X11Exception("Too many screens");

                            for (var j = 0; j < depth->NumberOfVisuals; j++)
                            {
                                var visual = *(VisualType*)&p[offset];
                                visualTypes.Add(visual.VisualId, visual);
                                offset += VisualTypeSize;
                            }
                        }
                    }
                }

                this.Screens = screens;
                this._visualTypes = visualTypes;
            }
        }

        private async void ReceiveWorker()
        {
            const int eventSize = 32;
            var eventBuffer = new byte[eventSize];

            try
            {
                while (true)
                {
                    await this.ReadExactAsync(eventBuffer, eventSize).ConfigureAwait(false);

                    EventOrReplyHeader header;
                    unsafe
                    {
                        fixed (byte* p = eventBuffer)
                        {
                            header = *(EventOrReplyHeader*)p;
                        }
                    }

                    if (header.EventType == 1) // Reply
                    {
                        using (var rentedArray = ArrayPool.Rent<byte>(header.ReplyLength))
                        {
                            var replyBuffer = rentedArray.Array;

                            if (header.ReplyLength > 0)
                                await this.ReadExactAsync(replyBuffer, header.ReplyLength).ConfigureAwait(false);

                            this._replyActions[header.SequenceNumber](eventBuffer, replyBuffer, null);
                        }
                    }
                    else
                    {
                        // TODO: イベント処理
                    }
                }
            }
            catch (Exception ex)
            {
                foreach (var kvp in this._replyActions)
                    kvp.Value?.Invoke(null, null, ex);

                this._replyActions.Clear();

                // TODO: イベント購読者に例外を流す
            }
        }
    }
}
