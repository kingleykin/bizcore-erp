using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Behaviors;
using Bizcore.BuildingBlocks.Infrastructure;
using Bizcore.BuildingBlocks.MassTransit;
using Bizcore.BuildingBlocks.Messaging;
using Invoice.API.Application.Clients;
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
            services.AddBizcoreDbContext<AppDbContext>(connStr);

            // 2. Application Services
            services.AddScoped<IUnitOfWork, InvoiceUnitOfWork>();

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

            // 5. Controllers
            services.AddControllers(options =>
            {
                options.Filters.Add<Invoice.API.Filters.HttpExceptionFilter>();
            });

            // 6. MassTransit
            services.AddBizcoreMassTransit<AppDbContext>(
                builder.Configuration, 
                QueueNames.InvoiceService);
        }
    }
}
