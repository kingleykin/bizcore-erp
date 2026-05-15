using Bizcore.BuildingBlocks.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Prometheus;
using Serilog;

namespace Bizcore.BuildingBlocks.Infrastructure
{
    public static class WebApplicationExtensions
    {
        public static WebApplication UseBizcorePipeline(this WebApplication app, string swaggerTitle)
        {
            app.UseSerilogRequestLogging(options =>
            {
                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    diagnosticContext.Set("UserId", httpContext.User?.Identity?.Name ?? "Anonymous");
                    diagnosticContext.Set("TenantId", httpContext.Items["TenantId"] ?? "Default");
                    diagnosticContext.Set("CorrelationId", httpContext.Items["X-Correlation-ID"] ?? httpContext.TraceIdentifier);
                    diagnosticContext.Set("TraceId", System.Diagnostics.Activity.Current?.TraceId.ToString());
                };
            });

            app.UseMiddleware<GlobalExceptionMiddleware>();
            app.UseMiddleware<CorrelationIdPropagationMiddleware>();
            app.UseMiddleware<TenantMiddleware>(); // 🏢 Tenant context extraction

            app.MapPrometheusScrapingEndpoint();
            app.MapHealthChecks("/health");

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", swaggerTitle);
                c.RoutePrefix = "swagger";
            });

            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            return app;
        }
    }
}
