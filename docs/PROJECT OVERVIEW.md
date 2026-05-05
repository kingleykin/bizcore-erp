# 📘 1. PROJECT OVERVIEW (Single Source of Truth)

## 🎯 Mục tiêu

Xây dựng hệ thống ERP demo kiến trúc Microservices chuyên nghiệp, tập trung vào luồng nghiệp vụ cốt lõi: **Hóa đơn -> Thanh toán -> Báo cáo**.

## 🏗️ Cấu trúc thư mục

```text
bizcore-erp/
├── src/
│   ├── Gateway/
│   │   └── Gateway.API/ (YARP Gateway)
│   ├── Services/
│   │   ├── Invoice/ (Quản lý hóa đơn)
│   │   ├── Payment/ (Xử lý thanh toán)
│   │   └── Report/  (Tổng hợp báo cáo)
│   ├── BuildingBlocks/
│   │   └── Bizcore.BuildingBlocks/ (Shared Library: Contracts, Events)
│   └── WebUI/ (React App)
├── Bizcore.slnx (Solution file)
├── docker-compose.yml
└── docs/ (Tài liệu dự án)
```

## 🧱 Kiến trúc Kỹ thuật

* **Microservices**: 3 services tách biệt theo Domain.
* **API Gateway**: YARP (Yet Another Reverse Proxy) port 5000.
* **Architecture**: Domain-Driven Lite (4-Layer: Domain, Application, Infrastructure, API) kết hợp **Event-Driven Architecture (EDA)**.
* **Database**: SQL Server (Shared Database cho giai đoạn demo).
* **Message Broker**: RabbitMQ (sử dụng MassTransit) để giao tiếp bất đồng bộ giữa các service.
* **Logging**: Hệ thống ghi log tập trung sử dụng **Serilog**, hỗ trợ truy vết lỗi và giám sát hiệu năng thời gian thực.
* **Validation**:
  * **FluentValidation**: Kiểm tra tính đúng đắn của dữ liệu đầu vào (format, độ dài, khoảng giá trị) ngay tại tầng API.
  * **Domain Validation**: Kiểm tra các quy tắc nghiệp vụ chuyên sâu (Business Rules) ngay tại tầng Domain, đảm bảo tính toàn vẹn của dữ liệu trong mọi tình huống.

## 🔗 Luồng nghiệp vụ (Flow)

1. **Payment**: Thực hiện thanh toán -> Publish `PaymentCompletedEvent` lên RabbitMQ.
2. **Invoice Service**: Consume Event -> Cập nhật trạng thái Hóa đơn sang `Paid`.
3. **Report**: Xem Dashboard doanh thu cập nhật thời gian thực.

---

# 📘 2. DOMAIN DESIGN

## 📦 Entities

### Invoice

```json
{
  "Id": "guid",
  "CustomerName": "string",
  "Amount": "decimal",
  "Status": "Pending (0) | Paid (1) | Cancelled (2)",
  "CreatedAt": "datetime"
}
```

### Payment

```json
{
  "Id": "guid",
  "InvoiceId": "guid",
  "Amount": "decimal",
  "PaymentDate": "datetime"
}
```

---

# 📘 3. API CONTRACT

| Service | Method | Endpoint | Quyền yêu cầu (Policy) | Mô tả |
| :--- | :--- | :--- | :--- | :--- |
| **Auth** | POST | `/auth/login` | Không yêu cầu | Đăng nhập |
| **Invoice** | GET | `/invoice` | `Invoice.View` | Lấy danh sách hóa đơn |
| **Invoice** | POST | `/invoice` | `Invoice.Create` | Tạo hóa đơn |
| **Payment** | POST | `/payment/pay` | `Payment.Create` | Thanh toán hóa đơn |
| **Report** | GET | `/report/summary` | `Report.View` | Báo cáo tổng hợp |

---

# 📘 4. GATEWAY ROUTING (YARP)

| Path | Destination | Policy áp dụng |
| :--- | :--- | :--- |
| `/invoice/{**catch-all}` | `http://invoice-api:8080` | RateLimit, Auth |
| `/payment/{**catch-all}` | `http://payment-api:8080` | RateLimit, Auth |
| `/report/{**catch-all}` | `http://report-api:8080` | RateLimit, Auth |

---

# 📘 5. DEVELOPMENT CHECKLIST

## 🟢 Phase 1 & 2: Backend & Infrastructure (Hoàn thành)

* [x] Khởi tạo Solution và Cấu trúc thư mục chuẩn.
* [x] Triển khai 3 Microservices với 4 lớp (Domain, Application, Infra, API).
* [x] Thiết lập Database Schema & Shared Context.

## 🟡 Phase 3: Integration & UI (Hoàn thành)

* [x] Cấu hình YARP Gateway & CORS.
* [x] Xây dựng WebUI (React/Vite) giao diện Premium.
* [x] Test luồng End-to-End thành công.
* [x] Cấu hình Dockerization (Multi-stage builds) cho toàn bộ hệ thống.
* [x] Triển khai Permission-based Authorization.
* [x] Tích hợp Serilog tập trung.
* [x] Thiết lập Rate Limiting & Security Hardening.

---

# 🚀 6. HƯỚNG DẪN CHẠY DỰ ÁN

Hệ thống đã được tối ưu hóa để chạy bằng Docker:

1. **Docker Compose**: Chạy lệnh duy nhất tại thư mục gốc:

   ```powershell
   docker-compose up --build -d
   ```

2. **Frontend**: `cd src/WebUI` sau đó `npm install`, `npm run dev`.
3. **Truy cập**:
   * **API Gateway**: `http://localhost:5000`
   * **RabbitMQ UI**: `http://localhost:15672` (guest/guest)
   * **SQL Server**: `localhost,1433` (sa/Password123!)

---
*Cập nhật lần cuối: 05/05/2026 - Nâng cấp hệ thống bảo mật Enterprise.*
