# Payment Service

## 🎯 Tổng quan (Overview)
**Payment Service** chịu trách nhiệm xử lý các giao dịch thanh toán trong hệ thống ERP. Nó thiết kế để trở thành nguồn chân lý (Single Source of Truth) đối với mọi luồng tiền vào/ra, và hoạt động hoàn toàn tách biệt khỏi Invoice Service.

## 🧱 Cấu trúc (Architecture)
* **Port nội bộ**: `5002`
* **Cơ sở dữ liệu**: `PaymentDb` (SQL Server)

## 🔄 Vòng đời của một giao dịch (Transaction Lifecycle)
1. User yêu cầu tạo thanh toán thông qua API Gateway.
2. Payment Service thực hiện logic ghi nhận dòng tiền và tạo một record với trạng thái ban đầu là `Completed`.
3. Payment Service dùng **Outbox Pattern** để đảm bảo đẩy chắc chắn `PaymentCompletedEvent` lên RabbitMQ thông báo cho toàn hệ thống biết dòng tiền đã được ghi nhận.
4. **Compensation**: Nếu Invoice Service thông báo không thể khớp nối thanh toán (bằng cách gửi `PaymentCompensationRequestedEvent`), Payment Service sẽ lắng nghe (Consume) event này và đổi trạng thái giao dịch thanh toán từ `Completed` thành `Reversed`.

## 🛡️ Idempotency (Tính lũy đẳng)
Payment Service là hệ thống tài chính, do đó tính lũy đẳng là cực kỳ quan trọng:
- Mọi request tạo thanh toán (`POST /pay`) đều yêu cầu Header `X-Idempotency-Key`.
- Service sử dụng Middleware/Behavior để lưu vết Key này. Nếu User cố tình ấn nút thanh toán nhiều lần hoặc do lỗi mạng retry, Payment Service chỉ xử lý duy nhất một lần.

## 🔗 Endpoint API (API Endpoints)
| Endpoint | Method | Chức năng | Phân quyền yêu cầu |
| --- | --- | --- | --- |
| `/api/v1/payment/pay` | POST | Khởi tạo giao dịch thanh toán | `Payment.Create` |
| `/api/v1/payment` | GET | Lịch sử thanh toán | `Payment.View` |
