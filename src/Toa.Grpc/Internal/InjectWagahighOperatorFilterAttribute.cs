using System;
using System.Threading.Tasks;
using MagicOnion.Server;

namespace WagahighChoices.Toa.Grpc.Internal
{
    public class InjectWagahighOperatorFilterAttribute : MagicOnionFilterAttribute
    {
        public const string ItemKey = "WagahighOperator";

        public WagahighOperator WagahighOperator { get; }

        public InjectWagahighOperatorFilterAttribute(WagahighOperator wagahighOperator, Func<ServiceContext, Task> next)
            : base(next)
        {
            this.WagahighOperator = wagahighOperator;
        }

        public override Task Invoke(ServiceContext context)
        {
            context.Items[ItemKey] = this.WagahighOperator;
            return this.Next(context);
        }
    }
}
