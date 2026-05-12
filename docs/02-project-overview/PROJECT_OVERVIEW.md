# 📘 1. PROJECT OVERVIEW (Single Source of Truth)

## 🎯 Mục tiêu

Xây dựng hệ thống ERP **Production-ready** theo kiến trúc Microservices chuyên nghiệp, tích hợp bảo mật toàn diện với luồng nghiệp vụ cốt lõi: **Identity -> Hóa đơn -> Thanh toán -> Báo cáo**.

## 🏗️ Cấu trúc thư mục

```text
bizcore-erp/
├── src/
│   ├── Gateway/
│   │   └── Gateway.API/ (YARP Gateway)
│   ├── Services/
│   │   ├── Admin/       (Identity, Org Master Data, Global Settings)
│   │   ├── Accounting/  (ACC Core & Batch, Ledger, Posting Engine)
│   │   ├── Invoice/     (Quản lý hóa đơn - Phân hệ AR/AP)
│   │   ├── Payment/     (Xử lý thanh toán - Phân hệ Treasury)
│   │   ├── Report/      (Tổng hợp báo cáo, Redis)
│   │   ├── Audit/       (Centralized Audit Service, Immutable log, Hash chain)
│   │   └── Orchestration/ (Theo dõi luồng giao dịch qua event)
│   ├── BuildingBlocks/
│   │   └── Bizcore.BuildingBlocks/ (Shared Library: Contracts, Events)
│   └── WebUI/ (React App)
├── Bizcore.slnx (Solution file)
├── docker-compose.yml
└── docs/ (Tài liệu dự án)
```

## 🧱 Kiến trúc Kỹ thuật

* **Microservices**: Các service core (Admin, Accounting, Invoice, Payment, Report) + **Audit** (Compliance/Security) + **Orchestration** (read-side theo dõi luồng qua event). Chi tiết: [ORCHESTRATION_GUIDE.md](../03-architecture/ORCHESTRATION_GUIDE.md).
* **API Gateway**: YARP (Yet Another Reverse Proxy) port 5001.
* **Architecture**: Domain-Driven Lite (4-Layer: Domain, Application, Infrastructure, API) kết hợp **Event-Driven Architecture (EDA)**.
* **Database**: SQL Server (Sử dụng các Database logic độc lập trên cùng 1 server: IdentityDb, InvoiceDb, PaymentDb, ReportDb, AuditDb, OrchestrationDb).
* **Message Broker**: RabbitMQ (sử dụng MassTransit) để giao tiếp bất đồng bộ giữa các service.
  * **Logging & Observability**:
    * **LGTM Stack**: Tích hợp toàn diện Loki (Logs), Grafana (Dashboard), Tempo (Traces) và Prometheus (Metrics).
    * **Serilog + Loki**: Ghi log tập trung và lưu trữ logs có cấu trúc trong Loki.
    * **Prometheus**: Thu thập metrics từ tất cả các microservices (HTTP request latency, count, size).
    * **Grafana**: Trực quan hóa logs và metrics từ Loki và Prometheus trên các dashboard chuyên nghiệp.
    * **Promtail**: Đơn vị log shipping để đẩy logs từ containers lên Loki.
    * **OpenTelemetry**: Chuẩn hóa distributed tracing xuyên suốt HTTP -> gRPC -> RabbitMQ -> SQL.
    * **Correlation ID**: Tự động gán và truyền ID (`X-Correlation-ID`) qua toàn bộ các service để truy vết.
* **Validation**:
  * **FluentValidation**: Kiểm tra tính đúng đắn của dữ liệu đầu vào (format, độ dài, khoảng giá trị) ngay tại tầng API.
  * **Domain Validation**: Kiểm tra các quy tắc nghiệp vụ chuyên sâu (Business Rules) ngay tại tầng Domain, đảm bảo tính toàn vẹn của dữ liệu trong mọi tình huống.
