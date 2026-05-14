# Orchestration Service (Saga/Choreography Tracker)

## 🎯 Tổng quan (Overview)
**Orchestration Service** đóng vai trò là "người quan sát" (Observer) trong hệ thống Event-Driven. Khi kiến trúc Microservices chuyển sang giao tiếp bằng Event, luồng giao dịch bị phân mảnh (Choreography). Orchestration Service ra đời với tư cách là **Read-side API**, chuyên lắng nghe mọi Event trong hệ thống để lắp ghép lại thành một bức tranh luồng xử lý liền mạch.

## 🧱 Cấu trúc (Architecture)
* **Port nội bộ**: `5004`
* **Cơ sở dữ liệu**: `OrchestrationDb` (SQL Server)
* **Standardized Persistence**: Sử dụng `BaseEntity` (Id, CreatedAt, UpdatedAt, RowVersion) cho mọi thực thể bao gồm Sagas và Flow Tracking.
* **Thành phần**: Read-side API kết hợp MassTransit Sagas.

## 🔄 Cơ chế hoạt động (How it works)
1. Orchestration Service có các Consumer lắng nghe tất cả Domain Events (Ví dụ: `InvoiceCreatedEvent`, `PaymentCompletedEvent`, `PaymentCompensationRequestedEvent`).
2. Nó kết nối các Event này lại với nhau dựa vào một tham số chung như `InvoiceId` hoặc `CorrelationId`.
3. Lưu trữ chuỗi các hành động này dưới dạng timeline: `ProcessFlow` (luồng tổng) và `FlowStep` (từng bước thực hiện kèm thời gian và kết quả).

## 💡 Giá trị (Value Proposition)
- Giúp Dev/Ops nhìn thấy ngay lập tức "Hóa đơn này đang tắc ở đâu?" mà không cần đi bới log từng Microservice.
- Hỗ trợ xây dựng giao diện "Trạng thái đơn hàng" cho người dùng cuối (ví dụ: Đang chờ thanh toán -> Đã thanh toán -> Hoàn tất).

## 🔗 Endpoint API (API Endpoints)
| Endpoint | Method | Chức năng | Phân quyền yêu cầu |
| --- | --- | --- | --- |
| `/api/v1/orchestration/flows` | GET | Lấy danh sách các luồng đang chạy | `Orchestration.View` |
| `/api/v1/orchestration/flows/{id}` | GET | Chi tiết các bước đã chạy của một luồng | `Orchestration.View` |
