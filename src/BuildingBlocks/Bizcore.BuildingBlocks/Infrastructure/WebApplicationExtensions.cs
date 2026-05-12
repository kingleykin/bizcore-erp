using Bizcore.BuildingBlocks.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Prometheus;

namespace Bizcore.BuildingBlocks.Infrastructure
{
    public static class WebApplicationExtensions
    {
        public static WebApplication UseBizcorePipeline(this WebApplication app, string swaggerTitle)
        {
            app.UseMiddleware<GlobalExceptionMiddleware>();
            app.UseMiddleware<CorrelationIdPropagationMiddleware>();

            app.UseHttpMetrics();
            app.MapHealthChecks("/health");
            app.MapMetrics();

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