* **Security & Compliance (Audit)**:

  ### 4. Centralized Audit & Data Correction (Reversal)

  **Vấn đề:** Thay vì lưu Snapshot trực tiếp tại Data Database gây phình to database và khó truy vết chéo, dự án áp dụng Centralized Audit. Cùng với đó, hệ thống cần hỗ trợ Admin sửa lỗi nhập liệu (Reversal) một cách an toàn mà không ảnh hưởng tính nhất quán tài chính.
  **Giải pháp:**
  * Audit: Sử dụng cả Application Layer (Business Events) để publish `AuditEvent` qua RabbitMQ tới `Audit.API`.
  * Integrity Check: Áp dụng Hash chain (SHA-256) và chế độ Append-Only để đảm bảo tính bất biến của lịch sử.
  * **Audit-Assisted Recovery**: Việc khôi phục (Restore) không ghi đè Snapshot mù quáng. Thay vào đó, Audit Service cung cấp `BeforeJson` để `RestoreDiffEngine` so sánh và đưa ra gợi ý (Restore Suggestion). Việc thực thi Restore do chính Domain Service (ví dụ Invoice) đảm nhiệm thông qua các "Semantic Domain Commands" (ví dụ `RestoreCustomerName()`), kết hợp với `IReversalPolicy` (chặn khôi phục trường Tài chính) và Concurrency Token (`RowVersion`) để tránh Stale Data.
  * Compliance: Che giấu (mask) các trường nhạy cảm bằng `SensitiveFieldMasker`.
  **Tech Stack:** Event-Driven, EF Core Interceptor, SHA-256, Dynamic Reversal Policy.
* **Resilience & Operability**:
  * **Global Exception Handling**: Chuẩn hóa phản hồi lỗi toàn hệ thống kèm theo TraceId.
  * **Health Checks**: Cung cấp endpoint `/health` cho từng service phục vụ giám sát trạng thái (Readiness/Liveness).
* **Performance**:
  * **Redis Caching**: Tập trung tại Identity Service để quản lý quyền hạn (Permissions) toàn hệ thống và tại Report Service để tối ưu hóa Dashboard.
* **API Versioning**: Hỗ trợ nhiều phiên bản API song song (ví dụ: `/api/v1/invoice`).
* **Resilience & Reliability**:
  * **Polly**: Triển khai Retry và Circuit Breaker tại Gateway để bảo vệ hệ thống khỏi các lỗi tạm thời hoặc quá tải.
    * **Idempotency**: Đảm bảo các giao dịch (như Thanh toán) không bị thực hiện lặp lại khi Client gửi trùng request thông qua `X-Idempotency-Key`.
  * **Outbox Pattern**: Sử dụng MassTransit Outbox để đảm bảo tính nhất quán dữ liệu (Eventual Consistency) giữa Database và Message Broker, ngăn chặn việc mất Event khi DB lưu thành công nhưng RabbitMQ lỗi.
  * **Module Pattern (Clean Program.cs)**: Đóng gói logic đăng ký DI vào lớp `Module` riêng, giúp `Program.cs` cực kỳ gọn nhẹ.
  * **gRPC (Synchronous Query)**: Sử dụng gRPC cho truy vấn dữ liệu tức thời giữa các microservices với hiệu năng cao.

## 🔗 Luồng nghiệp vụ (Flow)

1. **Payment**: Thực hiện thanh toán -> Lưu `Payment` với trạng thái `Completed` -> Publish `PaymentCompletedEvent` lên RabbitMQ.
2. **Invoice Service**: Consume event -> Nếu tìm thấy hóa đơn thì cập nhật trạng thái sang `Paid`.
3. **Compensation (Rollback nghiệp vụ)**: Nếu Invoice không áp dụng được event (ví dụ không tìm thấy hóa đơn), Invoice publish `PaymentCompensationRequestedEvent`.
4. **Payment Service**: Consume `PaymentCompensationRequestedEvent` -> Cập nhật giao dịch thanh toán sang `Reversed`.
5. **Orchestration** (tuỳ chọn quan sát): Ghi nhận timeline `ProcessFlow` / `FlowStep` theo cùng các event phía trên (queue riêng), API chỉ đọc qua Gateway.
6. **Audit (Kiểm toán)**: Mọi sự kiện phát sinh từ Identity (đăng nhập), Invoice (tạo, cập nhật) hoặc Payment đều được publish ngầm (qua Application code hoặc EF Interceptor) đến RabbitMQ và được ghi lại bởi Audit Service.
7. **Report**: Dashboard phản ánh dữ liệu cuối cùng sau khi xử lý bất đồng bộ.

### 🔄 Cơ chế `Reversed` (Business Rollback)

