using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Bizcore.BuildingBlocks.Infrastructure
{
    public interface IServiceModule
    {
        void RegisterServices(IServiceCollection services, WebApplicationBuilder builder);
    }

    public static class ModuleExtensions
    {
        public static IServiceCollection AddBizcoreModule<TModule>(this IServiceCollection services, WebApplicationBuilder builder)
            where TModule : IServiceModule, new()
        {
            var module = new TModule();
            module.RegisterServices(services, builder);
            return services;
        }
    }
}
