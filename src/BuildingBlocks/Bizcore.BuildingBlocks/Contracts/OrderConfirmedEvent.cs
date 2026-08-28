namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Event: Order đã được xác nhận (coi như hoàn tất/đã thanh toán trong mô hình hiện tại).
    /// Inventory Service lắng nghe để chốt số đã giữ chỗ thành trừ kho thật (commit).
    /// Invoice Service lắng nghe để tự sinh Invoice (chứng từ/biên lai) phái sinh từ Order —
    /// cần CustomerName/TotalAmount ở đây để không phải gọi ngược sang Order.API qua HTTP.
    /// Dùng record (concrete type) — xem giải thích ở OrderCreatedEvent.
    ///
    /// PaymentId: chỉ có giá trị khi Order được Confirm tự động do thanh toán hoàn tất
    /// (PaymentConfirmedConsumer) — null khi Confirm thủ công qua API. Inventory/Invoice dùng nó
    /// để biết có payment cần bồi hoàn (publish IPaymentCompensationRequestedEvent) hay không nếu
    /// bước Commit/tạo Invoice sau đó thất bại. Customer.API cũng dùng nó để chỉ cộng điểm thưởng
    /// cho đơn THỰC SỰ đã thanh toán, không cộng khi Confirm thủ công (PaymentId null).
    /// </summary>
    public record OrderConfirmedEvent(
        Guid Id,
        Guid CustomerId,
        string CustomerName,
        decimal TotalAmount,
        IReadOnlyCollection<OrderEventItem> Items,
        DateTime ConfirmedAt,
        Guid? PaymentId = null
    );
}
