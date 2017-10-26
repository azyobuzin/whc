using System;
using MagicOnion.Server;
using Grpc.Core;
using WagahighChoices.Toa.Grpc.Internal;

namespace WagahighChoices.Toa.Grpc
{
    public class GrpcToaServer : IDisposable
    {
        /// <remarks>12/3 は兎亜ちゃんの誕生日です。</remarks>
        public const int DefaultPort = 51203;

        private readonly Server _server;
        private readonly WagahighOperator _wagahighOperator;

        public GrpcToaServer(string host, int port, WagahighOperator wagahighOperator)
        {
            var service = MagicOnionEngine.BuildServerServiceDefinition(
                new[] { typeof(ToaMagicOnionService) },
                new MagicOnionOptions()
                {
                    FormatterResolver = ToaFormatterResolver.Instance,
                    GlobalFilters = new MagicOnionFilterAttribute[]
                    {
                        new InjectWagahighOperatorFilterAttribute(wagahighOperator)
                    }
                }
            );

            this._server = new Server()
            {
                Services = { service },
                Ports = { new ServerPort(host, port, ServerCredentials.Insecure) }
            };
            this._wagahighOperator = wagahighOperator;
        }

        public void Start()
        {
            this._server.Start();
        }

        public void Dispose()
        {
            var t = this._server.ShutdownAsync();
            this._wagahighOperator.Dispose();
            t.Wait();
        }
    }
}
