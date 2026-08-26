using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Behaviors;
using Bizcore.BuildingBlocks.Infrastructure;
using Bizcore.BuildingBlocks.MassTransit;
using Bizcore.BuildingBlocks.Messaging;
using FluentValidation;
using FluentValidation.AspNetCore;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Inventory.API.Infrastructure.Data;

namespace Inventory.API
{
    public class InventoryModule : IServiceModule
    {
        public void RegisterServices(IServiceCollection services, WebApplicationBuilder builder)
        {
            // 1. Database
            var connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
            DatabaseExtensions.PreCreateDatabase(connStr);
            services.AddBizcoreDbContext<AppDbContext>(connStr);

            // 2. Application Services
            services.AddScoped<IUnitOfWork, InventoryUnitOfWork>();

            // 3. MediatR with Transaction Pipeline
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
                cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
            });

            // 4. Validation & Controllers
            services.AddControllers();
            services.AddFluentValidationAutoValidation();
            services.AddValidatorsFromAssemblyContaining<Inventory.API.Application.Validators.AdjustStockRequestValidator>();

            // 5. MassTransit — tự động phát hiện OrderCreated/Confirmed/CancelledConsumer trong assembly này.
            services.AddBizcoreMassTransit<AppDbContext>(
                builder.Configuration,
                QueueNames.InventoryService);
        }
    }
}
