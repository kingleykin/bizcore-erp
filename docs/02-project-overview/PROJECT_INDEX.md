# 🤖 AI PROJECT INDEX: BIZCORE ERP

> [!IMPORTANT]
> **Mục đích của tài liệu này**: Cung cấp bức tranh toàn cảnh (ngữ cảnh, kiến trúc, và các quy tắc cốt lõi) của dự án **Bizcore ERP** để AI Agent có thể hiểu nhanh chóng hệ thống, từ đó đưa ra các đề xuất code chính xác và duy trì tính nhất quán của kiến trúc.
**Tradeoff:** Bias về cẩn trọng hơn là tốc độ. Với task trivial, dùng judgment.
---

## 1. 🌟 Tổng quan Dự án (Project Overview)

- **Tên dự án**: Bizcore ERP
- **Mô tả**: Hệ thống ERP **Production-ready** được xây dựng theo chuẩn kiến trúc Microservices chuyên nghiệp. Luồng nghiệp vụ cốt lõi tập trung vào: **Xác thực (Identity) -> Hóa đơn (Invoice) -> Thanh toán (Payment) -> Báo cáo (Report)**, được giám sát toàn diện bởi **Hệ thống Audit (Centralized Audit Service)**.
- **Tài liệu chi tiết**: Xem [PROJECT OVERVIEW](PROJECT_OVERVIEW.md)

### 🛠 Tech Stack & Patterns

- **Backend**: C#, .NET (ASP.NET Core Web API).
- **Kiến trúc**: Microservices kết hợp Domain-Driven Design (DDD) Lite và Event-Driven Architecture (EDA).
- **API Gateway**: YARP (Yet Another Reverse Proxy).
- **Message Broker**: RabbitMQ (MassTransit) cho giao tiếp bất đồng bộ và Audit Events.
- **Database**: SQL Server (Các service dùng chung 1 instance SQL Server nhưng phân tách logical database: IdentityDb, InvoiceDb, AuditDb...).
- **Observability**: Serilog + Loki (Logs), Prometheus (Metrics), Grafana (Dashboards), **Tempo (Traces)**, **OTEL Collector**.
- **Frontend**: React/Vite.
- **Caching**: **Redis** (Phân quyền, Performance).
- **Storage**: **MinIO** (Object Storage tương thích S3 cho Avatars, Invoices, Reports).
- **Design Patterns cốt lõi**: Outbox Pattern, Retry/Circuit Breaker (Polly), Idempotency, Eventual Consistency, **Module Pattern (Clean Program.cs)**, **gRPC (Synchronous Queries)**, Compensation (Rollback nghiệp vụ), **Audit-Assisted Data Correction (Reversal)**, **Dynamic Authorization (Fine-grained)**, **Enterprise Localization & Error Governance**.

---

## 2. 📂 Cấu trúc Mã nguồn (Project Structure)

Dự án áp dụng mô hình phân tách rõ ràng. Chi tiết thiết kế xem tại [PROJECT STRUCTURE](PROJECT_STRUCTURE.md).

```text
bizcore-erp/
├── src/
│   ├── Gateway/Gateway.API        # YARP Gateway, xử lý routing, auth tập trung
│   ├── Services/
│   │   ├── Admin                  # Admin Service (Identity, Org Master Data, Global Settings)
│   │   ├── Accounting             # ACC Core & ACC Batch (Ledger, Posting Rule, Period Close)
│   │   ├── Invoice                # Quản lý Hóa đơn (AR/AP Sub-ledger)
│   │   ├── Payment                # Xử lý Thanh toán (Treasury Sub-ledger)
│   │   ├── Inventory              # Quản lý Kho (INV Sub-ledger)
│   │   ├── Report                 # Báo cáo tổng hợp (CQRS/Materialized Views)
│   │   ├── Audit                  # Hệ thống Audit tập trung (Immutable, Hash chain)
│   │   ├── File                   # Quản lý tệp tin tập trung (MinIO Integration) - [Tài liệu](../04-services/file-service.md)
│   │   └── Orchestration          # Theo dõi luồng sự kiện phân tán (Read-side)
│   ├── BuildingBlocks/
│   │   ├── Bizcore.BuildingBlocks # Shared Library (Contracts, Events, Permissions, ErrorCodes)
│   │   ├── Bizcore.BuildingBlocks.Storage # Shared Storage Library (MinIO SDK)
│   │   └── Bizcore.Localization   # Hệ thống quản lý tài nguyên dịch thuật tập trung
│   └── WebUI                      # Frontend React
└── docker-compose.yml             # Triển khai toàn bộ hạ tầng
```

