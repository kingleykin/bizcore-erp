# 📐 Tiêu chuẩn Hệ thống & Hiệu năng (System Standards)

Tài liệu này quy định các tiêu chuẩn kỹ thuật tối thiểu và các chỉ số hiệu năng (SLA/SLO) bắt buộc phải tuân thủ khi triển khai và phát triển hệ thống Bizcore ERP.

---

## 1. Yêu cầu Triển khai Tối thiểu (Minimum Deployment Requirements)

Để đảm bảo service hoạt động ổn định trong môi trường Production, mỗi instance (container) cần được cấp phát tài nguyên như sau:

| Loại Service | CPU (vCPU) | RAM (Memory) | Disk (Tạm thời) |
| :--- | :--- | :--- | :--- |
| **Core Services** (Accounting, Invoice, Payment) | 1.0 - 2.0 | 1GB - 2GB | 5GB |
| **Supporting Services** (Audit, Orchestration, Admin) | 0.5 - 1.0 | 512MB - 1GB | 2GB |
| **Gateway** (YARP) | 0.5 - 1.0 | 512MB | 1GB |
| **Infrastructure** (SQL Server, Redis, RabbitMQ) | Theo khuyến cáo vendor (Tối thiểu 4 vCPU / 8GB RAM cho SQL) | | |

---

## 2. Chỉ số Hiệu năng Mục tiêu (Performance SLOs)

Hệ thống cam kết đạt được các chỉ số phản hồi sau (đo lường tại Gateway):

### 2.1. Tốc độ phản hồi (Latency)

- **Synchronous API (GET/POST)**:
  - P95 (95% request): < **300ms**
  - P99 (99% request): < **800ms**
- **gRPC (Internal communication)**:
  - Average: < **50ms**
- **Báo cáo/Dashboard**:
  - Thời gian tải dữ liệu cache: < **200ms**
  - Thời gian tính toán báo cáo nặng: < **5s** (Sử dụng background processing nếu lâu hơn).

### 2.2. Khả năng xử lý (Throughput)

- **Hệ thống cơ bản (1 Instance/Service)**: Có thể xử lý tối thiểu **100 - 200 Transactions Per Second (TPS)** cho các luồng nghiệp vụ thông thường.
- **Concurrent Users**: Hỗ trợ tối thiểu **500 - 1000 người dùng hoạt động đồng thời** trên một cấu hình tiêu chuẩn.

---

## 3. Nguyên tắc Toàn vẹn Dữ liệu (Data Integrity Principles)

Vì hệ thống sử dụng Event-Driven Architecture (EDA), việc đảm bảo dữ liệu "cuối cùng sẽ khớp" (Eventual Consistency) là cực kỳ quan trọng:

1. **Độ trễ Eventual Consistency**: Thời gian từ khi Payment thành công đến khi Invoice cập nhật trạng thái `Paid` phải < **2 giây** trong điều kiện bình thường.
2. **Độ trễ Audit Logging**: Audit log phải được ghi nhận vào `AuditDb` trong vòng < **1 giây** sau khi transaction gốc hoàn tất.
3. **Idempotency**: Mọi API làm thay đổi dữ liệu (POST/PUT/PATCH) bắt buộc phải xử lý Idempotency. Hệ thống phải từ chối các request trùng lặp trong vòng **24h** dựa trên `X-Idempotency-Key`.
4. **Audit Integrity**: Định kỳ 15 phút hệ thống phải chạy kiểm tra tính toàn vẹn (Hash chain integrity check) của Audit Log.

---

## 4. Nguyên tắc Vận hành (Operational Principles)

1. **Zero-Downtime Deployment**: Bắt buộc sử dụng chiến lược **Rolling Update** hoặc **Blue-Green Deployment**. Không bao giờ được ngắt kết nối người dùng khi cập nhật phiên bản mới.
2. **Observability**: 100% request phải có `Correlation-ID`. Mọi lỗi `5xx` phải được gửi cảnh báo (Alert) ngay lập tức tới đội ngũ vận hành.
3. **Security**:
    - 100% giao tiếp giữa các service và từ client phải qua HTTPS/TLS.
    - Cấp quyền theo nguyên tắc **Least Privilege** (Quyền tối thiểu).
4. **Health Checks**: Service không có Health Check hợp lệ (Liveness/Readiness) sẽ không được phép join vào luồng traffic của Gateway.

---

## 5. Định hướng Mở rộng (Scalability Rules)

- **Scale Ngang (Horizontal Scaling)**: Khi CPU của một service duy trì > **70%** trong 5 phút, hệ thống tự động khởi tạo thêm instance mới (Auto-scaling).
- **Database Sharding**: Khi một logical database (ví dụ `InvoiceDb`) vượt quá **500GB** hoặc **10 triệu bản ghi**, cần xem xét phương án sharding hoặc partitioning.

## 6. Gợi ý Cấu hình Triển khai (Recommended Deployment Profiles)

Dưới đây là các cấu hình đề xuất dựa trên quy mô doanh nghiệp để đảm bảo hiệu năng và sự ổn định:

### 🔹 Profile S: Standard (Cho Doanh nghiệp nhỏ / Testing)

- **Kiến trúc**: Single-node Docker Compose.
- **Tài nguyên**: 1 Server (8 vCPU, 16GB RAM).
- **Hạ tầng**: Chạy chung trên cùng server (SQL, Redis, RabbitMQ).
- **Đặc điểm**: Chi phí thấp, dễ quản lý, không có HA.

### 🔹 Profile M: Professional (Cho Doanh nghiệp vừa / Production)

- **Kiến trúc**: Micro-k8s hoặc Docker Swarm (2-3 Nodes).
- **Tài nguyên**: 3 Nodes (Mỗi node 8 vCPU, 16GB RAM).
- **Hạ tầng**:
  - SQL Server AlwaysOn (2 Nodes).
  - Redis Sentinel.
- **Đặc điểm**: Hỗ trợ HA cho Services, chịu tải tốt, đảm bảo an toàn dữ liệu.

### 🔹 Profile L: Enterprise (Cho Hệ thống lớn / High Load)

- **Kiến trúc**: Full Managed Kubernetes (AKS/EKS/GKE).
- **Tài nguyên**: Auto-scaling (Tối thiểu 5-10 Nodes).
- **Hạ tầng**:
  - Dedicated SQL Cluster (PaaS).
  - RabbitMQ Cluster (3 Nodes).
  - Redis Cluster (Sharding).
- **Đặc điểm**: Khả năng mở rộng không giới hạn, độ trễ cực thấp, tính sẵn sàng 99.99%.

---
*Cập nhật lần cuối: 12/05/2026 - Thiết lập tiêu chuẩn vận hành cho Bizcore ERP.*
