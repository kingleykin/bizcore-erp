using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Behaviors;
using Bizcore.BuildingBlocks.Infrastructure;
using Bizcore.BuildingBlocks.MassTransit;
using Bizcore.BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
// using Customer.API.Application.Services;
using Customer.API.Infrastructure.Data;

namespace Customer.API
{
    public class CustomerModule : IServiceModule
    {
        public void RegisterServices(IServiceCollection services, WebApplicationBuilder builder)
        {
            // 1. Database
            var connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
            DatabaseExtensions.PreCreateDatabase(connStr);
            services.AddBizcoreDbContext<CustomerDbContext>(connStr);

            // 2. Business Services
            services.AddScoped<IUnitOfWork, CustomerUnitOfWork>();
            //services.AddScoped<IIdempotencyService, IdempotencyService>();
            //services.AddSingleton<Customer.API.Infrastructure.Telemetry.CustomerMetrics>();

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
            services.AddBizcoreMassTransit<CustomerDbContext>(
                builder.Configuration, 
                QueueNames.CustomerService);
        }
    }
}