> [!NOTE]
> **Cấu trúc bên trong mỗi Microservice (4-Layer DDD Lite):**
>
> 1. **Domain Layer**: Chứa Entities, Enums, Interfaces. Không phụ thuộc thư viện ngoài. Chứa Domain Validation.
> 2. **Application Layer**: Chứa Use Cases (Services/Handlers). Điều phối logic nghiệp vụ.
> 3. **Infrastructure Layer**: DbContext, Migrations, MassTransit Config, External Clients.
> 4. **API Layer**: Controllers, **Module.cs**, **Program.cs**.
>    - `Program.cs`: Chỉ chứa host setup và nạp Module.
>    - `Module.cs`: Đóng gói toàn bộ đăng ký DI của service.

---

## 3. 🔄 Luồng Nghiệp vụ Cốt lõi & Giao tiếp (Business Flow)

Hệ thống giao tiếp bất đồng bộ bằng **RabbitMQ** (Event-Driven) để giảm coupling.

1. Khách hàng **Thanh toán**: `Payment Service` lưu giao dịch `Completed` -> Publish event `PaymentCompletedEvent`.
2. **Cập nhật Hóa đơn**: `Invoice Service` consume event trên -> Cập nhật trạng thái hóa đơn thành `Paid`.
3. **Rollback Nghiệp vụ (Compensation)**: Nếu `Invoice Service` xử lý lỗi (VD: hóa đơn không tồn tại), nó publish `PaymentCompensationRequestedEvent`. `Payment Service` consume event này và đổi trạng thái thanh toán thành `Reversed`.
4. **Theo dõi luồng (Orchestration)**: `Orchestration Service` lắng nghe tất cả events trên để lưu vào `ProcessFlow` và `FlowStep`. Cho phép theo dõi toàn bộ vòng đời giao dịch một cách minh bạch. Xem chi tiết tại [ORCHESTRATION_GUIDE](../03-architecture/ORCHESTRATION_GUIDE.md).
5. **Kiểm toán (Audit)**: Mọi thao tác làm thay đổi dữ liệu (từ Application Layer hoặc EF Core Interceptor) đều publish `AuditEvent` về `Audit Service` để lưu trữ dạng append-only với Hash chain chống thay đổi.
6. **Sửa sai dữ liệu (Admin Data Correction / Reversal)**: Khi Admin nhập sai dữ liệu (VD: sai tên khách hàng), hệ thống hỗ trợ khôi phục (Restore) dựa trên `BeforeJson` của Audit log. Audit Service đóng vai trò "gợi ý" (Suggest), còn Core Service (VD: Invoice) đóng vai trò "thực thi" (Execute) thông qua các Semantic Domain Commands để đảm bảo tính an toàn tài chính (không overwrite bừa bãi).

---

---

## 4. 🔄 Transaction Management (Data Integrity Protection)

Hệ thống áp dụng **3 Transaction Patterns** để đảm bảo tính toàn vẹn dữ liệu:

### 4.1. Local Transaction Pattern

**Dùng cho:** Nhiều thao tác ghi trên nhiều bảng trong cùng 1 database

- Payment + IdempotencyRecord
- Invoice + InvoiceLineItems
- User + UserRole + UserPermission

### 4.2. Outbox Pattern (MassTransit)

**Dùng cho:** DB write + Message publish (tránh dual write problem)

- Invoice creation + InvoiceCreatedEvent
- Payment initiation + PaymentInitiatedEvent
- Status update + AuditEvent

### 4.3. Partitioned Audit Hash Chain

**Dùng cho:** Bảo vệ Hash Chain khỏi race condition bằng sequence/lock theo partition

- Audit Service - AuditEventConsumer

### 📚 Tài liệu Transaction Management

| Document | Mục đích | Đối tượng |
|----------|----------|-----------|
| [TRANSACTION_README.md](../05-transactions/TRANSACTION_README.md) | Hướng dẫn sử dụng tài liệu | Everyone |
| [TRANSACTION_SUMMARY.md](../05-transactions/TRANSACTION_SUMMARY.md) | Executive summary, ROI | Managers |
| [TRANSACTION_MANAGEMENT_DESIGN.md](../05-transactions/TRANSACTION_MANAGEMENT_DESIGN.md) | Thiết kế chi tiết | Architects |
| [TRANSACTION_IMPLEMENTATION_GUIDE.md](../05-transactions/TRANSACTION_IMPLEMENTATION_GUIDE.md) | Code examples, step-by-step | Developers |
| [TRANSACTION_QUICK_REFERENCE.md](../05-transactions/TRANSACTION_QUICK_REFERENCE.md) | Code templates, troubleshooting | Developers |
| [TRANSACTION_PATTERNS_DIAGRAM.md](../05-transactions/TRANSACTION_PATTERNS_DIAGRAM.md) | Visual diagrams | Everyone |

---

## 5. 🧠 Hướng dẫn dành cho AI Agent (AI Developer Guidelines)

