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

## 📊 4. Trình diễn Event-Driven & Logging

### Bước 1: Thanh toán hóa đơn (Flow EDA)

1. **POST**: `http://localhost:5000/payment/pay`
2. **Body**: `{ "invoiceId": "<ID_HOA_DON_MOI_TAO>", "amount": 1000 }`
3. **Luồng hoạt động**:
   * `Payment.API` xử lý -> Bắn Event lên RabbitMQ.
   * `Invoice.API` nhận Event -> Tự động chuyển trạng thái hóa đơn sang `Paid`.

### Bước 2: Kiểm tra Log tập trung (Serilog)

Xem log của các container để thấy sự phối hợp:

```powershell
docker-compose logs -f invoice-api
```

*Bạn sẽ thấy các dòng log được format đẹp mắt bởi Serilog, ghi lại quá trình nhận Event và cập nhật Database.*

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

---

## 🏆 6. Điểm nhấn kiến trúc (Dành cho người xem)

Khi giới thiệu, hãy nhấn mạnh các điểm sau:

1. **Permission-based Auth**: "Hệ thống sử dụng claim-based để tránh nổ Role và hỗ trợ mở rộng linh hoạt."
2. **Double Validation Strategy**: "Chúng tôi áp dụng mô hình validation 2 lớp: FluentValidation ở cửa ngõ API để lọc rác, và Domain Validation ở trái tim của hệ thống để bảo vệ các quy tắc kinh doanh."
3. **Zero Trust**: "Mọi service đều tự validate Token, đảm bảo an toàn tối đa."
4. **Clean Code**: "Controller và Service cực kỳ sạch sẽ vì logic validation đã được đẩy ra đúng lớp (Middleware/Filter và Domain Entity)."
5. **Rate Limiting & Hardening**: "Gateway được cấu hình sẵn các lớp bảo vệ như một hệ thống Production thực thụ."
6. **Structured Logging**: "Log được ghi dưới dạng cấu trúc, sẵn sàng để đẩy vào ELK Stack để giám sát."
