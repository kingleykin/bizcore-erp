# Audit Service

## 🎯 Tổng quan (Overview)
**Audit Service** là hệ thống theo dõi và giám sát tập trung (Centralized Audit) của BizCore ERP. Nó sinh ra để giải quyết bài toán Compliance (tuân thủ quy định), Security Analysis (phân tích bảo mật), và Forensic (điều tra sự cố). Đây là một hệ thống thiết kế theo chuẩn Enterprise-grade với đặc tính lưu trữ không thể xóa sửa (Immutable) và chống thay đổi (Tamper-proofing).

## 🧱 Cấu trúc (Architecture)
* **Port nội bộ**: `5006`
* **Cơ sở dữ liệu**: `AuditDb` (SQL Server)
* **Cơ chế thu thập**: 
  * Asynchronous thông qua RabbitMQ (MassTransit Consumer).
  * Sử dụng Retry Policy và Dead Letter Queue (DLQ) để không bị mất dữ liệu kiểm toán khi có lỗi mạng.

## 🛡️ Thiết kế bảo mật cao (High Security Design)
Hệ thống Audit tuân thủ các quy tắc bảo mật khắt khe nhất:
1. **Append-Only DB**: Bảng `AuditEntries` trong cơ sở dữ liệu được cấu hình `DENY UPDATE/DELETE`. Không ai có thể thay đổi dữ liệu đã ghi xuống.
2. **Hash Chain (Tamper Detection)**: Tương tự như Blockchain, mỗi một Audit Record được băm SHA-256 kèm theo tham chiếu (PreviousHash) đến record trước đó. Bất kỳ nỗ lực nào can thiệp trực tiếp vào Database đều làm đứt gãy chuỗi Hash, và hệ thống sẽ phát hiện ngay lập tức qua `IntegrityVerificationJob`.
3. **Sensitive Data Masking**: Các trường như `password`, `token`, `cardNumber` tự động bị che khuất (`***`) thông qua tiện ích `SensitiveFieldMasker` trước khi được lưu vào JSON Before/After.

## 🚀 Kiến trúc Hybrid Trigger
Audit Service thu thập dữ liệu qua 2 luồng song song:
1. **Application Layer (Explicit)**: Các Service chủ động `IBus.Publish` các event mang ý nghĩa nghiệp vụ (Ví dụ: `Auth.Login.Success` ở mức Security, `DataReversal.Invoice.CustomerName` ở mức Compliance).
2. **EF Core Interceptor (Implicit)**: `AuditSaveChangesInterceptor` tự động bắt mọi thay đổi (Before/After) của bất kỳ Entity nào implement `IAuditable` và gửi đi dưới dạng Field-level audit (Mức Operational).

## 🔄 Reversal Tracking (Theo dõi khôi phục)
Audit Service đóng vai trò là kho dữ liệu (nguồn `BeforeJson`) cho các Domain Service chạy thuật toán **Restore Diff Engine**. Nó không trực tiếp thực hiện ghi đè dữ liệu. 
Khi Domain Service (ví dụ: Invoice) thực hiện khôi phục thành công một field, nó sẽ gọi PATCH về Audit Service để đánh dấu bản ghi gốc là `IsReversed = true`, kèm theo `ReversedByEntryId` và `ReversalReason` để đảm bảo traceability (một lỗi sai không được phép reverse nhiều lần).

## ⏳ Chiến lược lưu trữ (Retention Policy)
Audit Data phình to rất nhanh, hệ thống thiết kế lưu trữ theo Tier (phân lớp):
- **Hot Storage** (`AuditEntries`): Lưu trữ dữ liệu trong vòng 180 ngày để truy vấn nhanh cho mục đích vận hành.
- **Warm Storage** (`ArchiveEntries`): Dữ liệu cũ hơn 180 ngày sẽ được `RetentionCleanupJob` (Hangfire) tự động chuyển qua bảng lưu trữ dài hạn (Archive).

## 🔗 Endpoint API (API Endpoints)
| Endpoint | Method | Chức năng | Phân quyền yêu cầu |
| --- | --- | --- | --- |
| `/api/v1/audit` | GET | Truy vấn Audit (có filter theo hành động, user, thời gian) | `Audit.View` |
| `/api/v1/audit/{id}` | GET | Chi tiết một bản ghi Audit | `Audit.View` |
| `/api/v1/audit/verify-integrity` | GET | Chạy thuật toán xác minh toàn vẹn toàn bộ chuỗi Hash | `Audit.View` |
| `/api/v1/audit/{id}/mark-reversed` | PATCH | (Internal) Đánh dấu Audit Entry đã được reverse | `Audit.View` |
