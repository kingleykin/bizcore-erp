using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Behaviors;
using Bizcore.BuildingBlocks.Infrastructure;
using Bizcore.BuildingBlocks.MassTransit;
using Bizcore.BuildingBlocks.Messaging;
using FluentValidation;
using FluentValidation.AspNetCore;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Order.API.Application.Clients;
using Order.API.Infrastructure.Data;

namespace Order.API
{
    public class OrderModule : IServiceModule
    {
        public void RegisterServices(IServiceCollection services, WebApplicationBuilder builder)
        {
            // 1. Database
            var connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
            DatabaseExtensions.PreCreateDatabase(connStr);
            services.AddBizcoreDbContext<AppDbContext>(connStr);

            // 2. Application Services
            services.AddScoped<IUnitOfWork, OrderUnitOfWork>();

            // 3. MediatR with Transaction Pipeline
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
                cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
            });

            // 4. HTTP Clients
            // AuthForwardingHandler: forward Bearer token của request gốc, vì Customer.API/Product.API yêu cầu [Authorize].
            services.AddTransient<AuthForwardingHandler>();

            services.AddHttpClient<ICustomerServiceClient, CustomerServiceClient>(client =>
            {
                var customerUrl = builder.Configuration.GetValue<string>("CustomerService:BaseUrl") ?? "http://customer-api:8080";
                client.BaseAddress = new Uri(customerUrl);
                client.Timeout = TimeSpan.FromSeconds(10);
            }).AddHttpMessageHandler<AuthForwardingHandler>();

            services.AddHttpClient<IProductServiceClient, ProductServiceClient>(client =>
            {
                var productUrl = builder.Configuration.GetValue<string>("ProductService:BaseUrl") ?? "http://product-api:8080";
                client.BaseAddress = new Uri(productUrl);
                client.Timeout = TimeSpan.FromSeconds(10);
            }).AddHttpMessageHandler<AuthForwardingHandler>();

            services.AddHttpClient<IInventoryServiceClient, InventoryServiceClient>(client =>
            {
                var inventoryUrl = builder.Configuration.GetValue<string>("InventoryService:BaseUrl") ?? "http://inventory-api:8080";
                client.BaseAddress = new Uri(inventoryUrl);
                client.Timeout = TimeSpan.FromSeconds(10);
            }).AddHttpMessageHandler<AuthForwardingHandler>();

            // 5. Validation & Controllers
            services.AddControllers();
            services.AddFluentValidationAutoValidation();
            services.AddValidatorsFromAssemblyContaining<Order.API.Application.Validators.CreateOrderRequestValidator>();

            // 6. MassTransit
            services.AddBizcoreMassTransit<AppDbContext>(
                builder.Configuration,
                QueueNames.OrderService);
        }
    }
}
