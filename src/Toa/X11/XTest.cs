using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace WagahighChoices.Toa.X11
{
    public partial class XTest
    {
        public const string ExtensionName = "XTEST";
        public const uint MajorVersion = 2;
        public const uint MinorVersion = 1;

        private readonly X11Client _client;
        private byte? _majorOpcode;

        public XTest(X11Client client)
        {
            this._client = client;
        }

        private async Task EnsureMajorOpcodeAsync()
        {
            if (this._majorOpcode.HasValue) return;

            var queryResult = await this._client.QueryExtensionAsync(ExtensionName).ConfigureAwait(false);

            if (queryResult == null)
                throw new X11Exception("The server does not support XTEST.");

            this._majorOpcode = queryResult.MajorOpcode;
        }

        public async Task FakeInputAsync(XTestFakeEventType type, byte detail, uint time, uint root, short rootX, short rootY)
        {
            await this.EnsureMajorOpcodeAsync().ConfigureAwait(false);

            await this._client.SendRequestAsync(
                FakeInputRequestSize,
                buf =>
                {
                    ref var req = ref Unsafe.As<byte, FakeInputRequest>(ref buf[0]);
                    req = default;
                    req.Header.MajorOpcode = this._majorOpcode.Value;
                    req.Header.MinorOpcode = 2;
                    req.Header.RequestLength = 9;
                    req.Type = type;
                    req.Detail = detail;
                    req.Time = time;
                    req.Root = root;
                    req.RootX = rootX;
                    req.RootY = rootY;
                }
            ).ConfigureAwait(false);
        }
    }
}
