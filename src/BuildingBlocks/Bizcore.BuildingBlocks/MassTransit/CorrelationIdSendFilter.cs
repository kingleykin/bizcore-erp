using MassTransit;
using Microsoft.AspNetCore.Http;

namespace Bizcore.BuildingBlocks.MassTransit
{
    /// <summary>
    /// MassTransit send filter: tự động lấy CorrelationId từ HTTP context
    /// và gắn vào header của mọi message được gửi qua Send/RequestClient.
    ///
    /// Cần thiết vì CorrelationIdPublishFilter chỉ cover Publish(),
    /// còn IRequestClient.GetResponse() đi qua SendContext, không phải PublishContext.
    /// </summary>
    public class CorrelationIdSendFilter<T> : IFilter<SendContext<T>>
        where T : class
    {
        private const string CorrelationIdHeader = "X-Correlation-ID";
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CorrelationIdSendFilter(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Task Send(SendContext<T> context, IPipe<SendContext<T>> next)
        {
            var correlationId = _httpContextAccessor.HttpContext?.Items[CorrelationIdHeader]?.ToString();

            if (!string.IsNullOrEmpty(correlationId))
            {
                context.Headers.Set(CorrelationIdHeader, correlationId);
            }

            return next.Send(context);
        }

        public void Probe(ProbeContext context) =>
            context.CreateFilterScope("correlationIdSend");
    }
}
