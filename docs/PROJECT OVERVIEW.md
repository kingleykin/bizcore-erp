# 📘 1. PROJECT OVERVIEW (Single Source of Truth)

## 🎯 Mục tiêu

Xây dựng hệ thống CRM demo kiến trúc Microservices chuyên nghiệp, tập trung vào luồng nghiệp vụ cốt lõi: **Hóa đơn -> Thanh toán -> Báo cáo**.

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

| Service | Method | Endpoint | Mô tả |
| :--- | :--- | :--- | :--- |
| **Invoice** | GET | `/invoice` | Lấy danh sách hóa đơn |
| **Invoice** | POST | `/invoice` | Tạo mới hóa đơn |
| **Payment** | POST | `/payment/pay` | Xử lý thanh toán |
| **Report** | GET | `/report/summary` | Lấy số liệu dashboard |

---

# 📘 4. GATEWAY ROUTING (YARP)

| Path | Destination |
| :--- | :--- |
| `/invoice/{**catch-all}` | `http://localhost:5001` |
| `/payment/{**catch-all}` | `http://localhost:5002` |
| `/report/{**catch-all}` | `http://localhost:5003` |

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

---

# 🚀 6. HƯỚNG DẪN CHẠY DỰ ÁN

1. **Database**: `docker-compose up -d`
2. **Backend**: Mở `src/Bizcore.slnx`, chạy Multi-startup cho cả 4 project (Port 5000-5003).
3. **Frontend**: `cd src/WebUI`, `npm install`, `npm run dev`.

---
*Cập nhật lần cuối: 05/05/2026 - Dự án đã hoàn thành cấu trúc chuẩn.*
