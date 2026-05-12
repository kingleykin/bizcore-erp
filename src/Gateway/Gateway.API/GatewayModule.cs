using Bizcore.BuildingBlocks.Infrastructure;
using Bizcore.BuildingBlocks.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy.Transforms;

namespace Gateway.API
{
    public class GatewayModule : IServiceModule
    {
        public void RegisterServices(IServiceCollection services, WebApplicationBuilder builder)
        {
            // 1. CORS
            services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            // 2. Rate Limiting
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddFixedWindowLimiter("fixed", opt =>
                {
                    opt.PermitLimit = 100;
                    opt.Window = TimeSpan.FromMinutes(1);
                    opt.QueueLimit = 0;
                });
                options.AddPolicy("per-ip", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 50,
                            Window = TimeSpan.FromMinutes(1)
                        }));
            });

            // 3. Resilience
            services.AddResiliencePipeline("default", pipeline =>
            {
                pipeline.AddRetry(new Polly.Retry.RetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(2),
                    BackoffType = Polly.DelayBackoffType.Exponential
                });
                pipeline.AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    MinimumThroughput = 5,
                    BreakDuration = TimeSpan.FromSeconds(15)
                });
            });

            // 4. YARP
            services.AddReverseProxy()
                .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
                .AddTransforms(transformBuilder =>
                {
                    transformBuilder.AddRequestTransform(context =>
                    {
                        var correlationId = context.HttpContext.Items["X-Correlation-ID"]?.ToString();
                        if (!string.IsNullOrEmpty(correlationId))
                        {
                            context.ProxyRequest.Headers.Remove("X-Correlation-ID");
                            context.ProxyRequest.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);
                        }
                        return ValueTask.CompletedTask;
                    });
                    transformBuilder.AddResponseTransform(context =>
                    {
                        context.ProxyResponse?.Headers.Remove("X-Correlation-ID");
                        return ValueTask.CompletedTask;
                    });
                });
        }
    }
}
