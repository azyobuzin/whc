using Microsoft.Extensions.DependencyInjection;
using WagahighChoices.GrpcUtils;
using WagahighChoices.Toa.Grpc.Internal;

namespace WagahighChoices.Toa.Grpc
{
    public class GrpcToaServer : WhcGrpcServer
    {
        /// <remarks>12/3 は兎亜ちゃんの誕生日です。</remarks>
        public const int DefaultPort = 51203;

        private readonly WagahighOperator _wagahighOperator;

        public GrpcToaServer(string host, int port, WagahighOperator wagahighOperator)
            : base(host, port, typeof(ToaMagicOnionService))
        {
            this._wagahighOperator = wagahighOperator;
        }

        protected override void ConfigureServices(IServiceCollection services)
        {
            // ファクトリで渡して Dispose してもらう
            services.AddSingleton(_ => this._wagahighOperator);
        }
    }
}
