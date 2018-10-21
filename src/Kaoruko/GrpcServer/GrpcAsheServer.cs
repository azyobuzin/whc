using System;
using Grpc.Core;
using MagicOnion.Server;
using Microsoft.Extensions.DependencyInjection;

namespace WagahighChoices.Kaoruko.GrpcServer
{
    internal class GrpcAsheServer : IDisposable
    {
        private readonly Server _server;

        public GrpcAsheServer(string host, int port, DatabaseActivator databaseActivator)
        {
            var serviceProvider = new ServiceCollection()
                .AddSingleton(databaseActivator)
                .AddScoped(s => s.GetRequiredService<DatabaseActivator>().CreateConnection())
                .BuildServiceProvider();

            var service = MagicOnionEngine.BuildServerServiceDefinition(
                new[] { typeof(AsheMagicOnionService) },
                new MagicOnionOptions(true)
                {
                    GlobalFilters = new MagicOnionFilterAttribute[]
                    {
                        new DependencyInjectionFilterAttribute(serviceProvider),
                    },
                }
            );

            this._server = new Server()
            {
                Services = { service },
                Ports = { new ServerPort(host, port, ServerCredentials.Insecure) }
            };
        }

        public void Start()
        {
            this._server.Start();
        }

        public void Dispose()
        {
            this._server.ShutdownAsync().Wait();
        }
    }
}
