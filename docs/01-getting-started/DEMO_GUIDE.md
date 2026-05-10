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

* **POST**: `http://localhost:5001/auth/login`
* **Body**: `{ "username": "admin", "password": "any" }`
* **Kết quả**: Bạn sẽ nhận được một chuỗi JWT Token. Hãy lưu lại mã này.

**Đăng nhập với quyền User thường:**

* **POST**: `http://localhost:5001/auth/login`
* **Body**: `{ "username": "user", "password": "any" }`

### Bước 2: Kiểm tra Phân quyền (Permission-based)

1. **Xem danh sách hóa đơn (Quyền `invoice:view`)**:
   * **GET**: `http://localhost:5001/invoice`
   * **Header**: `Authorization: Bearer <ADMIN_OR_USER_TOKEN>`
   * **Kết quả**: Thành công (200 OK). Cả Admin và User đều xem được.

2. **Tạo hóa đơn mới (Quyền `invoice:create` - Chỉ Admin)**:
   * **POST**: `http://localhost:5001/invoice`
   * **Header**: `Authorization: Bearer <ADMIN_TOKEN>`
   * **Body**: `{ "customerName": "Demo Customer", "amount": 1000 }`
   * **Kết quả**: Thành công (201 Created).
   * **Thử lại với USER_TOKEN**: Trả về **403 Forbidden**. Đây là điểm "ăn tiền" chứng minh phân quyền theo claim hoạt động hoàn hảo.

---

## ⚡ 3. Trình diễn Giới hạn lưu lượng (Rate Limiting)

Hệ thống đã được cấu hình giới hạn 100 request/phút.

**Cách demo:**

1. Sử dụng một công cụ benchmark (như `ab` hoặc lặp lại request nhanh bằng Postman).
2. Gửi liên tục các request đến `http://localhost:5001/invoice`.
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

* **Truy cập**: `http://localhost:5001/health` (Gateway)
* **Truy cập**: `http://localhost:5001/health` (Invoice)
* **Kết quả**: Trả về `Healthy`.
* **Ý nghĩa**: "Hệ thống sẵn sàng để tích hợp với các công cụ Orchestration như Kubernetes để tự động khởi động lại khi có service gặp sự cố."

---

## 🛡️ 5. Trình diễn Validation chuyên nghiệp

Đây là điểm mấu chốt để chứng minh tư duy Senior trong việc phân tách trách nhiệm validation.

### Bước 1: Kiểm tra Input Validation (FluentValidation)

Thử tạo hóa đơn với dữ liệu sai định dạng:

* **POST**: `http://localhost:5001/invoice`
* **Body**: `{ "customerName": "", "amount": -100 }`
* **Kết quả**: Trả về **400 Bad Request** với chi tiết lỗi rõ ràng (ví dụ: "Số tiền phải lớn hơn 0").

### Bước 2: Kiểm tra Domain Validation (Business Rules)

Thử tạo hóa đơn với số tiền hợp lệ về format nhưng vi phạm quy tắc nghiệp vụ:

* **POST**: `http://localhost:5001/invoice`
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
    * **URL**: `http://localhost:5001/api/v1/payment/pay`
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

## 📊 10. Trình diễn Observability & Monitoring (Loki, Prometheus, Grafana)

Đây là điểm nhấn thứ hai về tính chuyên nghiệp của hệ thống enterprise-grade.

### Bước 1: Truy cập Grafana Dashboard

1. **Mở URL**: `http://localhost:3001`
2. **Đăng nhập**: admin/admin
3. **Giải thích**: "Grafana là hub trung tâm để visualize logs và metrics của toàn bộ hệ thống."

### Bước 2: Xem Logs từ Loki

1. **Trong Grafana**, click **Explore** (ngoài cùng bên trái).
2. **Chọn datasource**: "Loki" (nếu chưa có, add datasource mới với URL `http://loki:3100`).
3. **Viết query**: 
   ```
   {service="invoice-api"}
5. **Mô tả**: "Mỗi dòng log chứa structured data, bao gồm timestamp, service, level, và message. Chúng ta có thể filter theo service, environment, hay error level."

### Bước 3: Kiểm tra Metrics từ Prometheus

1. **Trong Grafana**, click **Explore**.
2. **Chọn datasource**: "Prometheus" (nếu chưa có, add datasource mới với URL `http://prometheus:9090`).
3. **Viết query Prometheus**:
   ```
   rate(http_requests_received_total{job="invoice-api"}[5m])
   ```
4. **Kết quả**: Thấy tỷ lệ request đến Invoice API trong 5 phút qua.
5. **Mô tả**: "Prometheus thu thập metrics HTTP từ mỗi service. Chúng ta có thể query request rate, latency, error rate, và nhiều metrics khác."

### Bước 4: Xem Correlation ID trong Logs

1. **Thực hiện một API request** (ví dụ: POST hóa đơn).
2. **Copy Correlation ID** từ response header: `X-Correlation-ID`.
3. **Trong Loki query**, thêm filter:
   ```
   {service=~".*-api"} | json | trace_id="<CORRELATION_ID>"
   ```
4. **Kết quả**: Xem tất cả logs từ tất cả services liên quan đến request này.
5. **Mô tả**: "Correlation ID cho phép chúng ta theo dõi một request xuyên suốt chuỗi microservices. Nó trở nên cực kỳ mạnh mẽ khi hệ thống có 10+ services."

