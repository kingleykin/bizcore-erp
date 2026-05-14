using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Diagnostics;

namespace Bizcore.BuildingBlocks.Infrastructure
{
    public static class TelemetryExtensions
    {
        public static IServiceCollection AddBizcoreTelemetry(this IServiceCollection services, string serviceName, string serviceVersion = "1.0.0")
        {
            var resourceBuilder = ResourceBuilder.CreateDefault()
                .AddService(serviceName, serviceVersion: serviceVersion)
                .AddTelemetrySdk();

            services.AddOpenTelemetry()
                .WithTracing(tracing => tracing
                    .SetResourceBuilder(resourceBuilder)
                    .SetSampler(new AlwaysOnSampler())
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddGrpcClientInstrumentation()
                    .AddSource("MassTransit")
                    .AddOtlpExporter())
                .WithMetrics(metrics => metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddMeter(
                        "Microsoft.AspNetCore.Hosting",
                        "Microsoft.AspNetCore.Server.Kestrel",
                        "System.Net.Http",
                        "Bizcore.*") // For business metrics
                    .AddPrometheusExporter());

            return services;
        }
    }
}
