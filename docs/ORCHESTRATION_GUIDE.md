# Hướng dẫn Orchestration Service (BizCore ERP)

Orchestration service là **điểm quan sát luồng nghiệp vụ phân tán** (distributed process tracking): không thay Invoice/Payment thực thi domain logic, mà **ghi nhận timeline** các sự kiện để dev/ops có cái nhìn thống nhất về vòng đời giao dịch.

---

## Kiến trúc vai trò (Rationale)

| Thành phần | Vai trò |
|---|---|
| Invoice / Payment / Report | Bounded context chứa quyết định nghiệp vụ và dữ liệu gốc |
| Orchestration | **Đồng bộ quan sát** các event-domain, persist `ProcessFlow` + các bước `FlowStep`, expose API chỉ đọc |

Đây là mô hình **choreography (event-led) + cửa quan trọng chỉ nhìn (read-side orchestration view)**. Khi muốn nâng cấp lên Saga có command trung tâm, có thể bắt đầu phát Command từ đây mà không bỏ lịch sử bước.

---

## Dữ liệu

Database (demo): **`OrchestrationDb`** (`ConnectionStrings__DefaultConnection` trong `Orchestration.API`)

| Bảng / entity | Mô tả |
|---|---|
| `ProcessFlow` | Một phiên luồng theo **`InvoiceId` (unique)**. Trường `CurrentState`, `LastPaymentId`, timestamps |
| `FlowStep` | Bản ghi bất biến: loại bước + `PayloadJson` (snapshot của event đã quan sát) |

Giá trị `CurrentState` (flow `invoice-payment`):

| State | Ý nghĩa |
|---|---|
| `InvoiceIndexed` | Đã thấy `IInvoiceCreatedEvent` |
| `PaymentCaptured` | Đã thấy `IPaymentCompletedEvent` |
| `CompensationRequired` | Đã thấy `IPaymentCompensationRequestedEvent` (rollback nghiệp vụ) |

Giá trị `StepType` (xem `Orchestration.API.Domain.InvoicePaymentFlow`):

| Step | Trigger |
|---|---|
| `InvoiceCreatedObserved` | `IInvoiceCreatedEvent` |
| `PaymentCompletedObserved` | `IPaymentCompletedEvent` |
| `PaymentCompensationRequestedObserved` | `IPaymentCompensationRequestedEvent` |

---

## RabbitMQ (MassTransit — queue riêng)

Để **không tranh endpoint** với Invoice/Payment/Report consumers, orchestrator có queue riêng:

| Endpoint (queue tiêu thụ) | Event |
|---|---|
| `orchestration-invoice-created` | `IInvoiceCreatedEvent` |
| `orchestration-payment-completed` | `IPaymentCompletedEvent` |
| `orchestration-payment-compensation-requested` | `IPaymentCompensationRequestedEvent` |

Cấu hình nhận: `Orchestration.API/Program.cs` → `ConfigureConsumer<>`.

Publish vẫn do các bounded context như Invoice/Payment hiện tại; RabbitMQ routing deliver **parallel** vào các queue của từng subscriber.

---

## API (qua Gateway)

Đường dẫn: prefix `/api/v1/orchestration/versions` không — thực tế:

```
GET http://localhost:5000/api/v1/orchestration/flows?take=50
GET http://localhost:5000/api/v1/orchestration/flows/{invoiceId}
```

- **JWT bắt buộc** với permission `orchestration:view` (`Permissions.Orchestration.View`).
- Mock login **`admin`** (Gateway `Program.cs`) đã được gắn quyền này. User **`user`** **không** có quyền orchestration trong demo hiện tại.

Swagger (local orchestration cổng 5004 khi dev): endpoint tương tự không qua Gateway.

---

## Docker Compose

Đã khai báo service `orchestration-api`; Gateway override cluster:

```
ReverseProxy__Clusters__orchestration-cluster__Destinations__d1__Address=http://orchestration-api:8080
```

---

## Chạy cục bộ

1. Chạy SQL Server + RabbitMQ (hoặc `docker-compose` subset).
2. Chạy `Orchestration.API` (profiles `launchSettings`: `http://localhost:5004`).
3. Chạy Gateway + các service domain.
4. Gọi `GET /api/v1/orchestration/flows` sau khi tạo hóa đơn / thanh toán để kiểm tra timeline.

---

## Hướng mở rộng

- Retry / inbox cho consumer orchestration (độ bền nội bộ OrchestrationDb).
- Thêm event `InvoicePaidConfirmed` để khép trạng thái luồng khi chỉ Invoice mới có chân lý nghiệp vụ chính xác nhất sau khi cập nhật DB.

---

*Tài liệu này mô tả triển khai hiện tại trong nhánh codebase; chỉnh sửa bổ sung hãy cập nhật nội dung và liên kết từ [PROJECT STRUCTURE.md](PROJECT%20STRUCTURE.md).*
