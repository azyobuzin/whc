using System.Threading.Tasks;
using Grpc.Core;
using MagicOnion.Client;
using WagahighChoices.Toa.Grpc.Internal;

namespace WagahighChoices.Toa.Grpc
{
    public class GrpcRemoteWagahighOperator : WagahighOperator
    {
        private readonly Channel _channel;
        private readonly IToaMagicOnionService _service;

        public GrpcRemoteWagahighOperator(string host, int port)
        {
            this._channel = new Channel(host, port, ChannelCredentials.Insecure);
            this._service = MagicOnionClient.Create<IToaMagicOnionService>(this._channel, ToaFormatterResolver.Instance);
        }

        public override Task<Argb32Image> CaptureContentAsync() => this._service.CaptureContent().ResponseAsync;

        public override Task SetCursorPositionAsync(short x, short y) => this._service.SetCursorPosition(x, y).ResponseAsync;

        public override Task MouseClickAsync() => this._service.MouseClick().ResponseAsync;

        public override Task<Argb32Image> GetCursorImageAsync() => this._service.GetCursorImage().ResponseAsync;

        protected override void Dispose(bool disposing)
        {
            // TODO: もっといい手段ないの
            this._channel.ShutdownAsync().Wait();
        }
    }
}