Hệ thống đang dùng **Eventual Consistency**, nên không có rollback transaction xuyên service. Thay vào đó:

* `PaymentCompletedEvent` chứa `PaymentId` để định danh chính xác giao dịch cần bù trừ.
* Invoice chỉ là nơi "áp trạng thái hóa đơn", không can thiệp trực tiếp DB của Payment.
* Khi Invoice xử lý thất bại theo nghiệp vụ, hệ thống dùng Compensation Event để yêu cầu Payment tự đảo trạng thái.
* Trạng thái cuối của Payment:
  * `Completed`: thanh toán thành công và chưa cần bù trừ.
  * `Reversed`: thanh toán đã bị đảo do bước đồng bộ hóa đơn thất bại.

---

## 📘 2. DOMAIN DESIGN

### 📦 Entities

#### Invoice

```json
{
  "Id": "guid",
  "CustomerName": "string",
  "Amount": "decimal",
  "Status": "Pending (0) | Paid (1) | Cancelled (2)",
  "CreatedAt": "datetime"
}
```

#### Payment

```json
{
  "Id": "guid",
  "InvoiceId": "guid",
  "Amount": "decimal",
  "PaymentDate": "datetime",
  "Status": "Completed (1) | Reversed (2)"
}
```

---

## 📘 3. API CONTRACT

| Service | Method | Endpoint | Quyền (Permission Code) | Mô tả |
| :--- | :--- | :--- | :--- | :--- |
| **Admin** | POST | `/api/v1/auth/login` | Công khai | Đăng nhập |
| **Admin** | GET | `/api/v1/me/permissions`| [Authorize] | Lấy quyền User hiện tại |
| **Admin** | GET | `/api/v1/org/legal-entities`| `Admin.Org.View` | Lấy danh sách pháp nhân |
| **Admin** | GET | `/api/v1/users` | `Admin.Users.View` | Danh sách người dùng |
| **Accounting**| POST | `/api/v1/journals` | `Acc.Journal.Create`| Lập bút toán thủ công |
| **Invoice** | GET | `/invoice` | `Invoice.View` | Xem danh sách hóa đơn |
| **Invoice** | POST | `/invoice` | `Invoice.Create` | Tạo hóa đơn mới |
| **Payment** | POST | `/payment/pay` | `Payment.Create` | Thanh toán (Idempotent) |
| **Report** | GET | `/report/summary` | `Report.View` | Báo cáo doanh thu |
| **Audit** | GET | `/audit` | `Audit.View` | Truy vấn nhật ký |
| **Orchestration** | GET | `/orchestration/flows`| `Orchestration.View`| Giám sát luồng giao dịch |

### 1. Audit Service (`Audit.API`) - Cổng: `5006`

*Service thu thập log kiểm toán tập trung từ các nguồn.*

* `GET /api/v1/audit` - Truy vấn danh sách Audit log (Có phân trang, filter theo Entity, Actor, Date).
* `GET /api/v1/audit/{id}` - Chi tiết 1 bản ghi Audit (Xem Before/After Json).
* `GET /api/v1/audit/verify-integrity` - Xác minh tính toàn vẹn của chuỗi Hash chain.
* `PATCH /api/v1/audit/{id}/mark-reversed` - (Internal) Đánh dấu Audit Entry đã được reverse.

### 2. Orchestration Service (`Orchestration.API`) - Cổng: `5007`

*Service theo dõi vòng đời giao dịch.*

* `GET /api/v1/orchestration/flows` - Danh sách các quy trình giao dịch (Process Flows).
* `GET /api/v1/orchestration/flows/{id}` - Chi tiết luồng giao dịch, bao gồm danh sách các bước (`FlowSteps`) đã thực hiện.
* `POST /api/v1/orchestration/flows/replay/{id}` - Kích hoạt lại toàn bộ giao dịch (Event Sourcing Replay).

---

## 🔒 Phân Quyền (Dynamic Authorization)

Hệ thống sử dụng cơ chế **Dynamic Authorization** với Redis Cache:

* 1. **Resource Format**: PascalCase dot-notation (ví dụ: `Invoice.View`, `Payment.Create`).
* 1. **Scopes**: Menu, Action, Field (Enterprise).
* 1. **Real-time Invalidation**: Thay đổi quyền trong Role sẽ có hiệu lực ngay lập tức cho toàn bộ User thuộc Role đó nhờ cơ chế xóa cache qua Event Bus.

