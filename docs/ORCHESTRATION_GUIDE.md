# Hướng dẫn Orchestration Service (BizCore ERP)

Orchestration service là **trung tâm điều phối và quan sát luồng nghiệp vụ phân tán**. Nó không chỉ ghi nhận timeline mà còn trực tiếp điều khiển các bước phức tạp như luồng thanh toán hóa đơn.

---

## 🧠 Cơ chế hoạt động (How it works)

Hệ thống triển khai song song hai mô hình để đảm bảo cả tính quan sát và tính toàn vẹn:

### 1. Saga Orchestrator (Luồng điều phối)
Sử dụng MassTransit State Machine để quản lý trạng thái của một giao dịch thanh toán.

- **Trigger**: Nhận `IPaymentInitiatedEvent` từ Payment Service.
- **Coordination**: 
  1. Gửi `IValidateInvoiceCommand` sang Invoice Service.
  2. Nếu hợp lệ: Gửi `IConfirmPaymentCommand` về Payment Service.
  3. Nếu không hợp lệ: Gửi `IRejectPaymentCommand` về Payment Service.
- **Chốt hạ**: Sau khi Payment service xác nhận thành công, nó phát `IPaymentCompletedEvent`. Lúc này Invoice service mới chính thức cập nhật trạng thái hóa đơn sang `Paid`.

### 2. Event Observer (Luồng quan sát)
Lắng nghe các domain events để xây dựng bức tranh tổng thể:
- **`ProcessFlow`**: Một phiên làm việc gắn liền với `InvoiceId`.
- **`FlowStep`**: Từng bước thực hiện (Invoice Created -> Payment Initiated -> Validated -> Completed).

---

## 🛠 Hướng dẫn Test luồng thanh toán (Step-by-Step)

Để kiểm tra tính đúng đắn của Orchestration, hãy thực hiện theo các bước sau:

### Bước 1: Khởi tạo dữ liệu (nếu chưa có)
Đảm bảo Invoice ID `f1d2c3b4-a5e6-4d7f-8e9a-0b1c2d3e4f5a` tồn tại trong hệ thống (mặc định đã được seed trong InvoiceDb và PaymentDb).

### Bước 2: Thực hiện thanh toán
Gọi API qua Gateway (cổng 5000):

```bash
curl -X POST http://localhost:5000/api/v1/payment/pay \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: test-key-101" \
  -H "Authorization: Bearer <TOKEN>" \
  -d '{
    "invoiceId": "f1d2c3b4-a5e6-4d7f-8e9a-0b1c2d3e4f5a",
    "amount": 1500,
    "paymentMethod": "CreditCard"
  }'
```
*Response dự kiến: `202 Accepted` kèm `paymentId`.*

### Bước 3: Kiểm tra trạng thái Payment (Polling)
```bash
curl -H "Authorization: Bearer <TOKEN>" http://localhost:5000/api/v1/payment/{paymentId}
```
Trạng thái sẽ chuyển từ `Processing` -> `Completed`.

### Bước 4: Kiểm tra Timeline tại Orchestration API
Xem lịch sử các bước đã thực hiện:
```bash
curl -H "Authorization: Bearer <TOKEN>" http://localhost:5000/api/v1/orchestration/flows/f1d2c3b4-a5e6-4d7f-8e9a-0b1c2d3e4f5a
```
Bạn sẽ thấy danh sách các steps kèm timestamp tương ứng.

### Bước 5: Xác nhận Invoice đã thanh toán
Kiểm tra trạng thái hóa đơn tại Invoice service:
```bash
curl -H "Authorization: Bearer <TOKEN>" http://localhost:5000/api/v1/invoices/f1d2c3b4-a5e6-4d7f-8e9a-0b1c2d3e4f5a
```
Trạng thái phải là `Paid`.

---

## 🔍 Troubleshooting (Xử lý sự cố)

1. **Payment bị kẹt ở "Processing"**:
   - Kiểm tra RabbitMQ Management (`http://localhost:15672` - guest/guest).
   - Xem queue `invoice-validate` có message nào bị kẹt không.
   - Kiểm tra log của `orchestration-api` xem có lỗi "Scheduler not found" không.

2. **Invoice không chuyển sang "Paid"**:
   - Kiểm tra xem `PaymentCompletedEvent` có được phát ra từ `payment-api` không.
   - Kiểm tra log của `invoice-api` xem có nhận được event này không.

3. **Lỗi 403 Forbidden**:
   - Đảm bảo TOKEN có permission `orchestration:view` (User `admin` mặc định có quyền này).

---

## Cấu hình Database

Kết nối SQL Server: `Server=sql-server;Database=OrchestrationDb;User Id=sa;Password=Password123!`.
- Bảng `ProcessFlows`: Lưu trạng thái hiện tại của luồng.
- Bảng `FlowSteps`: Lưu chi tiết các bước (snapshot event payload).

---

*Tài liệu này được cập nhật để phản ánh mô hình Saga Orchestrator mới. Mọi thay đổi về contract sự kiện cần được cập nhật đồng bộ tại đây.*
