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

        private readonly ConcurrentDictionary<ushort, Func<byte[], byte[], Exception, Task>> _replyActions = new ConcurrentDictionary<ushort, Func<byte[], byte[], Exception, Task>>();

        private ushort _sequenceNumber = 1;

        private byte[] _requestBuffer;

        private SetupResponseData _setup;

        public string ServerVendor { get; private set; }

        public IReadOnlyList<Screen> Screens { get; private set; }

        private IReadOnlyDictionary<uint, VisualType> _visualTypes;

        private ConcurrentDictionary<string, uint> _atomCache = new ConcurrentDictionary<string, uint>();

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

        private static void EnsureBufferSize(ref byte[] buffer, int size)
        {
            if (buffer.Length >= size) return;

            var v = (uint)size;
            // http://graphics.stanford.edu/~seander/bithacks.html#RoundUpPowerOf2
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            var newSize = (int)v;
            newSize = newSize < size ? int.MaxValue : newSize;

            buffer = new byte[newSize];
        }

        protected async Task SendRequestAsync(Func<Task> sendAction)
        {
            await this._requestSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                await sendAction().ConfigureAwait(false);
                this._sequenceNumber++;
            }
            finally
            {
                this._requestSemaphore.Release();
            }
        }

        /// <param name="readReply">引数の <c>byte[]</c> は後で再利用するので外部に持ち出さないように</param>
        protected async Task<T> SendRequestAsync<T>(int requestSize, Func<byte[], Task> createRequest, Func<byte[], byte[], ValueTask<T>> readReply)
        {
            var tcs = new TaskCompletionSource<T>();

            async Task action(byte[] replyHeader, byte[] replyContent, Exception excpetion)
            {
                if (excpetion != null)
                {
                    tcs.TrySetException(excpetion);
                    return;
                }

                try
                {
                    tcs.TrySetResult(await readReply(replyHeader, replyContent).ConfigureAwait(false));
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

                EnsureBufferSize(ref this._requestBuffer, requestSize);
                await createRequest(this._requestBuffer).ConfigureAwait(false);
                await this.Stream.WriteAsync(this._requestBuffer, 0, requestSize).ConfigureAwait(false);

                this._sequenceNumber++;
            }
            catch
            {
                this._replyActions.TryRemove(sequenceNumber, out var _);
                throw;
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
            return ReadUtf8String(buffer, index, count);
        }

        private static string ReadString16(byte[] buffer, int index, int count)
        {
            return Encoding.Unicode.GetString(buffer, index, count);
        }

        private static string ReadUtf8String(byte[] buffer, int index, int count)
        {
            return Encoding.UTF8.GetString(buffer, index, count);
        }

        private static int GetByteCountForString8(string s)
        {
            return Encoding.UTF8.GetByteCount(s);
        }

        private static void WriteString8(string s, byte[] buffer, int index)
        {
            Encoding.UTF8.GetBytes(s, 0, s.Length, buffer, index);
        }

        private async Task SetupConnectionAsync()
        {
            SetupResponseHeader responseHeader;

            var buf = new byte[8192]; // Connection Setup のレスポンスがでかい

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

            var additionalDataLength = responseHeader.LengthOfAdditionalData * 4;

            EnsureBufferSize(ref buf, additionalDataLength);
            this._requestBuffer = buf;

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
                    break;
                default:
                    throw new X11Exception("Unexpected response status");
            }

            if (additionalDataLength < SetupResponseDataSize)
                throw new X11Exception("Too small response");

            Screen[] screens;
            var visualTypes = new Dictionary<uint, VisualType>();

            unsafe
            {
                fixed (byte* p = buf)
                {
                    this._setup = *(SetupResponseData*)p;

                    this.ServerVendor = ReadString8(buf, SetupResponseDataSize, this._setup.LengthOfVendor);

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
            }

            this.Screens = screens;
            this._visualTypes = visualTypes;
        }

        private async void ReceiveWorker()
        {
            const int eventSize = 32;
            var eventBuffer = new byte[eventSize];
            var replyBuffer = new byte[256];

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

                    if (header.EventType == 0) // Error
                    {
                        // TODO: Reply なしのリクエストに対するエラーについて考える
                        await callReplyAction(header.SequenceNumber, null, null, new X11Exception(header.ErrorCode.ToString()))
                            .ConfigureAwait(false);
                    }
                    if (header.EventType == 1) // Reply
                    {
                        var replyLength = header.ReplyLength * 4;

                        if (replyLength > 0)
                        {
                            EnsureBufferSize(ref replyBuffer, replyLength);
                            await this.ReadExactAsync(replyBuffer, replyLength).ConfigureAwait(false);
                        }

                        await callReplyAction(header.SequenceNumber, eventBuffer, replyBuffer, null).ConfigureAwait(false);
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

            Task callReplyAction(ushort sequenceNumber, byte[] replyHeader, byte[] replyContent, Exception exception)
            {
                this._replyActions.TryRemove(sequenceNumber, out var replyAction);
                if (replyAction == null) throw new InvalidOperationException("The reply action was not set.");
                return replyAction(replyHeader, replyContent, exception);
            }
        }

        public Task<QueryTreeResult> QueryTreeAsync(uint window)
        {
            return this.SendRequestAsync(
                QueryTreeRequestSize,
                buf =>
                {
                    unsafe
                    {
                        fixed (byte* p = buf)
                        {
                            *(QueryTreeRequest*)p = new QueryTreeRequest()
                            {
                                Opcode = 15,
                                RequestLength = 2,
                                Window = window,
                            };
                        }
                    }

                    return Task.CompletedTask;
                },
                (replyHeader, replyContent) =>
                {
                    unsafe
                    {
                        fixed (byte* pReplyHeader = replyHeader)
                        {
                            var rep = (QueryTreeReply*)pReplyHeader;

                            if (rep->Header.ReplyLength < rep->NumberOfChildren)
                                throw new X11Exception("Too many children");

                            var children = new uint[rep->NumberOfChildren];

                            fixed (byte* pReplyContent = replyContent)
                            {
                                var pChildren = (uint*)pReplyContent;
                                for (var i = 0; i < rep->NumberOfChildren; i++)
                                    children[i] = pChildren[i];
                            }

                            return new ValueTask<QueryTreeResult>(
                                new QueryTreeResult(rep->Root, rep->Parent, children));
                        }
                    }
                }
            );
        }

        public ValueTask<uint> InternAtomAsync(string name, bool onlyIfExists)
        {
            if (this._atomCache.TryGetValue(name, out var atom))
                return new ValueTask<uint>(atom);

            var nameLength = GetByteCountForString8(name);
            var requestLength = InternAtomRequestSize + nameLength + ComputePad(nameLength);

            return new ValueTask<uint>(this.SendRequestAsync(
                requestLength,
                buf =>
                {
                    unsafe
                    {
                        fixed (byte* p = buf)
                        {
                            *(InternAtomRequest*)p = new InternAtomRequest()
                            {
                                Opcode = 16,
                                OnlyIfExists = onlyIfExists,
                                RequestLength = (ushort)(requestLength / 4),
                                LengthOfName = (ushort)nameLength,
                            };
                        }
                    }

                    WriteString8(name, buf, InternAtomRequestSize);

                    return Task.CompletedTask;
                },
                (replyHeader, replyContent) =>
                {
                    unsafe
                    {
                        fixed (byte* pReplyHeader = replyHeader)
                        {
                            var rep = (InternAtomReply*)pReplyHeader;

                            if (rep->Atom != 0)
                                this._atomCache[name] = rep->Atom;

                            return new ValueTask<uint>(rep->Atom);
                        }
                    }
                }
            ));
        }

        public Task<string> GetAtomNameAsync(uint atom)
        {
            return this.SendRequestAsync(
                GetAtomNameRequestSize,
                buf =>
                {
                    unsafe
                    {
                        fixed (byte* p = buf)
                        {
                            *(GetAtomNameRequest*)p = new GetAtomNameRequest()
                            {
                                Opcode = 17,
                                RequestLength = 2,
                                Atom = atom,
                            };
                        }
                    }

                    return Task.CompletedTask;
                },
                (replyHeader, replyContent) =>
                {
                    unsafe
                    {
                        fixed (byte* pReplyHeader = replyHeader)
                        {
                            var rep = (GetAtomNameReply*)pReplyHeader;
                            return new ValueTask<string>(
                                ReadString8(replyContent, 0, rep->LengthOfName));
                        }
                    }
                }
            );
        }

        public async Task<string> GetStringPropertyAsync(uint window, uint property)
        {
            // COMPOUND_STRING は文字コードがまったくわからん
            var utf8TextAtom = await this.InternAtomAsync("UTF8_STRING", false).ConfigureAwait(false);

            const int maxSize = 4096; // maximum-request-length にあわせてこれくらい？

            return await this.SendRequestAsync(
                GetPropertyRequestSize,
                buf =>
                {
                    unsafe
                    {
                        fixed (byte* p = buf)
                        {
                            *(GetPropertyRequest*)p = new GetPropertyRequest()
                            {
                                Opcode = 20,
                                Delete = false,
                                RequestLength = 6,
                                Window = window,
                                Property = property,
                                Type = 0,
                                LongOffset = 0,
                                LongLength = maxSize,
                            };
                        }
                    }

                    return Task.CompletedTask;
                },
                async (replyHeader, replyContent) =>
                {
                    // bytes-after は見ないので maxSize で足りなかったら残念

                    uint type;

                    unsafe
                    {
                        fixed (byte* pReplyHeader = replyHeader)
                        {
                            var rep = (GetPropertyReply*)pReplyHeader;
                            type = rep->Type;

                            if (type == 0) return null;

                            if (type == PredefinedAtoms.STRING)
                            {
                                switch (rep->Format)
                                {
                                    case 8:
                                        return ReadString8(replyContent, 0, (int)rep->LengthOfValue);
                                    case 16:
                                        return ReadString16(replyContent, 0, (int)(rep->LengthOfValue * 2));
                                    default:
                                        throw new X11Exception("STRING" + rep->Format + " is not supported.");
                                }
                            }

                            if (type == utf8TextAtom)
                            {
                                return ReadUtf8String(replyContent, 0, (int)(rep->LengthOfValue * (rep->Format / 8)));
                            }
                        }
                    }

                    string typeName;
                    try
                    {
                        typeName = await this.GetAtomNameAsync(type).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        throw new X11Exception("Unsuppoted type", ex);
                    }
                    throw new X11Exception("Unsupported type '" + typeName + "'");
                }
            ).ConfigureAwait(false);
        }
    }
}
