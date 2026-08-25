using System.Net.Http.Json;
using System.Text.Json;

namespace Order.API.Application.Clients
{
    /// <summary>
    /// DTO tối giản nhận từ Product Service khi kiểm tra sản phẩm tồn tại.
    /// </summary>
    public record ProductInfo(Guid Id, string Code, string Name, decimal Price, bool IsActive);

    public interface IProductServiceClient
    {
        Task<ProductInfo?> GetProductAsync(Guid productId, CancellationToken ct = default);
    }

    /// <summary>
    /// HTTP client gọi sang Product.API để xác thực sản phẩm khi thêm dòng hàng vào đơn hàng.
    /// Được inject qua IHttpClientFactory với named client "ProductService".
    /// </summary>
    public class ProductServiceClient : IProductServiceClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _http;
        private readonly ILogger<ProductServiceClient> _logger;

        public ProductServiceClient(HttpClient http, ILogger<ProductServiceClient> logger)
        {
            _http   = http;
            _logger = logger;
        }

        public async Task<ProductInfo?> GetProductAsync(Guid productId, CancellationToken ct = default)
        {
            try
            {
                var response = await _http.GetAsync($"api/v1/products/{productId}", ct);
                if (!response.IsSuccessStatusCode) return null;

                return await response.Content.ReadFromJsonAsync<ProductInfo>(JsonOptions, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch Product {ProductId} from Product Service.", productId);
                return null;
            }
        }
    }
}