Khi AI tham gia viết code hoặc debug cho dự án này, hãy TUÂN THỦ NGHIÊM NGẶT các quy tắc sau:

> [!CAUTION]
> **Tuân thủ DDD & Clean Architecture**:
>
> - **KHÔNG** viết logic nghiệp vụ (business logic) ở tầng API/Controllers.
> - Giữ tầng Domain "sạch" (pure), không inject DB contexts hay Framework-specific dependencies vào Domain entities.
> - Validate input format bằng **FluentValidation** (tầng API/App), validate business rules bên trong **Domain Entities**.

> [!WARNING]
> **Giao tiếp giữa các Services**:
>
> - **KHÔNG** gọi HTTP trực tiếp giữa các service (trừ phi thiết kế bắt buộc). Ưu tiên dùng Event (Publish/Subscribe qua MassTransit) đặt trong `Bizcore.BuildingBlocks`.
> - Khi publish Message/Event, bắt buộc phải cân nhắc tính toàn vẹn bằng cách sử dụng **Outbox Pattern**.

> [!TIP]
> **Bảo mật & Phân quyền**:
>
> - Hệ thống sử dụng **Dynamic Authorization** với **Redis Caching**.
> - Mọi API endpoint mới (trừ endpoint public/auth) đều phải được gắn `RequirePermission` attribute (ví dụ `[RequirePermission(Permissions.Invoice.Create)]`).
> - Quyền được phân loại theo: Menu, Page, Action, Field. Định nghĩa tập trung tại `Bizcore.BuildingBlocks`.
> - Thay đổi quyền hạn có hiệu lực tức thì nhờ cơ chế **Real-time Cache Invalidation** qua Event-bus.

> [!IMPORTANT]
> **Xử lý Lỗi (Error Handling) & Observability**:
>
> - Luôn throw các Exception cụ thể (`DomainException`, `NotFoundException`, v.v.) kèm theo **ErrorCode** (định nghĩa tại `Bizcore.BuildingBlocks.ErrorCodes`) thay vì return code trực tiếp.
> - `Global Exception Handling Middleware` sẽ xử lý và chuẩn hóa format lỗi cho Frontend (React/i18next) tự động dịch sang ngôn ngữ người dùng.
> - Mọi request đều có `X-Correlation-ID`. Đảm bảo các tiến trình background (MassTransit consumers) cũng kế thừa và ghi log kèm Correlation ID này để phục vụ distributed tracing.
> - Ngôn ngữ (Culture) được lan truyền tự động qua Headers (`X-Culture`) trong toàn bộ hệ thống (HTTP & RabbitMQ).

> [!IMPORTANT]
> **Đồng bộ hóa (Idempotency & Concurrency)**:
>
> - Các endpoint tạo mới/thanh toán phải xử lý Idempotency (kiểm tra `X-Idempotency-Key` từ header) để tránh duplicate data khi retry.
> - Xử lý event từ RabbitMQ (Consumers) phải là Idempotent (có thể chạy lại an toàn nếu bị retry do lỗi mạng).

> [!IMPORTANT]
> **Transaction Management (Data Integrity)**:
>
> - **Local Transaction**: Sử dụng `DbContext.Database.BeginTransactionAsync()` cho các thao tác ghi nhiều bảng trong cùng 1 database.
> - **Outbox Pattern**: Sử dụng MassTransit Outbox để đảm bảo tính atomic giữa DB write và Message publish (tránh dual write problem).
> - **Partitioned Audit Hash Chain**: Áp dụng cho Audit Service; serialize append theo `PartitionKey`, không dùng global Serializable.
> - **ExecutionStrategy**: Sử dụng để tự động retry khi gặp transient errors (deadlock, connection timeout).
> - Chi tiết: [TRANSACTION_MANAGEMENT_DESIGN.md](../05-transactions/TRANSACTION_MANAGEMENT_DESIGN.md) và [TRANSACTION_IMPLEMENTATION_GUIDE.md](../05-transactions/TRANSACTION_IMPLEMENTATION_GUIDE.md)
>
---

## 6. 🧪 Kiểm thử (Testing Strategy)

Hệ thống áp dụng chiến lược kiểm thử đa tầng:

- **Unit Tests**: Kiểm tra logic nghiệp vụ tại `Bizcore.UnitTests`.
- **API Tests**: Kiểm tra tích hợp Microservices với real infra (Docker) tại `Bizcore.ApiTests`.
- **E2E Tests**: Kiểm tra luồng người dùng trên trình duyệt (Playwright) tại `Bizcore.E2ETests`.

> [!TIP]
> Hướng dẫn chi tiết cách chạy và viết test xem tại: [TESTING_GUIDE.md](../09-testing/TESTING_GUIDE.md)
