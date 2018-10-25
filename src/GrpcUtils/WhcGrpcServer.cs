using System;
using Grpc.Core;
using MagicOnion.Server;
using Microsoft.Extensions.DependencyInjection;

namespace WagahighChoices.GrpcUtils
{
    public class WhcGrpcServer : IDisposable
    {
        private readonly Server _server;
        private readonly Type _serviceType;
        private bool _initialized;
        private IServiceProvider _serviceProvider;

        public WhcGrpcServer(string host, int port, Type serviceType)
        {
            this._server = new Server()
            {
                Ports = { new ServerPort(host, port, ServerCredentials.Insecure) }
            };
            this._serviceType = serviceType;
        }

        protected virtual void ConfigureServices(IServiceCollection services) { }

        public void Start()
        {
            if (!this._initialized)
            {
                this._initialized = true;

                // IServiceProvider の準備
                var serviceCollection = new ServiceCollection();
                this.ConfigureServices(serviceCollection);
                this._serviceProvider = serviceCollection.BuildServiceProvider();

                var service = MagicOnionEngine.BuildServerServiceDefinition(
                    new[] { this._serviceType },
                    new MagicOnionOptions(true)
                    {
                        FormatterResolver = WhcFormatterResolver.Instance,
                        GlobalFilters = new MagicOnionFilterAttribute[]
                        {
                            new DependencyInjectionFilterAttribute(this._serviceProvider),
                        },
                    }
                );

                this._server.Services.Add(service);
            }

            this._server.Start();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this._server.ShutdownAsync().Wait();
                (this._serviceProvider as IDisposable)?.Dispose();
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
