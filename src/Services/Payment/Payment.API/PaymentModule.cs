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
            services.AddDbContext<PaymentDbContext>(options => options.UseSqlServer(connStr));

            // 2. Business Services
            services.AddScoped<IPaymentService, PaymentService>();

            // 3. MassTransit (Using the new convention-based helper)
            services.AddBizcoreMassTransit<PaymentDbContext>(
                builder.Configuration, 
                QueueNames.PaymentService);
        }
    }
}
