using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
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

        private bool _disposed;

        public string ServerVendor { get; private set; }

        public IReadOnlyList<Screen> Screens { get; private set; }

        private IReadOnlyDictionary<uint, VisualType> _visualTypes;

        private readonly ConcurrentDictionary<string, uint> _atomCache = new ConcurrentDictionary<string, uint>();

        private XFixes _xfixes;
        public XFixes XFixes => this._xfixes ?? (this._xfixes = new XFixes(this));

        private XTest _xtest;
        public XTest XTest => this._xtest ?? (this._xtest = new XTest(this));

        protected X11Client(Stream stream)
        {
            this.Stream = stream;
        }

        public static async Task<X11Client> ConnectAsync(string host, int display)
        {
            var tcpClient = new TcpClient();
            X11Client x11Client;

            try
            {
                await tcpClient.ConnectAsync(host, 6000 + display).ConfigureAwait(false);
                x11Client = new X11Client(tcpClient.GetStream());
                await x11Client.SetupConnectionAsync().ConfigureAwait(false);
            }
            catch
            {
                tcpClient.Dispose();
                throw;
            }

            x11Client.ReceiveWorker();
            return x11Client;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this._disposed) return;
            this._disposed = true;

            if (disposing) this.Stream.Dispose();
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

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

        protected internal async Task SendRequestAsync(int requestSize, Action<byte[]> createRequest)
        {
            await this._requestSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                //if (!this._replyActions.TryAdd(sequenceNumber, action))
                //    throw new InvalidOperationException("Duplicated sequence number");

                EnsureBufferSize(ref this._requestBuffer, requestSize);
                createRequest(this._requestBuffer);
                await this.Stream.WriteAsync(this._requestBuffer, 0, requestSize).ConfigureAwait(false);

                this._sequenceNumber++;
            }
            /*
            catch
            {
                //this._replyActions.TryRemove(sequenceNumber, out var _);
                throw;
            }
            */
            finally
            {
                this._requestSemaphore.Release();
            }
        }

        /// <param name="readReply">引数の <c>byte[]</c> は後で再利用するので外部に持ち出さないように</param>
        protected internal async Task<T> SendRequestAsync<T>(int requestSize, Action<byte[]> createRequest, Func<byte[], byte[], ValueTask<T>> readReply)
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

            ushort sequenceNumber = 0;

            await this._requestSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                sequenceNumber = this._sequenceNumber;

                if (!this._replyActions.TryAdd(sequenceNumber, action))
                    throw new InvalidOperationException("Duplicated sequence number");

                EnsureBufferSize(ref this._requestBuffer, requestSize);
                createRequest(this._requestBuffer);
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

        internal static string ReadString8(byte[] buffer, int index, int count)
        {
            return ReadUtf8String(buffer, index, count);
        }

        internal static string ReadString16(byte[] buffer, int index, int count)
        {
            return Encoding.Unicode.GetString(buffer, index, count);
        }

        internal static string ReadUtf8String(byte[] buffer, int index, int count)
        {
            return Encoding.UTF8.GetString(buffer, index, count);
        }

        internal static int GetByteCountForString8(string s)
        {
            return Encoding.UTF8.GetByteCount(s);
        }

        internal static void WriteString8(string s, byte[] buffer, int index)
        {
            Encoding.UTF8.GetBytes(s, 0, s.Length, buffer, index);
        }

        internal static ValueTask<T> VT<T>(T result) => new ValueTask<T>(result);

        private async Task SetupConnectionAsync()
        {
            SetupResponseHeader responseHeader;

            var buf = new byte[8192]; // Connection Setup のレスポンスがでかい

            unsafe
            {
                fixed (byte* p = buf)
                {
                    var req = (SetupRequestData*)p;
                    req->ByteOrder = BitConverter.IsLittleEndian ? (byte)0x6c : (byte)0x42;
                    req->ProtocolMajorVersion = 11;
                    req->ProtocolMinorVersion = 0;
                    req->LengthOfAuthorizationProtocolName = 0;
                    req->LengthOfAuthorizationProtocolData = 0;
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
                    this.ReadSetupResponse(buf, additionalDataLength);
                    break;
                default:
                    throw new X11Exception("Unexpected response status");
            }
        }

        private void ReadSetupResponse(byte[] buf, int additionalDataLength)
        {
            if (additionalDataLength < SetupResponseDataSize)
                throw new X11Exception("Too small response");

            Screen[] screens;
            var visualTypes = new Dictionary<uint, VisualType>();

            unsafe
            {
                fixed (byte* p = buf)
                {
                    ref var setupRes = ref Unsafe.AsRef<SetupResponseData>(p);

                    this.ServerVendor = ReadString8(buf, SetupResponseDataSize, setupRes.LengthOfVendor);

                    screens = new Screen[setupRes.NumberOfScreens];
                    var screenIndex = 0;

                    var offset = SetupResponseDataSize + setupRes.LengthOfVendor
                        + ComputePad(setupRes.LengthOfVendor) + 8 * setupRes.NumberOfFormats;

                    while (screenIndex < setupRes.NumberOfScreens)
                    {
                        if (offset + SetupScreenDataSize > additionalDataLength)
                            throw new X11Exception("Too many screens");

                        ref var screen = ref Unsafe.AsRef<SetupScreenData>(&p[offset]);
                        screens[screenIndex++] = new Screen(ref screen);

                        offset += SetupScreenDataSize;

                        for (var i = 0; i < screen.NumberOfAllowedDepths; i++)
                        {
                            if (offset + SetupDepthDataSize > additionalDataLength)
                                throw new X11Exception("Too many screens");

                            ref var depth = ref Unsafe.AsRef<SetupDepthData>(&p[offset]);
                            offset += SetupDepthDataSize;

                            if (offset + VisualTypeSize * depth.NumberOfVisuals > additionalDataLength)
                                throw new X11Exception("Too many screens");

                            for (var j = 0; j < depth.NumberOfVisuals; j++)
                            {
                                ref var visual = ref Unsafe.AsRef<VisualType>(&p[offset]);
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

                    Debug.WriteLine("Received " + header.EventType);

                    if (header.EventType == 0) // Error
                    {
                        // TODO: Reply なしのリクエストに対するエラーについて考える
                        await callReplyAction(header.SequenceNumber, null, null, new X11Exception(header.ErrorCode.ToString()))
                            .ConfigureAwait(false);
                    }
                    else if (header.EventType == 1) // Reply
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
                if (!this._disposed)
                {
                    foreach (var kvp in this._replyActions)
                        kvp.Value?.Invoke(null, null, ex);

                    this._replyActions.Clear();

                    // TODO: イベント購読者に例外を流す
                }
            }

            Task callReplyAction(ushort sequenceNumber, byte[] replyHeader, byte[] replyContent, Exception exception)
            {
                this._replyActions.TryRemove(sequenceNumber, out var replyAction);
                if (replyAction == null) throw new InvalidOperationException("The reply action was not set.");
                return replyAction(replyHeader, replyContent, exception);
            }
        }

        public Task ConfigureWindowAsync(uint window, short? x = null, short? y = null, ushort? width = null, ushort? height = null, ushort? borderWidth = null, uint? sibling = null, StackMode? stackMode = null)
        {
            ushort valueMask = 0;
            var valueLength = 0;
            if (x.HasValue)
            {
                valueMask |= 0x0001;
                valueLength += 4;
            }
            if (y.HasValue)
            {
                valueMask |= 0x0002;
                valueLength += 4;
            }
            if (width.HasValue)
            {
                valueMask |= 0x0004;
                valueLength += 4;
            }
            if (height.HasValue)
            {
                valueMask |= 0x0008;
                valueLength += 4;
            }
            if (borderWidth.HasValue)
            {
                valueMask |= 0x0010;
                valueLength += 4;
            }
            if (sibling.HasValue)
            {
                valueMask |= 0x0020;
                valueLength += 4;
            }
            if (stackMode.HasValue)
            {
                valueMask |= 0x0040;
                valueLength += 4;
            }
            var requestLength = ConfigureWindowRequestSize + valueLength;

            return this.SendRequestAsync(
                requestLength,
                buf =>
                {
                    unsafe
                    {
                        fixed (byte* p = buf)
                        {
                            ref var req = ref Unsafe.AsRef<ConfigureWindowRequest>(p);
                            req = default;
                            req.Opcode = 12;
                            req.RequestLength = (ushort)(requestLength / 4);
                            req.Window = window;
                            req.ValueMask = valueMask;

                            var pv = &p[ConfigureWindowRequestSize];
                            if (x.HasValue)
                            {
                                *(short*)pv = x.Value;
                                pv += 4;
                            }
                            if (y.HasValue)
                            {
                                *(short*)pv = y.Value;
                                pv += 4;
                            }
                            if (width.HasValue)
                            {
                                *(ushort*)pv = width.Value;
                                pv += 4;
                            }
                            if (height.HasValue)
                            {
                                *(ushort*)pv = height.Value;
                                pv += 4;
                            }
                            if (borderWidth.HasValue)
                            {
                                *(ushort*)pv = borderWidth.Value;
                                pv += 4;
                            }
                            if (sibling.HasValue)
                            {
                                *(uint*)pv = sibling.Value;
                                pv += 4;
                            }
                            if (stackMode.HasValue)
                            {
                                *pv = (byte)stackMode.Value;
                            }
                        }
                    }
                }
            );
        }

        public Task<GetGeometryResult> GetGeometryAsync(uint drawable)
        {
            return this.SendRequestAsync(
                GetGeometryRequestSize,
                buf =>
                {
                    unsafe
                    {
                        fixed (byte* p = buf)
                        {
                            ref var req = ref Unsafe.AsRef<GetGeometryRequest>(p);
                            req = default;
                            req.Opcode = 14;
                            req.RequestLength = 2;
                            req.Drawable = drawable;
                        }
                    }
                },
                (replyHeader, replyContent) =>
                {
                    unsafe
                    {
                        fixed (byte* pReplyHeader = replyHeader)
                        {
                            ref var rep = ref Unsafe.AsRef<GetGeometryReply>(pReplyHeader);
                            return VT(new GetGeometryResult(ref rep));
                        }
                    }
                }
            );
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
                            ref var req = ref Unsafe.AsRef<QueryTreeRequest>(p);
                            req = default;
                            req.Opcode = 15;
                            req.RequestLength = 2;
                            req.Window = window;
                        }
                    }
                },
                (replyHeader, replyContent) =>
                {
                    unsafe
                    {
                        fixed (byte* pReplyHeader = replyHeader)
                        {
                            ref var rep = ref Unsafe.AsRef<QueryTreeReply>(pReplyHeader);

                            if (rep.Header.ReplyLength < rep.NumberOfChildren)
                                throw new X11Exception("Too many children");

                            var children = new uint[rep.NumberOfChildren];

                            fixed (byte* pReplyContent = replyContent)
                            {
                                var pChildren = (uint*)pReplyContent;
                                for (var i = 0; i < rep.NumberOfChildren; i++)
                                    children[i] = pChildren[i];
                            }

                            return VT(new QueryTreeResult(rep.Root, rep.Parent, children));
                        }
                    }
                }
            );
        }

        public ValueTask<uint> InternAtomAsync(string name, bool onlyIfExists)
        {
            if (this._atomCache.TryGetValue(name, out var atom))
                return VT(atom);

            var nameLength = GetByteCountForString8(name);
            var requestLength = InternAtomRequestSize + nameLength + ComputePad(nameLength);

            return this.SendRequestAsync(
                requestLength,
                buf =>
                {
                    unsafe
                    {
                        fixed (byte* p = buf)
                        {
                            ref var req = ref Unsafe.AsRef<InternAtomRequest>(p);
                            req = default;
                            req.Opcode = 16;
                            req.OnlyIfExists = onlyIfExists;
                            req.RequestLength = (ushort)(requestLength / 4);
                            req.LengthOfName = (ushort)nameLength;
                        }
                    }

                    WriteString8(name, buf, InternAtomRequestSize);
                },
                (replyHeader, replyContent) =>
                {
                    unsafe
                    {
                        fixed (byte* pReplyHeader = replyHeader)
                        {
                            ref var rep = ref Unsafe.AsRef<InternAtomReply>(pReplyHeader);

                            if (rep.Atom != 0)
                                this._atomCache[name] = rep.Atom;

                            return VT(rep.Atom);
                        }
                    }
                }
            ).ToValueTask();
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
                            ref var req = ref Unsafe.AsRef<GetAtomNameRequest>(p);
                            req = default;
                            req.Opcode = 17;
                            req.RequestLength = 2;
                            req.Atom = atom;
                        }
                    }
                },
                (replyHeader, replyContent) =>
                {
                    unsafe
                    {
                        fixed (byte* pReplyHeader = replyHeader)
                        {
                            ref var rep = ref Unsafe.AsRef<GetAtomNameReply>(pReplyHeader);
                            return VT(ReadString8(replyContent, 0, rep.LengthOfName));
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
                            ref var req = ref Unsafe.AsRef<GetPropertyRequest>(p);
                            req = default;
                            req.Opcode = 20;
                            req.Delete = false;
                            req.RequestLength = 6;
                            req.Window = window;
                            req.Property = property;
                            req.Type = 0;
                            req.LongOffset = 0;
                            req.LongLength = maxSize;
                        }
                    }
                },
                (replyHeader, replyContent) =>
                {
                    // bytes-after は見ないので maxSize で足りなかったら残念

                    uint type;

                    unsafe
                    {
                        fixed (byte* pReplyHeader = replyHeader)
                        {
                            ref var rep = ref Unsafe.AsRef<GetPropertyReply>(pReplyHeader);
                            type = rep.Type;

                            if (type == 0) return VT(default(string));

                            if (type == PredefinedAtoms.STRING)
                            {
                                switch (rep.Format)
                                {
                                    case 8:
                                        return VT(ReadString8(replyContent, 0, (int)rep.LengthOfValue));
                                    case 16:
                                        return VT(ReadString16(replyContent, 0, (int)(rep.LengthOfValue * 2)));
                                    default:
                                        throw new X11Exception("STRING" + rep.Format + " is not supported.");
                                }
                            }

                            if (type == utf8TextAtom)
                            {
                                return VT(ReadUtf8String(replyContent, 0, (int)(rep.LengthOfValue * (rep.Format / 8))));
                            }
                        }
                    }

                    return this.GetAtomNameAsync(type)
                        .ContinueWith<string>(
                            t =>
                            {
                                if (t.IsFaulted) throw new X11Exception("Unsuppoted type", t.Exception);
                                throw new X11Exception("Unsupported type '" + t.Result + "'");
                            },
                            CancellationToken.None,
                            TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.NotOnCanceled,
                            TaskScheduler.Default
                        )
                        .ToValueTask();
                }
            ).ConfigureAwait(false);
        }

        public Task<TranslateCoordinatesResult> TranslateCoordinatesAsync(uint srcWindow, uint dstWindow, short srcX, short srcY)
        {
            return this.SendRequestAsync(
                TranslateCoordinatesRequestSize,
                buf =>
                {
                    unsafe
                    {
                        fixed (byte* p = buf)
                        {
                            ref var req = ref Unsafe.AsRef<TranslateCoordinatesRequest>(p);
                            req = default;
                            req.Opcode = 40;
                            req.RequestLength = 4;
                            req.SrcWindow = srcWindow;
                            req.DstWindow = dstWindow;
                            req.SrcX = srcX;
                            req.SrcY = srcY;
                        }
                    }
                },
                (replyHeader, replyContent) =>
                {
                    unsafe
                    {
                        fixed (byte* pReplyHeader = replyHeader)
                        {
                            ref var rep = ref Unsafe.AsRef<TranslateCoordinatesReply>(pReplyHeader);
                            return VT(new TranslateCoordinatesResult(ref rep));
                        }
                    }
                }
            );
        }

        public Task<GetImageResult> GetImageAsync(uint drawable, short x, short y, ushort width, ushort height, uint planeMask, GetImageFormat format)
        {
            return this.SendRequestAsync(
                GetImageRequestSize,
                buf =>
                {
                    unsafe
                    {
                        fixed (byte* p = buf)
                        {
                            ref var req = ref Unsafe.AsRef<GetImageRequest>(p);
                            req = default;
                            req.Opcode = 73;
                            req.Format = format;
                            req.RequestLength = 5;
                            req.Drawable = drawable;
                            req.X = x;
                            req.Y = y;
                            req.Width = width;
                            req.Height = height;
                            req.PlaneMask = planeMask;
                        }
                    }
                },
                (replyHeader, replyContent) =>
                {
                    unsafe
                    {
                        fixed (byte* pReplyHeader = replyHeader)
                        {
                            ref var rep = ref Unsafe.AsRef<GetImageReply>(pReplyHeader);

                            var data = new byte[rep.ReplyLength * 4];
                            Buffer.BlockCopy(replyContent, 0, data, 0, data.Length);

                            return VT(new GetImageResult(rep.Depth, this._visualTypes[rep.Visual], data));
                        }
                    }
                }
            );
        }

        public Task<QueryExtensionResult> QueryExtensionAsync(string name)
        {
            var lengthOfName = GetByteCountForString8(name);
            var requestLength = QueryExtensionRequestSize + lengthOfName + ComputePad(lengthOfName);

            return this.SendRequestAsync(
                requestLength,
                buf =>
                {
                    unsafe
                    {
                        fixed (byte* p = buf)
                        {
                            ref var req = ref Unsafe.AsRef<QueryExtensionRequest>(p);
                            req = default;
                            req.Opcode = 98;
                            req.RequestLength = (ushort)(requestLength / 4);
                            req.LengthOfName = (ushort)lengthOfName;
                        }
                    }

                    WriteString8(name, buf, QueryExtensionRequestSize);
                },
                (replyHeader, replyContent) =>
                {
                    unsafe
                    {
                        fixed (byte* pReplyHeader = replyHeader)
                        {
                            ref var rep = ref Unsafe.AsRef<QueryExtensionReply>(pReplyHeader);
                            return VT(rep.Present ? new QueryExtensionResult(ref rep) : null);
                        }
                    }
                }
            );
        }
    }
}
