using Microsoft.Extensions.DependencyInjection;
using WagahighChoices.GrpcUtils;

namespace WagahighChoices.Kaoruko.GrpcServer
{
    internal class GrpcAsheServer : WhcGrpcServer
    {
        private readonly DatabaseActivator _databaseActivator;
        private readonly bool _isScreenshotEnabled;

        public GrpcAsheServer(string host, int port, DatabaseActivator databaseActivator, bool isScreenshotEnabled)
            : base(host, port, typeof(AsheMagicOnionService))
        {
            this._databaseActivator = databaseActivator;
            this._isScreenshotEnabled = isScreenshotEnabled;
        }

        protected override void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(this._databaseActivator);
            services.AddScoped(s => s.GetRequiredService<DatabaseActivator>().CreateConnection());
            services.Configure<AsheServerOptions>(options => options.Screenshot = this._isScreenshotEnabled);
        }
    }
}
