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
        private readonly ChannelContext _channelContext;
        private readonly IAsheMagicOnionService _service;

        public GrpcRemoteSearchDirector(string host, int port)
        {
            this._channel = new Channel(host, port, ChannelCredentials.Insecure);

            string hostName = null;
            try
            {
                hostName = Environment.MachineName;
            }
            catch (InvalidOperationException) { }

            var metadata = new Metadata();
            if (!string.IsNullOrEmpty(hostName))
                metadata.Add(GrpcAsheServerContract.HostNameHeader, hostName);

            // ConnectionId を設定する
            this._channelContext = new ChannelContext(this._channel);
            this._service = this._channelContext.CreateClient<IAsheMagicOnionService>(metadata);
        }

        public Task ConnectAsync() => this._channelContext.WaitConnectComplete();

        public override Task<SeekDirectionResult> SeekDirectionAsync() => this._service.SeekDirection().ResponseAsync;

        public override Task ReportResultAsync(Guid jobId, Heroine heroine, IReadOnlyList<int> selectionIds) => this._service.ReportResult(jobId, heroine, selectionIds).ResponseAsync;

        public override Task LogAsync(string message, bool isError, DateTimeOffset timestamp) => this._service.Log(message, isError, timestamp).ResponseAsync;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            this._channelContext.Dispose();
            this._channel.ShutdownAsync().Wait();
        }
    }
}
