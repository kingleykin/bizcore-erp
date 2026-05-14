using Bizcore.BuildingBlocks.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Bizcore.BuildingBlocks.Infrastructure
{
    public static class ServiceDefaults
    {
        public static WebApplicationBuilder AddServiceDefaults(this WebApplicationBuilder builder, string? serviceName = null)
        {
            var name = serviceName ?? builder.Configuration["ServiceName"] ?? builder.Environment.ApplicationName;

            // 1. Standard Logging (Serilog)
            builder.Host.AddBizcoreLogging(name);

            // 2. OpenTelemetry (Traces & Metrics)
            builder.Services.AddBizcoreTelemetry(name);

            // 3. Health Checks
            builder.Services.AddBizcoreHealthChecks();

            // 4. Infrastructure (HttpContext, Controllers, ProblemDetails, Multi-tenancy)
            builder.Services.AddBizcoreInfrastructure();

            // 6. Default Resilience (Standard for all HttpClients)
            builder.Services.ConfigureHttpClientDefaults(http =>
            {
                http.AddStandardResilienceHandler();
            });

            return builder;
        }

        public static WebApplication MapDefaultEndpoints(this WebApplication app, string swaggerTitle)
        {
            // Unify Pipeline
            app.UseBizcorePipeline(swaggerTitle);

            return app;
        }


        private static IServiceCollection AddBizcoreHealthChecks(this IServiceCollection services)
        {
            services.AddHealthChecks()
                // Thêm các check mặc định cho infra nếu cần
                .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

            return services;
        }
    }
}
