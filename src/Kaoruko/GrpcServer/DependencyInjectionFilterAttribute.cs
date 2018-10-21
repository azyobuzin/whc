using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MagicOnion.Server;
using Microsoft.Extensions.DependencyInjection;

namespace WagahighChoices.Kaoruko.GrpcServer
{
    // （これ Toa.Grpc でも使えばよかったな）
    internal class DependencyInjectionFilterAttribute : MagicOnionFilterAttribute
    {
        public const string ItemKey = "DependencyInjectionFilterAttribute.ServiceProvider";

        public IServiceProvider ServiceProvider { get; }

        public DependencyInjectionFilterAttribute(IServiceProvider serviceProvider)
            : base(null)
        {
            this.ServiceProvider = serviceProvider;
        }

        public DependencyInjectionFilterAttribute(Func<ServiceContext, Task> next)
            : base(next)
        { }

        public override async Task Invoke(ServiceContext context)
        {
            using (var scope = this.ServiceProvider.CreateScope())
            {
                context.Items[ItemKey] = scope;
                await this.Next(context).ConfigureAwait(false);
                context.Items.TryRemove(ItemKey, out _);
            }
        }
    }

    internal static class DependencyInjectionServiceContextExtensions
    {
        public static IServiceProvider GetServiceProvider(this ServiceContext context)
        {
            return context.Items.GetValueOrDefault(DependencyInjectionFilterAttribute.ItemKey) as IServiceProvider;
        }

        public static T GetService<T>(this ServiceContext context)
        {
            return context.GetServiceProvider() is IServiceProvider serviceProvider
                ? serviceProvider.GetService<T>()
                : default;
        }

        public static T GetRequiredService<T>(this ServiceContext context)
        {
            var serviceProvider = context.GetServiceProvider() ?? throw new InvalidOperationException();
            return serviceProvider.GetRequiredService<T>();
        }
    }
}
