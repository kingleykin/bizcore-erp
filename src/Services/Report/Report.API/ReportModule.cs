using Bizcore.BuildingBlocks.Infrastructure;
using Bizcore.BuildingBlocks.MassTransit;
using Bizcore.BuildingBlocks.Messaging;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Report.API.Application.Services;
using Report.API.Infrastructure.Data;

namespace Report.API
{
    public class ReportModule : IServiceModule
    {
        public void RegisterServices(IServiceCollection services, WebApplicationBuilder builder)
        {
            // 1. Database
            var connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
            DatabaseExtensions.PreCreateDatabase(connStr);
            services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connStr));

            // 2. Application Services
            services.AddScoped<IReportService, ReportService>();
            services.AddMemoryCache();

            // 3. MassTransit
            services.AddBizcoreMassTransit<AppDbContext>(
                builder.Configuration,
                QueueNames.ReportService);
        }
    }
}
