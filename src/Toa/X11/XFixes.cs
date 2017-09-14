using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static WagahighChoices.Toa.X11.X11Client;

namespace WagahighChoices.Toa.X11
{
    public partial class XFixes
    {
        public const string ExtensionName = "XFIXES";
        public const uint MajorVersion = 5;
        public const uint MinorVersion = 0;

        private readonly X11Client _client;
        private byte? _majorOpcode;
        private Task _negotiationTask;

        public XFixes(X11Client client)
        {
            this._client = client;
        }

        private async Task EnsureMajorOpcodeAsync()
        {
            if (this._majorOpcode.HasValue) return;

            var queryResult = await this._client.QueryExtensionAsync(ExtensionName).ConfigureAwait(false);

            if (queryResult == null)
                throw new X11Exception("The server does not support XFIXES.");

            this._majorOpcode = queryResult.MajorOpcode;
        }

        private Task EnsureNegotiated()
        {
            if (this._negotiationTask == null)
            {
                this._negotiationTask = this.QueryVersionAsync(MajorVersion, MinorVersion);
            }

            return this._negotiationTask;
        }

        public async Task<XFixesQueryVersionResult> QueryVersionAsync(uint clientMajorVersion, uint clientMinorVersion)
        {
            await this.EnsureMajorOpcodeAsync().ConfigureAwait(false);

            return await this._client.SendRequestAsync(
                QueryVersionRequestSize,
                buf =>
                {
                    unsafe
                    {
                        fixed (byte* p = buf)
                        {
                            ref var req = ref Unsafe.AsRef<QueryVersionRequest>(p);
                            req = default;
                            req.Header.MajorOpcode = this._majorOpcode.Value;
                            req.Header.MinorOpcode = 0;
                            req.Header.RequestLength = QueryVersionRequestSize / 4;
                            req.ClientMajorVersion = clientMajorVersion;
                            req.ClientMinorVersion = clientMinorVersion;
                        }
                    }
                },
                (replyHeader, replyContent) =>
                {
                    unsafe
                    {
                        fixed (byte* pReplyHeader = replyHeader)
                        {
                            ref var rep = ref Unsafe.AsRef<QueryVersionReply>(pReplyHeader);
                            return VT(new XFixesQueryVersionResult(ref rep));
                        }
                    }
                }
            ).ConfigureAwait(false);
        }

        public async Task<XFixesGetCursorImageResult> GetCursorImageAsync()
        {
            await this.EnsureNegotiated().ConfigureAwait(false);

            return await this._client.SendRequestAsync(
                ExtensionRequestHeaderSize,
                buf =>
                {
                    unsafe
                    {
                        fixed (byte* p = buf)
                        {
                            ref var req = ref Unsafe.AsRef<ExtensionRequestHeader>(p);
                            req = default;
                            req.MajorOpcode = this._majorOpcode.Value;
                            req.MinorOpcode = 4;
                            req.RequestLength = ExtensionRequestHeaderSize / 4;
                        }
                    }
                },
                (replyHeader, replyContent) =>
                {
                    unsafe
                    {
                        fixed (byte* pReplyHeader = replyHeader)
                        {
                            ref var rep = ref Unsafe.AsRef<GetCursorImageReply>(pReplyHeader);

                            return VT(new XFixesGetCursorImageResult(ref rep,
                                new ReadOnlySpan<byte>(replyContent, 0, rep.Width * rep.Height * 4)));
                        }
                    }
                }
            ).ConfigureAwait(false);
        }

        public async Task<XFixesGetCursorImageAndNameResult> GetCursorImageAndNameAsync()
        {
            await this.EnsureNegotiated().ConfigureAwait(false);

            return await this._client.SendRequestAsync(
                ExtensionRequestHeaderSize,
                buf =>
                {
                    unsafe
                    {
                        fixed (byte* p = buf)
                        {
                            ref var req = ref Unsafe.AsRef<ExtensionRequestHeader>(p);
                            req = default;
                            req.MajorOpcode = this._majorOpcode.Value;
                            req.MinorOpcode = 25;
                            req.RequestLength = ExtensionRequestHeaderSize / 4;
                        }
                    }
                },
                (replyHeader, replyContent) =>
                {
                    unsafe
                    {
                        fixed (byte* pReplyHeader = replyHeader)
                        {
                            ref var rep = ref Unsafe.AsRef<GetCursorImageAndNameReply>(pReplyHeader);

                            var imageLength = rep.Width * rep.Height * 4;
                            var name = ReadString8(replyContent, imageLength, rep.NBytes);

                            return VT(new XFixesGetCursorImageAndNameResult(ref rep, name, new ReadOnlySpan<byte>(replyContent, 0, imageLength)));
                        }
                    }
                }
            ).ConfigureAwait(false);
        }
    }
}