## 🛠️ Data Correction (Reversal) Endpoints

Nằm trong các Domain Service (ví dụ Invoice Service), phục vụ quá trình Audit-Assisted Recovery:

* `GET /api/v1/invoice/{id}/restore-suggestion?auditEntryId={auditId}` - Sinh Diff (Before vs Current) gợi ý các trường có thể khôi phục.
* `POST /api/v1/invoice/{id}/restore-field` - Thực thi khôi phục một trường cụ thể (`CustomerName`) về giá trị cũ, sinh AuditEntry mới ghi nhận Reversal. Yêu cầu lý do (Reason).

---

## 📘 4. GATEWAY ROUTING (YARP)

| Path | Destination | Policy áp dụng |
| :--- | :--- | :--- |
| `/invoice/{**catch-all}` | `http://invoice-api:8080` | RateLimit, Auth |
| `/payment/{**catch-all}` | `http://payment-api:8080` | RateLimit, Auth |
| `/report/{**catch-all}` | `http://report-api:8080` | RateLimit, Auth |
| `/audit/{**catch-all}` | `http://audit-api:8080` | RateLimit, Auth |
| `/orchestration/{**catch-all}` | `http://orchestration-api:8080` | RateLimit, Auth |
| `/auth/{**catch-all}` | `http://admin-api:8080` | RateLimit, Anonymous |
| `/users/{**catch-all}` | `http://admin-api:8080` | RateLimit, Auth |
| `/org/{**catch-all}`   | `http://admin-api:8080` | RateLimit, Auth |
| `/acc/{**catch-all}`   | `http://accounting-api:8080` | RateLimit, Auth |

---

## 📘 5. DEVELOPMENT CHECKLIST

### 🟢 Phase 1 & 2: Backend & Infrastructure (Hoàn thành)

* [x] Khởi tạo Solution và Cấu trúc thư mục chuẩn.
* [x] Triển khai 5 Microservices (bao gồm Identity & Orchestration) với 4 lớp (Domain, Application, Infra, API).
* [x] Thiết lập Database Schema & Shared Context.

### 🟡 Phase 3: Integration & UI (Hoàn thành)

* [x] Cấu hình YARP Gateway & CORS.
* [x] Xây dựng WebUI (React/Vite) giao diện Premium.
* [x] Test luồng End-to-End thành công.
* [x] Cấu hình Dockerization (Multi-stage builds) cho toàn bộ hệ thống.
* [x] Triển khai Permission-based Authorization.
* [x] Tích hợp Serilog tập trung.
* [x] Thiết lập Rate Limiting & Security Hardening.

---

## 🚀 6. HƯỚNG DẪN CHẠY DỰ ÁN

Hệ thống đã được tối ưu hóa để chạy bằng Docker:

1. **Docker Compose**: Chạy lệnh duy nhất tại thư mục gốc:

   ```powershell
   docker-compose up --build -d
   ```

2. **Frontend**: `cd src/WebUI` sau đó `npm install`, `npm run dev`.
3. **Truy cập**:
   * **API Gateway**: `http://localhost:5001`
   * **Web UI**: `http://localhost:3000`
   * **Grafana Dashboard**: `http://localhost:3001` (admin/admin)
   * **Prometheus**: `http://localhost:9090`
   * **RabbitMQ UI**: `http://localhost:15672` (guest/guest)
   * **Portainer**: `http://localhost:9000`
   * **SQL Server**: `localhost,1433` (sa/Password123!)

## 📊 Monitoring Stack

Hệ thống tích hợp đầy đủ monitoring stack:

| Component | Port | Chức năng |
| :--- | :--- | :--- |
| **Loki** | 3100 | Log aggregation |
| **Prometheus** | 9090 | Metrics collection |
| **Grafana** | 3001 | Visualization & Dashboards |
| **Promtail** | N/A | Log shipping agent |

**Xem chi tiết**: Tham khảo [MONITORING_GUIDE.md](../07-operations/MONITORING_GUIDE.md)

---
*Cập nhật lần cuối: 07/05/2026 - Nâng cấp hệ thống bảo mật Enterprise và Identity Service Production-Ready.*
