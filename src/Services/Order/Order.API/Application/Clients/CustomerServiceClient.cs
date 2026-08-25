using System.Net.Http.Json;
using System.Text.Json;

namespace Order.API.Application.Clients
{
    /// <summary>
    /// DTO tối giản nhận từ Customer Service khi kiểm tra khách hàng tồn tại.
    /// </summary>
    public record CustomerInfo(Guid Id, string Name, bool IsActive);

    public interface ICustomerServiceClient
    {
        Task<CustomerInfo?> GetCustomerAsync(Guid customerId, CancellationToken ct = default);
    }

    /// <summary>
    /// HTTP client gọi sang Customer.API để xác thực khách hàng khi tạo đơn hàng.
    /// Được inject qua IHttpClientFactory với named client "CustomerService".
    /// </summary>
    public class CustomerServiceClient : ICustomerServiceClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _http;
        private readonly ILogger<CustomerServiceClient> _logger;

        public CustomerServiceClient(HttpClient http, ILogger<CustomerServiceClient> logger)
        {
            _http   = http;
            _logger = logger;
        }

        public async Task<CustomerInfo?> GetCustomerAsync(Guid customerId, CancellationToken ct = default)
        {
            try
            {
                var response = await _http.GetAsync($"api/v1/customers/{customerId}", ct);
                if (!response.IsSuccessStatusCode) return null;

                return await response.Content.ReadFromJsonAsync<CustomerInfo>(JsonOptions, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch Customer {CustomerId} from Customer Service.", customerId);
                return null;
            }
        }
    }
}
