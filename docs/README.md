# 📚 Bizcore ERP Documentation

Chào mừng bạn đến với kho tài liệu kỹ thuật của dự án **Bizcore ERP**. Tài liệu được tổ chức theo cấu trúc phân tầng để dễ dàng theo dõi và tra cứu.

---

## 📂 Danh mục Tài liệu

### 🚀 01. Bắt đầu (Getting Started)

Hướng dẫn dành cho người mới bắt đầu và thiết lập môi trường.

- [Hướng dẫn Phát triển (DEV_GUIDE)](01-getting-started/DEV_GUIDE.md)
- [Hướng dẫn Debug (DEBUGGING_GUIDE)](01-getting-started/DEBUGGING_GUIDE.md)
- [Hướng dẫn Demo hệ thống (DEMO_GUIDE)](01-getting-started/DEMO_GUIDE.md)

### 🏗️ 02. Tổng quan Dự án (Project Overview)

Cái nhìn toàn cảnh về hệ thống, cấu trúc thư mục và chỉ mục chính.

- [Chỉ mục Dự án (PROJECT_INDEX)](02-project-overview/PROJECT_INDEX.md) - **Điểm bắt đầu quan trọng nhất cho AI & Dev.**
- [Tổng quan dự án (PROJECT_OVERVIEW)](02-project-overview/PROJECT_OVERVIEW.md)
- [Cấu trúc mã nguồn (PROJECT_STRUCTURE)](02-project-overview/PROJECT_STRUCTURE.md)

### 📐 03. Kiến trúc & Design Patterns (Architecture)

Các tài liệu chuyên sâu về thiết kế hệ thống và các pattern áp dụng.

- [Thiết kế Idempotency](03-architecture/IDEMPOTENCY_DESIGN.md)
- [Hướng dẫn Orchestration](03-architecture/ORCHESTRATION_GUIDE.md)
- [Saga Orchestrator Guide](03-architecture/SAGA_ORCHESTRATOR_GUIDE.md)
- [Saga Guardrails](03-architecture/SAGA_GUARDRAILS.md)
- [Dynamic Authorization](03-architecture/DYNAMIC_AUTHORIZATION.md)
- [Hangfire Guide](03-architecture/HANGFIRE_GUIDE.md)

### ⚙️ 04. Chi tiết Microservices (Services)

Tài liệu riêng biệt cho từng service.

- [Admin Service (Organization & Master Data)](04-services/admin-service.md)
- [Accounting Service (Core Engine & Batch)](04-services/accounting-service.md)
- [Audit Service](04-services/audit-service.md)
- [Identity Service](04-services/identity-service.md)
- [Invoice Service (AR/AP Sub-ledger)](04-services/invoice-service.md)
- [Payment Service (Treasury Sub-ledger)](04-services/payment-service.md)
- [Report Service](04-services/report-service.md)
- [Orchestration Service](04-services/orchestration-service.md)

### 🖥️ 04.1. Giao diện Người dùng (UI/UX)

- [Phân bổ UI/UX cho Kiến trúc Microservices](03-architecture/UIUX_ARCHITECTURE_MAPPING.md)

### 🛡️ 05. Quản lý Giao dịch & Dữ liệu (Transactions)

Đảm bảo tính toàn vẹn dữ liệu trong hệ thống phân tán.

- [Transaction Readme](05-transactions/TRANSACTION_README.md)
- [Transaction Summary](05-transactions/TRANSACTION_SUMMARY.md)
- [Transaction Management Design](05-transactions/TRANSACTION_MANAGEMENT_DESIGN.md)
- [Transaction Implementation Guide](05-transactions/TRANSACTION_IMPLEMENTATION_GUIDE.md)
- [Transaction Quick Reference](05-transactions/TRANSACTION_QUICK_REFERENCE.md)

### 📏 06. Quy định & Quy trình (Conventions)

Tiêu chuẩn coding và quy trình review code.

- [Coding Conventions](06-conventions/CODING_CONVENTIONS.md)
- [Git Workflow & Collaboration](06-conventions/GIT_WORKFLOW.md)
- [Code Review Guide](06-conventions/CODE_REVIEW_GUIDE.md)
- [Conventions Index](06-conventions/CONVENTIONS_INDEX.md)

### 🛠️ 07. Vận hành (Operations)

Giám sát, triển khai và bảo trì.

- [Monitoring Guide](07-operations/MONITORING_GUIDE.md)
- [Deployment Guide](07-operations/DEPLOYMENT_GUIDE.md)
- [HA & Load Balancing Guide](07-operations/HA_LB_GUIDE.md)
- [Tiêu chuẩn Hệ thống (System Standards)](07-operations/SYSTEM_STANDARDS.md)

### 🔄 08. Di cư & Nâng cấp (Migration)

Hướng dẫn nâng cấp và chuyển đổi dữ liệu.

- [Payment Service Migration Guide](08-migration/PAYMENT_SERVICE_MIGRATION_GUIDE.md)

### 🧪 09. Kiểm thử (Testing)

Chiến lược và hướng dẫn thực hiện kiểm thử tự động.

- [Hướng dẫn Kiểm thử (TESTING_GUIDE)](09-testing/TESTING_GUIDE.md)

---

> [!TIP]
> Nếu bạn là **AI Agent**, hãy bắt đầu đọc từ [PROJECT_INDEX.md](02-project-overview/PROJECT_INDEX.md) để nắm bắt ngữ cảnh nhanh nhất.
