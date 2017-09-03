using System;
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
                            *(QueryVersionRequest*)p = new QueryVersionRequest()
                            {
                                Header = new ExtensionRequestHeader()
                                {
                                    MajorOpcode = this._majorOpcode.Value,
                                    MinorOpcode = 0,
                                    RequestLength = QueryVersionRequestSize / 4,
                                },
                                ClientMajorVersion = clientMajorVersion,
                                ClientMinorVersion = clientMinorVersion,
                            };
                        }
                    }
                },
                (replyHeader, replyContent) =>
                {
                    unsafe
                    {
                        fixed (byte* pReplyHeader = replyHeader)
                        {
                            var rep = (QueryVersionReply*)pReplyHeader;
                            return new ValueTask<XFixesQueryVersionResult>(
                                new XFixesQueryVersionResult(rep->MajorVersion, rep->MinorVersion));
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
                            *(ExtensionRequestHeader*)p = new ExtensionRequestHeader()
                            {
                                MajorOpcode = this._majorOpcode.Value,
                                MinorOpcode = 4,
                                RequestLength = ExtensionRequestHeaderSize / 4,
                            };
                        }
                    }
                },
                (replyHeader, replyContent) =>
                {
                    unsafe
                    {
                        fixed (byte* pReplyHeader = replyHeader)
                        {
                            var rep = (GetCursorImageReply*)pReplyHeader;

                            var imageLength = rep->Width * rep->Height * 4;
                            var image = new byte[imageLength];
                            Buffer.BlockCopy(replyContent, 0, image, 0, imageLength);

                            return new ValueTask<XFixesGetCursorImageResult>(
                                new XFixesGetCursorImageResult(rep, image));
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
                            *(ExtensionRequestHeader*)p = new ExtensionRequestHeader()
                            {
                                MajorOpcode = this._majorOpcode.Value,
                                MinorOpcode = 25,
                                RequestLength = ExtensionRequestHeaderSize / 4,
                            };
                        }
                    }
                },
                (replyHeader, replyContent) =>
                {
                    unsafe
                    {
                        fixed (byte* pReplyHeader = replyHeader)
                        {
                            var rep = (GetCursorImageAndNameReply*)pReplyHeader;

                            var imageLength = rep->Width * rep->Height * 4;
                            var image = new byte[imageLength];
                            Buffer.BlockCopy(replyContent, 0, image, 0, imageLength);

                            var name = X11Client.ReadString8(replyContent, imageLength, rep->NBytes);

                            return new ValueTask<XFixesGetCursorImageAndNameResult>(
                                new XFixesGetCursorImageAndNameResult(rep, name, image));
                        }
                    }
                }
            ).ConfigureAwait(false);
        }
    }
}
