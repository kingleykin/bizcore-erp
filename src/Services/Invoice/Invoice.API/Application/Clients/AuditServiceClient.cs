namespace Invoice.API.Application.Clients
{
    /// <summary>
    /// DTO nhận từ Audit Service khi query một AuditEntry.
    /// Chỉ chứa những field Invoice Service cần để chạy Diff Engine.
    /// </summary>
    public class AuditEntryResponse
    {
        public Guid      Id             { get; set; }
        public string?   EntityName     { get; set; }
        public string?   EntityId       { get; set; }
        public string    Action         { get; set; } = null!;
        public string?   BeforeJson     { get; set; }
        public string?   AfterJson      { get; set; }
        public DateTime  PerformedAt    { get; set; }
        public bool      IsReversed     { get; set; }
    }

    public interface IAuditServiceClient
    {
        Task<AuditEntryResponse?> GetEntryAsync(Guid auditEntryId, CancellationToken ct = default);
        Task MarkAsReversedAsync(Guid auditEntryId, Guid reversalEntryId, string reason, CancellationToken ct = default);
    }

    /// <summary>
    /// HTTP client gọi sang Audit.API.
    /// Được inject qua IHttpClientFactory với named client "AuditService".
    /// </summary>
    public class AuditServiceClient : IAuditServiceClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<AuditServiceClient> _logger;

        public AuditServiceClient(HttpClient http, ILogger<AuditServiceClient> logger)
        {
            _http   = http;
            _logger = logger;
        }

        public async Task<AuditEntryResponse?> GetEntryAsync(Guid auditEntryId, CancellationToken ct = default)
        {
            try
            {
                var response = await _http.GetAsync($"api/v1/audit/{auditEntryId}", ct);
                if (!response.IsSuccessStatusCode) return null;

                return await response.Content.ReadFromJsonAsync<AuditEntryResponse>(cancellationToken: ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch AuditEntry {AuditEntryId} from Audit Service.", auditEntryId);
                return null;
            }
        }

        public async Task MarkAsReversedAsync(
            Guid auditEntryId, Guid reversalEntryId, string reason, CancellationToken ct = default)
        {
            try
            {
                var body = new { reversalEntryId, reason };
                await _http.PatchAsync(
                    $"api/v1/audit/{auditEntryId}/mark-reversed",
                    JsonContent.Create(body),
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to mark AuditEntry {AuditEntryId} as reversed.", auditEntryId);
                // Non-critical: không throw — reversal đã thành công, chỉ marking thất bại
            }
        }
    }
}
