using Microsoft.Extensions.DependencyInjection;
using WagahighChoices.GrpcUtils;

namespace WagahighChoices.Kaoruko.GrpcServer
{
    internal class GrpcAsheServer : WhcGrpcServer
    {
        private readonly DatabaseActivator _databaseActivator;

        public GrpcAsheServer(string host, int port, DatabaseActivator databaseActivator)
            : base(host, port, typeof(AsheMagicOnionService))
        {
            this._databaseActivator = databaseActivator;
        }

        protected override void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(this._databaseActivator);
            services.AddScoped(s => s.GetRequiredService<DatabaseActivator>().CreateConnection());
        }
    }
}
