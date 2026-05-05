using System.Net.Http;
using Microsoft.AspNetCore.Http;

namespace Bizcore.BuildingBlocks.DelegatingHandlers
{
    public class CorrelationIdHeaderHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private const string CorrelationIdHeader = "X-Correlation-ID";

        public CorrelationIdHeaderHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var correlationId = _httpContextAccessor.HttpContext?.Items[CorrelationIdHeader]?.ToString();

            if (!string.IsNullOrEmpty(correlationId) && !request.Headers.Contains(CorrelationIdHeader))
            {
                request.Headers.Add(CorrelationIdHeader, correlationId);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
