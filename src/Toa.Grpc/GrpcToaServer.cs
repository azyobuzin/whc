using System;
using MagicOnion.Server;
using Grpc.Core;
using WagahighChoices.Toa.Grpc.Internal;

namespace WagahighChoices.Toa.Grpc
{
    public class GrpcToaServer : IDisposable
    {
        private static readonly ServerServiceDefinition _service = MagicOnionEngine.BuildServerServiceDefinition(
            new[] { typeof(ToaMagicOnionService) },
            new MagicOnionOptions() { FormatterResolver = ToaFormatterResolver.Instance }
        );

        private readonly Server _server;
        private readonly WagahighOperator _wagahighOperator;

        public GrpcToaServer(string host, int port, WagahighOperator wagahighOperator)
        {
            // TODO: InjectWagahighOperatorFilterAttribute
            this._server = new Server()
            {
                Services = { _service },
                Ports = { new ServerPort(host, port, ServerCredentials.Insecure) }
            };
            this._wagahighOperator = wagahighOperator;
        }

        public void Dispose()
        {
            var t = this._server.ShutdownAsync();
            this._wagahighOperator.Dispose();
            t.Wait();
        }
    }
}
