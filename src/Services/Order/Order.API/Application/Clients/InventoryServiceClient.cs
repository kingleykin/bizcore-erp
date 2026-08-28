using System.Net.Http.Json;
using System.Text.Json;

namespace Order.API.Application.Clients
{
    /// <summary>
    /// DTO tối giản nhận từ Inventory Service khi kiểm tra tồn kho khả dụng.
    /// </summary>
    public record StockInfo(Guid ProductId, int QuantityOnHand, int QuantityReserved, int AvailableQuantity);

    public interface IInventoryServiceClient
    {
        Task<StockInfo?> GetStockAsync(Guid productId, CancellationToken ct = default);
    }

    /// <summary>
    /// HTTP client gọi sang Inventory.API để kiểm tra tồn kho khả dụng trước khi tạo đơn hàng.
    /// Được inject qua IHttpClientFactory với named client "InventoryService".
    /// </summary>
    public class InventoryServiceClient : IInventoryServiceClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _http;
        private readonly ILogger<InventoryServiceClient> _logger;

        public InventoryServiceClient(HttpClient http, ILogger<InventoryServiceClient> logger)
        {
            _http   = http;
            _logger = logger;
        }

        public async Task<StockInfo?> GetStockAsync(Guid productId, CancellationToken ct = default)
        {
            try
            {
                var response = await _http.GetAsync($"api/v1/inventory/{productId}", ct);
                if (!response.IsSuccessStatusCode) return null;

                return await response.Content.ReadFromJsonAsync<StockInfo>(JsonOptions, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch Stock for Product {ProductId} from Inventory Service.", productId);
                return null;
            }
        }
    }
}
