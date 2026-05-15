using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Behaviors;
using Bizcore.BuildingBlocks.Infrastructure;
using Bizcore.BuildingBlocks.MassTransit;
using Bizcore.BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Payment.API.Application.Services;
using Payment.API.Infrastructure.Data;

namespace Payment.API
{
    public class PaymentModule : IServiceModule
    {
        public void RegisterServices(IServiceCollection services, WebApplicationBuilder builder)
        {
            // 1. Database
            var connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
            DatabaseExtensions.PreCreateDatabase(connStr);
            services.AddBizcoreDbContext<AppDbContext>(connStr);

            // 2. Business Services
            services.AddScoped<IUnitOfWork, PaymentUnitOfWork>();
            services.AddScoped<IIdempotencyService, IdempotencyService>();
            services.AddSingleton<Payment.API.Infrastructure.Telemetry.PaymentMetrics>();

            // 3. MediatR with Transaction Pipeline
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
                cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
            });

            // 4. SignalR for Real-time Status Updates
            services.AddSignalR();

            // 5. MassTransit (Using the new convention-based helper)
            services.AddBizcoreMassTransit<AppDbContext>(
                builder.Configuration, 
                QueueNames.PaymentService);
        }
    }
}

