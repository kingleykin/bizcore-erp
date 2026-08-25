using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Behaviors;
using Bizcore.BuildingBlocks.Infrastructure;
using Bizcore.BuildingBlocks.MassTransit;
using Bizcore.BuildingBlocks.Messaging;
using Customer.API.Infrastructure.Data;
using FluentValidation;
using FluentValidation.AspNetCore;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Customer.API
{
    public class CustomerModule : IServiceModule
    {
        public void RegisterServices(IServiceCollection services, WebApplicationBuilder builder)
        {
            // 1. Database
            var connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
            DatabaseExtensions.PreCreateDatabase(connStr);
            services.AddBizcoreDbContext<AppDbContext>(connStr);

            // 2. Application Services
            services.AddScoped<IUnitOfWork, CustomerUnitOfWork>();

            // 3. MediatR with Transaction Pipeline
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
                cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
            });

            // 4. Validation & Controllers
            // Exception handling is centralized via GlobalExceptionMiddleware (Bizcore.BuildingBlocks.Middlewares),
            // registered globally in UseBizcorePipeline — no per-service exception filter needed.
            services.AddControllers();
            services.AddFluentValidationAutoValidation();
            services.AddValidatorsFromAssemblyContaining<Customer.API.Application.Validators.CreateCustomerRequestValidator>();

            // 5. MassTransit
            services.AddBizcoreMassTransit<AppDbContext>(
                builder.Configuration,
                QueueNames.CustomerService);
        }
    }
}
