using System;
using System.Collections.Generic;
using System.Reactive.Linq;
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

            this.LogStream = Observable.Create<string>(async (observer, cancellationToken) =>
            {
                using (var result = await this._service.LogStream().ConfigureAwait(false))
                using (cancellationToken.Register(result.Dispose))
                {
                    var stream = result.ResponseStream;

                    // MoveNext に CancellationToken を指定するのは対応していない
                    while (true)
                    {
                        if (cancellationToken.IsCancellationRequested) return;
                        if (!await stream.MoveNext().ConfigureAwait(false)) break;
                        observer.OnNext(stream.Current);
                    }
                }

                observer.OnCompleted();
            });
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
