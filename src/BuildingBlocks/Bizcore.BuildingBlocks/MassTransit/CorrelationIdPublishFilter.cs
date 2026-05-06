using MassTransit;
using Microsoft.AspNetCore.Http;

namespace Bizcore.BuildingBlocks.MassTransit
{
    /// <summary>
    /// MassTransit publish filter: tự động lấy CorrelationId từ HTTP context
    /// và gắn vào header của mọi message được publish lên RabbitMQ.
    ///
    /// Consumer đọc lại bằng CorrelationIdConsumeFilter và push vào Serilog LogContext,
    /// đảm bảo log xuyên suốt HTTP → RabbitMQ → Consumer đều có cùng CorrelationId.
    /// </summary>
    public class CorrelationIdPublishFilter<T> : IFilter<PublishContext<T>>
        where T : class
    {
        private const string CorrelationIdHeader = "X-Correlation-ID";
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CorrelationIdPublishFilter(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
        {
            var correlationId = _httpContextAccessor.HttpContext?.Items[CorrelationIdHeader]?.ToString();

            if (!string.IsNullOrEmpty(correlationId))
            {
                context.Headers.Set(CorrelationIdHeader, correlationId);
            }

            return next.Send(context);
        }

        public void Probe(ProbeContext context) =>
            context.CreateFilterScope("correlationIdPublish");
    }
}
