namespace Payment.API.Domain.Entities
{
    /// <summary>
    /// Persistent idempotency record trong database.
    /// Đảm bảo idempotency across service restarts và multiple instances.
    /// Supports response replay for duplicate requests.
    /// </summary>
    public class IdempotencyRecord
    {
        /// <summary>Idempotency key từ client (unique).</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>PaymentId đã được tạo cho key này.</summary>
        public Guid PaymentId { get; set; }

        /// <summary>Timestamp khi record được tạo.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Timestamp khi record hết hạn (TTL).</summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>Request payload hash để verify request consistency.</summary>
        public string? RequestHash { get; set; }

        /// <summary>Processing status: InProgress, Completed, Failed, Expired.</summary>
        public string Status { get; set; } = "InProgress";

        /// <summary>Cached response JSON for replay on duplicate requests.</summary>
        public string? ResponseJson { get; set; }

        /// <summary>HTTP status code of the cached response.</summary>
        public int? StatusCode { get; set; }
    }
}
