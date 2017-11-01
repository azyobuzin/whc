using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
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

            this.LogStream = Observable.Defer(() => this._service.LogStream().ToObservable())
                .SelectMany(x => ((IAsyncEnumerable<string>)x.ResponseStream)
                    .ToObservable().Finally(() => x.Dispose())
                );
        }

        public Task ConnectAsync() => this._channel.ConnectAsync();

        public override Task<Argb32Image> CaptureContentAsync() => this._service.CaptureContent().ResponseAsync;

        public override Task SetCursorPositionAsync(short x, short y) => this._service.SetCursorPosition(x, y).ResponseAsync;

        public override Task MouseClickAsync() => this._service.MouseClick().ResponseAsync;

        public override Task<Argb32Image> GetCursorImageAsync() => this._service.GetCursorImage().ResponseAsync;

        public override IObservable<string> LogStream { get; }

        protected override void Dispose(bool disposing)
        {
            // TODO: もっといい手段ないの
            this._channel.ShutdownAsync().Wait();
        }
    }
}
