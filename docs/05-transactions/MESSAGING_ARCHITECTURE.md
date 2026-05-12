# Hướng dẫn Kiến trúc Messaging & Độ tin cậy (Reliability)

Tài liệu này mô tả các tiêu chuẩn về messaging cấp độ production được triển khai trong hệ thống Bizcore ERP sử dụng **MassTransit** và **RabbitMQ**. Kiến trúc của chúng ta ưu tiên tính chính xác về tài chính, độ tin cậy của thông điệp và tính nhất quán dữ liệu phân tán.

## 1. Transactional Outbox & Inbox (EF Core)

Để đảm bảo tính nguyên tử (atomicity) giữa việc thay đổi database và xuất bản message (publishing), chúng ta sử dụng mô hình **Transactional Outbox**. Điều này ngăn chặn lỗi "dual-write" - trường hợp database commit thành công nhưng gửi message thất bại (hoặc ngược lại).

### Các thành phần chính

- **Outbox**: Lưu tạm các message gửi đi vào database trong cùng một transaction với logic nghiệp vụ của bạn.
- **Inbox**: Ghi lại ID của các message gửi đến để ngăn chặn việc xử lý trùng lặp (Deduplication/Idempotency).
- **Delivery Service**: Một tác vụ chạy ngầm (background task) đẩy các message từ database lên RabbitMQ.

### Triển khai

Trong `Program.cs` của mỗi service:

```csharp
x.AddBusinessOutbox<AppDbContext>(); // Giai đoạn đăng ký (Registration)

// ... bên trong UsingRabbitMq
cfg.ReceiveEndpoint(QueueNames.ServiceName, e => {
    e.UseEntityFrameworkOutbox<AppDbContext>(context); // Giai đoạn gắn Middleware
});
```

### Cấu hình (BuildingBlocks)

- `QueryDelay`: Được đặt thành **1 giây** để đảm bảo phản hồi nhanh trong các luồng ERP.
- `UseBusOutbox()`: Tự động đánh chặn `IPublishEndpoint` và `ISendEndpoint` từ tầng HTTP/Service.

---

## 2. Chiến lược Điều phối Saga (Saga Orchestration)

Chúng ta sử dụng **State Machine Sagas** để điều phối các giao dịch phân tán phức tạp (ví dụ: Thanh toán -> Xác thực hóa đơn -> Xác nhận hoàn tất).

### Tiêu chuẩn độ tin cậy

- **Scoped DbContext**: Sagas BẮT BUỘC phải sử dụng `r.ExistingDbContext<T>()` để dùng chung phạm vi transaction với Outbox và các service nghiệp vụ.
- **Persistence**: Trạng thái của Saga được lưu trữ vào database chính của service (ví dụ: bảng `PaymentSagaStates`).
- **Concurrency**: Sử dụng **Pessimistic Concurrency** (Khóa bi quan) để ngăn chặn race conditions trong quá trình chuyển đổi trạng thái.

---

## 3. RabbitMQ Topology & Quy ước

Chúng ta áp dụng mô hình sở hữu ở cấp độ service để tránh xung đột cấu hình (topology conflicts).

### Thiết lập Receive Endpoints tiêu chuẩn

Sử dụng `ApplyBusinessEndpointSettings()` để thực thi:

- **Durable**: Queue được lưu trữ bền vững trên đĩa.
- **AutoDelete = false**: Queue không bị xóa ngay cả khi không có consumer nào hoạt động.
- **No TTL**: Dữ liệu tài chính quan trọng trong ERP không được phép tự động hết hạn (expire).
- **Shared DLX**: Các message lỗi được chuyển đến `bizcore.dlx` với routing key `{queue-name}.error`.

### Định tuyến Command (Sender Topology)

Bên gửi không cần biết chi tiết cấu hình queue của bên nhận. Chúng ta định tuyến command thông qua **Exchange**:

```csharp
x.MapBusinessCommand<IValidateInvoiceCommand>(QueueNames.InvoiceService);
```

---

## 4. Khả năng quan sát (Observability) & Correlation

Mỗi message đều mang theo ngữ cảnh để cho phép truy vết (tracing) từ đầu đến cuối qua các microservices.

### Correlation ID

- **Truyền dẫn (Propagation)**: `CorrelationIdPropagationMiddleware` trích xuất ID từ HTTP headers.
- **Message Headers**: `CorrelationIdPublishFilter` và `CorrelationIdSendFilter` gắn ID vào các message gửi đi.
- **Logging**: `CorrelationIdConsumeFilter` đẩy ID vào Serilog `LogContext` cho mỗi message được xử lý.

---

## 5. Bảo trì & Vận hành

### Dọn dẹp Outbox (Cleanup)

MassTransit 8 tích hợp sẵn job dọn dẹp. Mặc định:

- Các message đã xử lý sẽ được xóa khỏi bảng `OutboxMessage` và `InboxState`.
- Việc này ngăn chặn database phình to vô hạn.

### Database Migrations

Khi kích hoạt Outbox trong một service mới, bạn phải thêm các thực thể MassTransit vào `DbContext`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder) {
    base.OnModelCreating(modelBuilder);
    modelBuilder.AddInboxStateEntity();
    modelBuilder.AddOutboxMessageEntity();
    modelBuilder.AddOutboxStateEntity();
}
```

Sau đó chạy lệnh: `dotnet ef migrations add AddMassTransitOutbox`.
