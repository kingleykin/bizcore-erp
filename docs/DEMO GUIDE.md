# 🚀 Bizcore ERP - Demo Guide

Tài liệu này hướng dẫn các bước để trình diễn (demo) các tính năng chuyên nghiệp của hệ thống Bizcore ERP.

---

## 🛠️ 1. Chuẩn bị hệ thống

Chạy lệnh Docker để khởi động toàn bộ hạ tầng và các service:

```powershell
docker-compose up --build -d
```

*Đợi khoảng 1-2 phút để SQL Server và RabbitMQ sẵn sàng.*

---

## 🔐 2. Trình diễn Bảo mật & Phân quyền

### Bước 1: Đăng nhập (Lấy Token)

Sử dụng công cụ như Postman hoặc curl để gọi API login tại Gateway.

**Đăng nhập với quyền Admin:**

* **POST**: `http://localhost:5000/auth/login`
* **Body**: `{ "username": "admin", "password": "any" }`
* **Kết quả**: Bạn sẽ nhận được một chuỗi JWT Token. Hãy lưu lại mã này.

**Đăng nhập với quyền User thường:**

* **POST**: `http://localhost:5000/auth/login`
* **Body**: `{ "username": "user", "password": "any" }`

### Bước 2: Kiểm tra Phân quyền (Permission-based)

1. **Xem danh sách hóa đơn (Quyền `invoice:view`)**:
   * **GET**: `http://localhost:5000/invoice`
   * **Header**: `Authorization: Bearer <ADMIN_OR_USER_TOKEN>`
   * **Kết quả**: Thành công (200 OK). Cả Admin và User đều xem được.

2. **Tạo hóa đơn mới (Quyền `invoice:create` - Chỉ Admin)**:
   * **POST**: `http://localhost:5000/invoice`
   * **Header**: `Authorization: Bearer <ADMIN_TOKEN>`
   * **Body**: `{ "customerName": "Demo Customer", "amount": 1000 }`
   * **Kết quả**: Thành công (201 Created).
   * **Thử lại với USER_TOKEN**: Trả về **403 Forbidden**. Đây là điểm "ăn tiền" chứng minh phân quyền theo claim hoạt động hoàn hảo.

---

## ⚡ 3. Trình diễn Giới hạn lưu lượng (Rate Limiting)

Hệ thống đã được cấu hình giới hạn 100 request/phút.

**Cách demo:**

1. Sử dụng một công cụ benchmark (như `ab` hoặc lặp lại request nhanh bằng Postman).
2. Gửi liên tục các request đến `http://localhost:5000/invoice`.
3. **Kết quả**: Sau khi vượt ngưỡng, Gateway sẽ trả về **429 Too Many Requests**. Chứng minh hệ thống có khả năng tự bảo vệ trước tấn công spam.

---

## 📊 4. Trình diễn Observability & Distributed Tracing

Đây là tính năng "Pro" nhất để chứng minh khả năng quản lý hệ thống phân tán.

### Bước 1: Kiểm tra Correlation ID

1. Thực hiện một request bất kỳ (ví dụ: `GET /invoice`).
2. Kiểm tra **Response Header**. Bạn sẽ thấy header `X-Correlation-ID` kèm một mã GUID.
3. **Mô tả**: "Mọi request vào hệ thống đều được cấp một thẻ định danh duy nhất. Thẻ này sẽ đi theo request qua Gateway -> Service A -> Service B."

### Bước 2: Truy vết Log xuyên suốt

Xem log đồng thời của Gateway và Invoice:

```powershell
docker-compose logs -f
```

**Kết quả**: Bạn sẽ thấy cùng một mã Correlation ID xuất hiện trong log của cả Gateway và Invoice Service cho cùng một transaction. Chứng minh khả năng truy vết lỗi xuyên biên giới các service.

---

## 🏥 5. Trình diễn Operability (Health Checks)

Thử nghiệm khả năng tự giám sát của hệ thống:

* **Truy cập**: `http://localhost:5000/health` (Gateway)
* **Truy cập**: `http://localhost:5001/health` (Invoice)
* **Kết quả**: Trả về `Healthy`.
* **Ý nghĩa**: "Hệ thống sẵn sàng để tích hợp với các công cụ Orchestration như Kubernetes để tự động khởi động lại khi có service gặp sự cố."

---

## 🛡️ 5. Trình diễn Validation chuyên nghiệp

Đây là điểm mấu chốt để chứng minh tư duy Senior trong việc phân tách trách nhiệm validation.

### Bước 1: Kiểm tra Input Validation (FluentValidation)

Thử tạo hóa đơn với dữ liệu sai định dạng:

* **POST**: `http://localhost:5000/invoice`
* **Body**: `{ "customerName": "", "amount": -100 }`
* **Kết quả**: Trả về **400 Bad Request** với chi tiết lỗi rõ ràng (ví dụ: "Số tiền phải lớn hơn 0").

### Bước 2: Kiểm tra Domain Validation (Business Rules)

Thử tạo hóa đơn với số tiền hợp lệ về format nhưng vi phạm quy tắc nghiệp vụ:

