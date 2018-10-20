using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using MagicOnion.Client;

namespace WagahighChoices.Ashe
{
    internal class GrpcRemoteSearchDirector : SearchDirector
    {
        private readonly Channel _channel;
        private readonly IAsheMagicOnionService _service;

        public GrpcRemoteSearchDirector(string host, int port)
        {
            this._channel = new Channel(host, port, ChannelCredentials.Insecure);
            this._service = MagicOnionClient.Create<IAsheMagicOnionService>(this._channel);
        }

        public Task ConnectAsync() => this._channel.ConnectAsync();

        public override Task<SeekDirectionResult> SeekDirectionAsync() => this._service.SeekDirection().ResponseAsync;

        public override Task ReportResultAsync(Guid jobId, Heroine heroine, IReadOnlyList<int> selectionIds) => this._service.ReportResult(jobId, heroine, selectionIds).ResponseAsync;

        public override Task LogAsync(string message, bool isError, DateTimeOffset timestamp) => this._service.Log(message, isError, timestamp).ResponseAsync;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            this._channel.ShutdownAsync().Wait();
        }
    }
}
