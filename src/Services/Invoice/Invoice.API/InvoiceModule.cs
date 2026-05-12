using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Behaviors;
using Bizcore.BuildingBlocks.Infrastructure;
using Bizcore.BuildingBlocks.MassTransit;
using FluentValidation;
using FluentValidation.AspNetCore;
using Invoice.API.Application.Clients;
using Invoice.API.Application.Services;
using Invoice.API.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Invoice.API
{
    public class InvoiceModule : IServiceModule
    {
        public void RegisterServices(IServiceCollection services, WebApplicationBuilder builder)
        {
            // 1. Database
            var connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
            DatabaseExtensions.PreCreateDatabase(connStr);
            services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connStr));

            // 2. Application Services
            services.AddScoped<IUnitOfWork, InvoiceUnitOfWork>();
            services.AddScoped<IInvoiceService, InvoiceService>();

            // 3. MediatR with Transaction Pipeline
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
                cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
            });

            // 4. HTTP Clients (Legacy Audit Client - will be replaced by gRPC later)
            services.AddHttpClient<IAuditServiceClient, AuditServiceClient>(client =>
            {
                var auditUrl = builder.Configuration.GetValue<string>("AuditService:BaseUrl") ?? "http://audit-api:8080";
                client.BaseAddress = new Uri(auditUrl);
                client.Timeout = TimeSpan.FromSeconds(10);
            });

            // 5. Validation & Controllers
            services.AddControllers(options =>
            {
                options.Filters.Add<Invoice.API.Filters.HttpExceptionFilter>();
            });
            services.AddFluentValidationAutoValidation();
            services.AddValidatorsFromAssemblyContaining<Invoice.API.DTOs.CreateInvoiceRequestValidator>();

            // 6. MassTransit
            services.AddBizcoreMassTransit<AppDbContext>(
                builder.Configuration,
                "invoice-service",
                x =>
                {
                    x.AddQuartz();
                    x.AddQuartzConsumers();
                });
        }
    }
}
