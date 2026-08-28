namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Event: toàn bộ chuỗi xử lý phía sau một thanh toán Đơn hàng đã hoàn tất — publish bởi
    /// Customer.API sau bước CUỐI cùng của chuỗi (cộng điểm thưởng). Payment.API lắng nghe để
    /// chuyển Payment.Status = Fulfilled và đẩy SignalR báo kết quả CUỐI cho khách hàng.
    ///
    /// Vì sao cần event này: Payment.Status = Completed được set NGAY khi tiền được ghi nhận, TRƯỚC
    /// khi các bước phía sau (Order.Confirm, Inventory.Commit, cộng điểm) chạy xong. Nếu một trong
    /// các bước đó thất bại vĩnh viễn, payment bị bồi hoàn về Reversed — có thể xảy ra rất muộn
    /// (riêng bước cộng điểm phải chờ hết 5 lần retry, chưa kể thời gian mỗi lần thử thất bại).
    /// Báo "thanh toán thành công" cho khách ngay tại Completed vì thế là báo một kết quả CHƯA chắc
    /// chắn — đúng lỗi đã gặp: khách thấy thành công rồi một lát sau mới biết bị hoàn. Đợi event này
    /// (hoặc IPaymentCompensationRequestedEvent) mới là kết quả cuối cùng, xác định, không cần đoán
    /// bằng bất kỳ khoảng chờ cố định nào ở client.
    ///
    /// Chỉ áp dụng cho thanh toán Đơn hàng. Thanh toán Hóa đơn trực tiếp không có chuỗi phía sau nên
    /// Completed đã là trạng thái cuối.
    /// </summary>
    public interface IOrderPaymentFulfilledEvent
    {
        Guid PaymentId { get; }
        Guid OrderId { get; }
        DateTime FulfilledAt { get; }
    }
}