* **POST**: `http://localhost:5000/invoice`
* **Body**: `{ "customerName": "VIP Customer", "amount": 2000000 }` (Vượt hạn mức 1 triệu)
* **Kết quả**: Trả về **400 Bad Request** với lỗi: "Hóa đơn không được vượt quá hạn mức 1,000,000 VNĐ."

## 🛡️ 6. Trình diễn Resilience (Global Exception Handling)

Thử gây lỗi để xem cách hệ thống phản ứng:

* Gửi dữ liệu sai quy tắc nghiệp vụ (Domain Validation).
* **Kết quả**: Trả về một JSON format chuẩn:

  ```json
  {
    "Code": "DOMAIN_ERROR",
    "Message": "...",
    "TraceId": "<CORRELATION_ID>",
    "Timestamp": "..."
  }
  ```

* **Ý nghĩa**: "Hệ thống không bao giờ trả về trang lỗi 500 thô sơ. Client luôn nhận được thông tin có cấu trúc kèm theo TraceId để hỗ trợ việc debug nhanh chóng."

---

## 🔁 7. Trình diễn Idempotency (Chống trùng giao dịch)

Mô phỏng trường hợp mạng lag và người dùng bấm thanh toán nhiều lần:

1. **Gửi request thanh toán lần 1**:
    * **URL**: `http://localhost:5000/api/v1/payment/pay`
    * **Header**: `X-Idempotency-Key: pay_unique_123`
    * **Body**: `{ "invoiceId": "...", "amount": 1000 }`
    * **Kết quả**: Thành công.
2. **Gửi request thanh toán lần 2** (Y hệt lần 1):
    * **Kết quả**: Hệ thống nhận ra key trùng và trả về kết quả thành công ngay lập tức mà **không xử lý trừ tiền hay bắn Event lần 2**.
    * **Ý nghĩa**: "Bảo vệ tài khoản người dùng và tính toàn vẹn của dữ liệu giao dịch."

---

## 📦 8. Trình diễn Outbox Pattern & Resilience

Giải thích cách hệ thống đảm bảo Event không bao giờ bị mất:

1. **Mô tả**: "Khi một hóa đơn được tạo, chúng tôi lưu hóa đơn VÀ message vào cùng một Database Transaction. Nếu DB lưu thành công, MassTransit Outbox sẽ đảm bảo message sớm muộn gì cũng được gửi đến RabbitMQ, ngay cả khi RabbitMQ bị sập tạm thời."
2. **Resilience**: "Tại Gateway, Polly được cấu hình để tự động Retry khi các microservices phía sau bị quá tải hoặc phản hồi chậm."

---

## 🏆 9. Điểm nhấn kiến trúc (Dành cho người xem)

Khi giới thiệu, hãy nhấn mạnh các điểm sau:

1. **Distributed Tracing**: "Hệ thống hỗ trợ tracing toàn diện qua Correlation ID, giúp giảm thời gian debug trong môi trường microservices từ vài giờ xuống vài giây."
2. **Double Validation & Consistency**: "Chúng tôi kết hợp FluentValidation và Domain Validation, đảm bảo dữ liệu luôn sạch và đúng nghiệp vụ."
3. **Resilience & Standardization**: "Mọi lỗi đều được chuẩn hóa format. Hệ thống có khả năng tự phục hồi và giám sát sức khỏe qua Health Checks."
4. **Performance Optimization**: "Áp dụng Memory Caching cho các báo cáo Dashboard, đảm bảo tốc độ phản hồi tối ưu cho người dùng cuối."
5. **Permission-based Auth**: "Hệ thống sử dụng claim-based để tránh nổ Role và hỗ trợ mở rộng linh hoạt."
6. **Double Validation Strategy**: "Chúng tôi áp dụng mô hình validation 2 lớp: FluentValidation ở cửa ngõ API để lọc rác, và Domain Validation ở trái tim của hệ thống để bảo vệ các quy tắc kinh doanh."
7. **Zero Trust**: "Mọi service đều tự validate Token, đảm bảo an toàn tối đa."
8. **Clean Code**: "Controller và Service cực kỳ sạch sẽ vì logic validation đã được đẩy ra đúng lớp (Middleware/Filter và Domain Entity)."
9. **Rate Limiting & Hardening**: "Gateway được cấu hình sẵn các lớp bảo vệ như một hệ thống Production thực thụ."
10. **Structured Logging**: "Log được ghi dưới dạng cấu trúc, sẵn sàng để đẩy vào ELK Stack để giám sát."
11. **Advanced EDA**: "Sử dụng Outbox Pattern để đạt được tính nhất quán cuối cùng (Eventual Consistency) một cách tin cậy."
12. **Industrial Standards**: "Hệ thống áp dụng đầy đủ API Versioning, Idempotency và Resilience (Polly) - những tiêu chuẩn bắt buộc của các hệ thống Microservices thực tế."
13. **Fault Tolerance**: "Thiết kế của chúng tôi chấp nhận lỗi xảy ra và có cơ chế tự phục hồi tự động."
