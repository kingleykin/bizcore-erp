using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;

namespace Bizcore.BuildingBlocks.Grpc
{
    public static class GrpcExtensions
    {
        /// <summary>
        /// Registers a gRPC client with production-grade resilience (Retry, Circuit Breaker, Timeout)
        /// and automatic Correlation ID propagation.
        /// </summary>
        public static IServiceCollection AddBizcoreGrpcClient<TClient>(
            this IServiceCollection services, 
            IConfiguration configuration,
            string serviceKey)
            where TClient : ClientBase<TClient>
        {
            var section = configuration.GetSection($"GrpcServices:{serviceKey}");
            var url = section.GetValue<string>("Url") 
                      ?? throw new InvalidOperationException($"gRPC URL for {serviceKey} is not configured.");
            
            var timeout = section.GetValue("TimeoutSeconds", 5);
            var retries = section.GetValue("RetryCount", 3);

            services.AddGrpcClient<TClient>(o =>
            {
                o.Address = new Uri(url);
            })
            .AddInterceptor<CorrelationIdInterceptor>()
            .AddResilienceHandler($"grpc-{serviceKey.ToLower()}", pipeline =>
            {
                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = retries,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromSeconds(1)
                });

                pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    MinimumThroughput = 5,
                    BreakDuration = TimeSpan.FromSeconds(30)
                });

                pipeline.AddTimeout(TimeSpan.FromSeconds(timeout));
            });

            return services;
        }

        public static IServiceCollection AddBizcoreGrpcServer(this IServiceCollection services)
        {
            services.AddGrpc(options =>
            {
                options.Interceptors.Add<ServerLoggingInterceptor>();
                // We could add a global Exception Interceptor here too
            });
            return services;
        }
    }

    public class CorrelationIdInterceptor : Interceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CorrelationIdInterceptor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            var correlationId = _httpContextAccessor.HttpContext?.Request.Headers["x-correlation-id"].ToString();
            if (!string.IsNullOrEmpty(correlationId))
            {
                var metadata = context.Options.Headers ?? new Metadata();
                metadata.Add("x-correlation-id", correlationId);
                var newOptions = context.Options.WithHeaders(metadata);
                context = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, newOptions);
            }

            return continuation(request, context);
        }
    }

    public class ServerLoggingInterceptor : Interceptor
    {
        private readonly ILogger<ServerLoggingInterceptor> _logger;

        public ServerLoggingInterceptor(ILogger<ServerLoggingInterceptor> logger)
        {
            _logger = logger;
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            var correlationId = context.RequestHeaders.GetValue("x-correlation-id");
            using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId ?? "N/A" }))
            {
                _logger.LogInformation("Starting gRPC call {Method}", context.Method);
                try
                {
                    return await continuation(request, context);
                }
                catch (RpcException ex)
                {
                    _logger.LogWarning("gRPC call {Method} returned status {Status}", context.Method, ex.StatusCode);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "gRPC call {Method} failed with unhandled exception", context.Method);
                    throw;
                }
            }
        }
    }
}
