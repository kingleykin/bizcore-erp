using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Bizcore.BuildingBlocks;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace Order.API.Application.Clients
{
    /// <summary>
    /// Forward Bearer token của request gốc sang các lời gọi HTTP nội bộ tới
    /// Customer.API/Product.API/Inventory.API — các service này yêu cầu [Authorize], nên nếu không
    /// forward token thì lời gọi sẽ nhận 401 và bị hiểu nhầm thành "không tìm thấy".
    ///
    /// Khi không có HttpContext (đang chạy trong MassTransit consumer nền — vd.
    /// ValidateOrderCommandConsumer gọi Inventory.API để check tồn kho lúc validate thanh toán —
    /// không phải trong 1 HTTP request nên không có token người dùng để forward): tự ký 1 service
    /// token ngắn hạn bằng Jwt:SecretKey dùng chung toàn hệ thống (mọi service đã cùng validate
    /// bằng 1 secret — xem AddBizcoreAuth), chỉ mang đúng permission cần cho các lời gọi nội bộ,
    /// không đại diện cho user thật nào.
    /// </summary>
    public class AuthForwardingHandler : DelegatingHandler
    {
        private static readonly Guid SystemServiceId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public AuthForwardingHandler(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var authHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();

            if (!string.IsNullOrEmpty(authHeader) && request.Headers.Authorization == null)
            {
                request.Headers.TryAddWithoutValidation("Authorization", authHeader);
            }
            else if (string.IsNullOrEmpty(authHeader) && request.Headers.Authorization == null)
            {
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {BuildServiceToken()}");
            }

            return base.SendAsync(request, cancellationToken);
        }

        private string BuildServiceToken()
        {
            var secretKey = _configuration["Jwt:SecretKey"]
                ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
            var key = Encoding.ASCII.GetBytes(secretKey);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, SystemServiceId.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, "order-service-internal"),
                new Claim("permission", Permissions.Inventory.View),
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(2),
                Issuer = _configuration["Jwt:Issuer"] ?? "bizcore-admin",
                Audience = _configuration["Jwt:Audience"] ?? "bizcore-erp",
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var handler = new JwtSecurityTokenHandler();
            return handler.WriteToken(handler.CreateToken(tokenDescriptor));
        }
    }
}