### Bước 5: Tạo Dashboard Simple

1. **Trong Grafana**, click **+** -> **Dashboard**.
2. **Add Panel** -> **Loki** -> Query:
   ```
   {service="payment-api"} |= "error"
   ```
3. **Đặt tên**: "Payment API Errors".
4. **Thêm Panel thứ 2** -> **Prometheus** -> Query:
   ```
   rate(http_requests_received_total[5m])
   ```
5. **Đặt tên**: "HTTP Request Rate".
6. **Lưu Dashboard**: `Ctrl+S`.
7. **Mô tả**: "Dashboard này giúp chúng ta nhanh chóng phát hiện các sự cố hoặc anomaly. Các KPI chính được trực quan hóa trong một màn hình duy nhất."

### Bước 6: Verify Prometheus Scraping

1. **Truy cập**: `http://localhost:9090`
2. **Click**: Targets (thanh menu phía trên).
3. **Kết quả**: Xem danh sách tất cả các services mà Prometheus đang scrape metrics từ. Chúng sẽ ở trạng thái "UP" nếu khỏe mạnh.
4. **Mô tả**: "Prometheus chủ động kéo metrics từ mỗi service qua endpoint `/metrics`. Nếu một service bị down, status sẽ chuyển sang 'DOWN' và chúng ta sẽ được cảnh báo ngay."

---

## 🔝 11. Điểm nhấn tổng kết

## 🔝 11. Điểm nhấn tổng kết

Khi giới thiệu, hãy nhấn mạnh các điểm sau:

1. **Distributed Tracing**: "Hệ thống hỗ trợ tracing toàn diện qua Correlation ID, giúp giảm thời gian debug trong môi trường microservices từ vài giờ xuống vài phút."
2. **Centralized Logging with Loki**: "Loki cung cấp khả năng tìm kiếm và filter logs có cấu trúc từ tất cả containers. Không cần SSH vào từng server để xem logs."
3. **Metrics & Visualization**: "Prometheus thu thập metrics performance từ tất cả services, Grafana visualize chúng thành dashboard thực thi. Chúng ta có thể phát hiện bottleneck trong 5 giây."
4. **Full Stack Observability**: "Combination của Logs + Metrics + Traces tạo thành một hệ thống observability hoàn chỉnh, cho phép chúng ta hiểu rõ hành vi của toàn bộ hệ thống."
5. **Production-Ready**: "Stack monitoring này được sử dụng bởi hàng ngàn công ty để monitor production systems hàng ngày."
6. **Double Validation & Consistency**: "Chúng tôi kết hợp FluentValidation và Domain Validation, đảm bảo dữ liệu luôn sạch và đúng nghiệp vụ."
7. **Resilience & Standardization**: "Mọi lỗi đều được chuẩn hóa format. Hệ thống có khả năng tự phục hồi và giám sát sức khỏe qua Health Checks."
8. **Performance Optimization**: "Áp dụng Memory Caching cho các báo cáo Dashboard, đảm bảo tốc độ phản hồi tối ưu cho người dùng cuối."
9. **Permission-based Auth**: "Hệ thống sử dụng claim-based để tránh nổ Role và hỗ trợ mở rộng linh hoạt."
10. **Double Validation Strategy**: "Chúng tôi áp dụng mô hình validation 2 lớp: FluentValidation ở cửa ngõ API để lọc rác, và Domain Validation ở trái tim của hệ thống để bảo vệ các quy tắc kinh doanh."
11. **Zero Trust**: "Mọi service đều tự validate Token, đảm bảo an toàn tối đa."
12. **Clean Code**: "Controller và Service cực kỳ sạch sẽ vì logic validation đã được đẩy ra đúng lớp (Middleware/Filter và Domain Entity)."
13. **Rate Limiting & Hardening**: "Gateway được cấu hình sẵn các lớp bảo vệ như một hệ thống Production thực thụ."
14. **Structured Logging**: "Log được ghi dưới dạng cấu trúc, sẵn sàng để đẩy vào ELK Stack hoặc Loki để giám sát."
15. **Advanced EDA**: "Sử dụng Outbox Pattern để đạt được tính nhất quán cuối cùng (Eventual Consistency) một cách tin cậy."
16. **Industrial Standards**: "Hệ thống áp dụng đầy đủ API Versioning, Idempotency, Resilience (Polly), và Observability - những tiêu chuẩn bắt buộc của các hệ thống Microservices thực tế."
17. **Fault Tolerance**: "Thiết kế của chúng tôi chấp nhận lỗi xảy ra và có cơ chế tự phục hồi tự động."
18. **Modern DevOps Stack**: "Tích hợp đầy đủ containerization (Docker), orchestration (docker-compose), monitoring (Loki, Prometheus, Grafana), message broker (RabbitMQ), và databases (SQL Server)."

---

## 🎯 Kết luận

Hệ thống BizCore ERP đã được cấu hình đầy đủ monitoring stack production-ready với:

✅ **Loki**: Centralized logging với service discovery tự động
✅ **Prometheus**: Metrics collection từ tất cả microservices  
✅ **Grafana**: Visualization dashboard cho logs và metrics
✅ **Promtail**: Log shipping với Docker service discovery

**Query mạnh mẽ**:
```
{service="payment-api"} |= "error"
{service=~".*-api"} | json | trace_id="<CORRELATION_ID>"
```
