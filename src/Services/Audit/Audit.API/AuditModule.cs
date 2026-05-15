using Audit.API.Application.Jobs;
using Audit.API.Infrastructure.Data;
using Audit.API.Infrastructure.Services;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Infrastructure;
using Bizcore.BuildingBlocks.MassTransit;
using Bizcore.BuildingBlocks.Messaging;
using Bizcore.BuildingBlocks.Behaviors;
using Bizcore.BuildingBlocks.Grpc;
using MediatR;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace Audit.API;

public class AuditModule : IServiceModule
{
    public void RegisterServices(IServiceCollection services, WebApplicationBuilder builder)
    {
        // 1. Database
        var connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
        DatabaseExtensions.PreCreateDatabase(connStr);
        services.AddDbContext<AuditDbContext>(options => options.UseSqlServer(connStr));

        // 2. MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssembly(typeof(AuditModule).Assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
        });

        // 3. Application Services
        services.AddScoped<HashChainService>();
        services.AddScoped<IUnitOfWork, AuditUnitOfWork>();
        services.AddScoped<RetentionCleanupJob>();
        services.AddScoped<IntegrityVerificationJob>();

        // 4. gRPC
        services.AddBizcoreGrpcServer();
        services.AddGrpcReflection();

        // 5. Hangfire
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connStr, new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true
            }));
        services.AddHangfireServer(options => { options.WorkerCount = 2; options.Queues = new[] { "default" }; });

        // 6. MassTransit
        services.AddBizcoreMassTransit<AuditDbContext>(
            builder.Configuration,
            QueueNames.AuditService);
    }
}
