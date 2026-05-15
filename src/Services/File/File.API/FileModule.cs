using Bizcore.BuildingBlocks.Infrastructure;
using Bizcore.BuildingBlocks.Storage;
using Bizcore.BuildingBlocks.Behaviors;
using MediatR;

namespace File.API;

public class FileModule : IServiceModule
{
    public void RegisterServices(IServiceCollection services, WebApplicationBuilder builder)
    {
        // 1. Database/Storage
        services.AddBizcoreModule<StorageModule>(builder);

        // 2. MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssembly(typeof(FileModule).Assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
        });
    }
}
