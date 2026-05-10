# Invoice Service

## 🎯 Tổng quan (Overview)
**Invoice Service** là service chịu trách nhiệm quản lý toàn bộ vòng đời của Hóa đơn (Invoice) trong hệ thống ERP. Service này đóng vai trò trung tâm lưu trữ thông tin số tiền cần thanh toán và thông tin khách hàng.

## 🧱 Cấu trúc (Architecture)
* **Port nội bộ**: `5001`
* **Cơ sở dữ liệu**: `InvoiceDb` (SQL Server)
* **Mô hình**: Bounded Context độc lập, tuân thủ Clean Architecture (Domain, Application, Infrastructure, API).

## 🔄 Luồng tương tác bất đồng bộ (Async Flow)
Invoice Service không gọi API trực tiếp sang Payment Service mà giao tiếp qua **Event-Driven Architecture (EDA)** với RabbitMQ:
1. Khi khách hàng thanh toán thành công, Payment Service gửi đi `PaymentCompletedEvent`.
2. Invoice Service lắng nghe event này, tìm hóa đơn tương ứng và cập nhật trạng thái từ `Pending` sang `Paid`.
3. Nếu vì một lý do nghiệp vụ nào đó Invoice Service không thể cập nhật (Ví dụ: Hóa đơn đã bị hủy, hoặc ID Hóa đơn không tồn tại), nó sẽ kích hoạt tiến trình **Compensation** (Rollback nghiệp vụ) bằng cách publish ngược lại `PaymentCompensationRequestedEvent` để Payment Service tiến hành hủy/đảo ngược thanh toán đó.

## 🛠️ Outbox Pattern & Idempotency
- **Outbox Pattern**: Mọi event được publish ra ngoài (như `PaymentCompensationRequestedEvent` hay `AuditEvent` cho Data Reversal) đều được lưu vào Outbox table cùng transaction với Database. MassTransit sẽ lo việc đẩy event lên RabbitMQ sau khi commit, đảm bảo không bao giờ bị mất event (Eventual Consistency).
- **Idempotency**: Tránh xử lý trùng lặp. Các API nhận đầu vào tạo mới thường kiểm tra `Idempotency-Key` để không tạo hóa đơn trùng lặp do lỗi mạng từ phía Client.

## 🛡️ Audit-Assisted Recovery (Khôi phục dữ liệu)
Invoice Service implement mô hình khôi phục dữ liệu an toàn dưới sự hỗ trợ của Audit Service:
1. Khi Admin muốn khôi phục (Reverse) một Invoice bị sai sót, Client gọi `/restore-suggestion`. Invoice Service fetch `BeforeJson` từ Audit Service, đưa qua `RestoreDiffEngine` so sánh với DB hiện tại và áp dụng `InvoiceReversalPolicy` (chỉ cho phép sửa field meta như CustomerName, chặn field tài chính như Amount).
2. Khi Admin chốt khôi phục, Client gọi `/restore-field`. Invoice Service thực thi hàm domain (VD: `RestoreCustomerName()`), kiểm tra concurrency qua `RowVersion` để chặn Stale Data, publish `AuditEvent` (DataReversal) và gọi Audit Service để mark-reversed bản ghi gốc.

## 🔗 Endpoint API (API Endpoints)
| Endpoint | Method | Chức năng | Phân quyền yêu cầu |
| --- | --- | --- | --- |
| `/api/v1/invoice` | GET | Danh sách hóa đơn | `Invoice.View` |
| `/api/v1/invoice` | POST | Tạo hóa đơn mới | `Invoice.Create` |
| `/api/v1/invoice/{id}` | GET | Chi tiết hóa đơn | `Invoice.View` |
| `/api/v1/invoice/{id}/restore-suggestion` | GET | Lấy gợi ý khôi phục dữ liệu (từ Audit log) | `Audit.View` |
| `/api/v1/invoice/{id}/restore-field` | POST | Thực thi khôi phục 1 field về giá trị cũ | `Audit.View` |
