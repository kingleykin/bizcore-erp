using Bizcore.BuildingBlocks.Infrastructure;
using Bizcore.BuildingBlocks.MassTransit;
using Bizcore.BuildingBlocks.Messaging;
using Bizcore.BuildingBlocks.Behaviors;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Report.API.Infrastructure.Data;

namespace Report.API;

public class ReportModule : IServiceModule
{
    public void RegisterServices(IServiceCollection services, WebApplicationBuilder builder)
    {
        // 1. Database
        var connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
        DatabaseExtensions.PreCreateDatabase(connStr);
        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connStr));

        // 2. MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssembly(typeof(ReportModule).Assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
        });

        // 3. Infrastructure
        services.AddMemoryCache();

        // 4. MassTransit
        services.AddBizcoreMassTransit<AppDbContext>(
            builder.Configuration,
            QueueNames.ReportService);
    }
}
