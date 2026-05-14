using Bizcore.BuildingBlocks.Security.DataClassification;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Sinks.Grafana.Loki;

namespace Bizcore.BuildingBlocks.Infrastructure
{
    public static class HostExtensions
    {
        public static IHostBuilder AddBizcoreLogging(this IHostBuilder host, string serviceName)
        {
            host.UseSerilog((context, loggerConfiguration) =>
            {
                var lokiUrl = context.Configuration.GetValue<string>("Loki:Url") ?? "http://loki:3100";
                
                loggerConfiguration
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("Service", serviceName)
                    .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                    .Destructure.With<SensitiveDataDestructuringPolicy>() // 🛡️ Data Sanitization
                    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
                    .WriteTo.GrafanaLoki(lokiUrl,
                        labels: new[]
                        {
                            new LokiLabel { Key = "service", Value = serviceName.ToLower().Replace(".", "-") },
                            new LokiLabel { Key = "environment", Value = context.HostingEnvironment.EnvironmentName }
                        },
                        propertiesAsLabels: new[] { "CorrelationId", "TenantId" }); // 🏢 Tenant propagation
            });

            return host;
        }
    }
}
