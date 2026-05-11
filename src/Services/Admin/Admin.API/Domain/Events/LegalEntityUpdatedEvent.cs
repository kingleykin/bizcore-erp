namespace Admin.API.Domain.Events
{
    /// <summary>
    /// Event được publish qua RabbitMQ khi thông tin LegalEntity thay đổi.
    /// Các service khác (ACC, HR) subscribe để cập nhật Read Model của mình.
    /// </summary>
    public record LegalEntityUpdatedEvent
    {
        /// <summary>ID của pháp nhân bị thay đổi.</summary>
        public Guid   LegalEntityId { get; init; }

        /// <summary>Mã code định danh ngắn gọn.</summary>
        public string Code { get; init; } = null!;

        /// <summary>Tên đầy đủ của pháp nhân.</summary>
        public string Name { get; init; } = null!;

        /// <summary>Mã tiền tệ cơ sở (ISO 4217). VD: VND, USD.</summary>
        public string? BaseCurrencyCode { get; init; }

        /// <summary>Trạng thái (1=Active, 0=Inactive).</summary>
        public int Status { get; init; }

        /// <summary>Thời điểm sự kiện xảy ra (UTC).</summary>
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    }
}
