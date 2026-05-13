# 🚀 Bizcore ERP - Microservices Platform

[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Docker](https://img.shields.io/badge/Docker-Enabled-blue.svg)](https://www.docker.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**Bizcore ERP** là một hệ thống quản trị nguồn lực doanh nghiệp (ERP) hiện đại, được xây dựng trên kiến trúc **Microservices** hướng sự kiện (Event-Driven). Dự án tập trung vào tính mở rộng, khả năng quan sát (Observability) và độ tin cậy cao cho các nghiệp vụ Tài chính - Kế toán.

---

## 🏗️ Kiến trúc Hệ thống

Hệ thống được thiết kế theo các nguyên lý Cloud-Native:

- **API Gateway**: Sử dụng YARP (Yet Another Reverse Proxy) để điều phối request và quản lý phân quyền tập trung.
- **Messaging**: Giao tiếp bất đồng bộ qua RabbitMQ với thư viện MassTransit.
- **Patterns**: Áp dụng Transactional Outbox, Saga Orchestration, và CQRS.
- **Observability**: Hệ thống giám sát toàn diện với LGTM Stack (Loki, Grafana, Tempo, Prometheus).

---

## 💻 Ngăn xếp Công nghệ (Tech Stack)

| Lớp | Công nghệ |
| :--- | :--- |
| **Framework** | .NET 8, ASP.NET Core API |
| **Cơ sở dữ liệu** | SQL Server (Persistence), Redis (Caching) |
| **Giao tiếp** | MassTransit, RabbitMQ, gRPC, SignalR |
| **Giám sát** | OpenTelemetry, Grafana, Prometheus, Loki, Tempo |
| **Hạ tầng** | Docker, Docker Compose, Portainer |
| **Thư viện chính** | Entity Framework Core, Hangfire, MediatR, YARP |

---

## 🗺️ Bản đồ Dịch vụ (Service Map)

| Microservice | Cổng (Internal) | Chức năng chính |
| :--- | :--- | :--- |
| **Gateway.API** | 5001 | Cổng vào duy nhất, điều hướng request, AuthN/AuthZ. |
| **Admin.API** | 8080 | Quản lý hệ thống, cấu hình và người dùng. |
| **Invoice.API** | 8080 | Quản lý hóa đơn, quy trình tạo và phê duyệt. |
| **Payment.API** | 8080 | Xử lý thanh toán, tích hợp ngân hàng. |
| **Audit.API** | 8080 | Lưu vết hoạt động, kiểm soát toàn vẹn dữ liệu. |
| **Report.API** | 8080 | Tổng hợp dữ liệu và xuất báo cáo nghiệp vụ. |
| **Orchestration.API** | 8080 | Điều phối các quy trình nghiệp vụ phức tạp (Sagas). |
| **WebUI** | 3000 | Giao diện người dùng (React/Next.js). |

---

## 🛠️ Công cụ Giám sát & Hạ tầng

Sau khi khởi chạy hệ thống, bạn có thể truy cập các công cụ sau:

| Công cụ | URL | Tài khoản mặc định |
| :--- | :--- | :--- |
| **Web UI** | [http://localhost:3000](http://localhost:3000) | Tùy chọn |
| **API Gateway** | [http://localhost:5001](http://localhost:5001) | - |
| **Grafana (Dashboard)** | [http://localhost:3001](http://localhost:3001) | `admin` / `admin` |
| **RabbitMQ Management** | [http://localhost:15672](http://localhost:15672) | `guest` / `guest` |
| **Portainer (Docker)** | [http://localhost:9000](http://localhost:9000) | `admin` / `admin123456789` |
| **Prometheus** | [http://localhost:9090](http://localhost:9090) | - |

---

## 🚀 Hướng dẫn Cài đặt Nhanh

### Yêu cầu hệ thống

- Docker & Docker Compose
- .NET 8 SDK (nếu muốn chạy local)
- PowerShell (Windows) hoặc Bash (Linux/macOS)

### Triển khai với Docker

1. **Clone dự án**:

   ```bash
   git clone https://github.com/kingleykin/bizcore-erp.git
   cd bizcore-erp
   ```

2. **Khởi chạy toàn bộ hệ thống**:

   ```bash
   docker compose up -d --build
   ```

3. **Kiểm tra trạng thái**: Sử dụng Portainer hoặc `docker ps` để đảm bảo tất cả containers đã chạy.

---

## 📖 Hướng dẫn cho Lập trình viên

Nếu bạn là thành viên mới trong đội ngũ phát triển, vui lòng đọc tài liệu sau trước khi bắt đầu:

- [**Bắt đầu tại đây (START_HERE.md)**](START_HERE.md): Quy tắc làm việc và lộ trình tìm hiểu.
- [**Quy trình làm việc (GIT_WORKFLOW.md)**](docs/06-conventions/GIT_WORKFLOW.md): Cách quản lý branch, commit và merge.
- [**Hướng dẫn Deploy (DEPLOYMENT_GUIDE.md)**](docs/07-operations/DEPLOYMENT_GUIDE.md): Các môi trường và quy trình triển khai.
- [**Quy tắc Code (CODING_CONVENTIONS.md)**](docs/06-conventions/CODING_CONVENTIONS.md): Các tiêu chuẩn viết mã trong dự án.
- [**Hướng dẫn Review (CODE_REVIEW_GUIDE.md)**](docs/06-conventions/CODE_REVIEW_GUIDE.md): Quy trình kiểm soát chất lượng.

---

## 🔧 Xử lý sự cố (Troubleshooting)

### Lỗi xung đột cổng 5000/5001 trên macOS

Trên các phiên bản macOS mới, cổng 5000 thường bị chiếm bởi **AirPlay Receiver**.

1. Vào **System Settings** -> **General** -> **AirDrop & Handoff**.
2. Tắt **AirPlay Receiver**.
3. Khởi động lại Docker: `docker compose up -d`.

---

## 📄 Giấy phép

Dự án được phát hành dưới giấy phép [MIT](LICENSE).
