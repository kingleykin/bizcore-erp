using Microsoft.AspNetCore.Http;

namespace Order.API.Application.Clients
{
    /// <summary>
    /// Forward Bearer token của request gốc sang các lời gọi HTTP nội bộ tới
    /// Customer.API/Product.API — 2 service này yêu cầu [Authorize], nên nếu không
    /// forward token thì lời gọi sẽ nhận 401 và bị hiểu nhầm thành "không tìm thấy".
    /// </summary>
    public class AuthForwardingHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthForwardingHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var authHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();

            if (!string.IsNullOrEmpty(authHeader) && request.Headers.Authorization == null)
            {
                request.Headers.TryAddWithoutValidation("Authorization", authHeader);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
