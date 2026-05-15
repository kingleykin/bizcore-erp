# 📋 Bizcore ERP Logging & Observability Guide

Tài liệu này đặc tả hệ thống ghi log tập trung (Centralized Logging) của Bizcore ERP, hướng dẫn cách triển khai, quy chuẩn ghi log và quản lý dữ liệu nhạy cảm.

---

## 1. 🌟 Tổng quan hệ thống (Overview)

Hệ thống Logging của Bizcore được thiết kế để phục vụ 3 mục tiêu cốt lõi:

1. **Standardization**: Mọi Microservice đều tuân thủ một Schema duy nhất, giúp truy vấn chéo (cross-service query) dễ dàng.
2. **Security**: Tự động phân loại và che dấu (masking) dữ liệu nhạy cảm (PII, Passwords) theo chính sách chuẩn hóa.
3. **Scalability**: Sử dụng **Loki với backend MinIO (S3)** để lưu trữ log dung lượng lớn với chi phí thấp và hiệu năng cao.

---

## 2. 🛠 Đặc tả Chức năng (Functional Specification)

### 2.1. Standardized Log Schema

Mỗi log entry trong hệ thống (ghi xuống Loki) sẽ bao gồm các trường chuẩn:

| Trường | Mô tả | Loại |
| :--- | :--- | :--- |
| `Timestamp` | Thời gian xảy ra sự kiện (UTC) | ISO8601 |
| `Service` | Tên service (ví dụ: `Invoice.API`, `Admin.API`) | Label |
| `Environment` | Môi trường (`Development`, `Production`) | Label |
| `TraceId` | Định danh Trace từ OpenTelemetry | Property |
| `CorrelationId` | Định danh request xuyên suốt các service | Label/Property |
| `UserId` | ID người dùng thực hiện thao tác | Property |
| `TenantId` | ID của khách hàng (Tenant) | Label/Property |
| `EventType` | Loại sự kiện (ví dụ: `InvoiceCreated`, `AuthLoginFailed`) | Property |
| `ElapsedMs` | Thời gian xử lý request (chỉ dành cho Request Log) | Property |

### 2.2. Request Lifecycle Tracking

Hệ thống tự động ghi log cho mọi HTTP Request thông qua Middleware. Log này chứa:

- HTTP Method, Path, StatusCode.
- Thời gian xử lý (`Elapsed`).
- Context: `UserId`, `TenantId`, `CorrelationId`.

### 2.3. Data Classification & Masking Policy

Hệ thống áp dụng chính sách phân loại dữ liệu (Data Classification) để bảo vệ quyền riêng tư:

| Mức độ | Chính sách | Ví dụ |
| :--- | :--- | :--- |
| **Public** | Ghi log đầy đủ | InvoiceNo, ProductCode |
| **Internal** | Ghi log đầy đủ (hoặc partial masking trong tương lai) | CustomerName |
| **Sensitive** | **Masking (***)** | Email, PhoneNumber, Address |
| **Restricted** | **Omit (Không bao giờ ghi log)** | Password, SecretKey, Token |

---

## 3. 👩‍💻 Hướng dẫn dành cho Lập trình viên (Developer Guide)

### 3.1. Kích hoạt Logging cho Service mới

Mọi Microservice trong Bizcore nên sử dụng `AddServiceDefaults()` trong `Program.cs`. Phương thức này đã bao gồm cấu hình Serilog chuẩn.

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults("Your.Service.Name"); // Đã bao gồm Logging
```

### 3.2. Sử dụng Structured Event Logging

**KHÔNG** sử dụng log dạng text thuần túy cho các sự kiện nghiệp vụ quan trọng. Hãy sử dụng **Structured Logging**.

❌ **Sai:**

```csharp
_logger.LogInformation("User {Username} created invoice {Id}", user, id);
```

✅ **Đúng:**

```csharp
_logger.LogInformation("InvoiceCreated {@InvoiceEvent}", new { 
    InvoiceId = id, 
    Customer = customerName, 
    Amount = total 
});
```

*Lưu ý: Sử dụng ký hiệu `@` trước tên property để Serilog thực hiện destructuring đối tượng thành JSON.*

### 3.3. Bảo vệ dữ liệu với [SensitiveData]

Sử dụng attribute `[SensitiveData]` trên các thuộc tính của DTO để tự động áp dụng chính sách bảo mật.

```csharp
public record CreateUserRequest(
    string Username,
    [SensitiveData(ClassificationLevel.Sensitive)] string Email,
    [SensitiveData(ClassificationLevel.Restricted)] string Password
);
```

---

## 4. 🚀 Hướng dẫn Triển khai & Vận hành (Ops Guide)

### 4.1. Cấu hình Loki (Loki-to-MinIO)

Hệ thống sử dụng `loki-config.yaml` để đẩy dữ liệu log về MinIO thay vì lưu cục bộ trên ổ cứng container.

**Cấu hình quan trọng trong `docker-compose.yml`:**

```yaml
loki:
  image: grafana/loki:2.9.0
  volumes:
    - ./loki-config.yaml:/etc/loki/local-config.yaml
  command: -config.file=/etc/loki/local-config.yaml
```

### 4.2. Quản lý lưu trữ (Retention)

- **Hot Storage (Loki Index)**: Mặc định giữ 30 ngày (`retention_period: 720h`).
- **Cold Storage (MinIO)**: Log được lưu dưới dạng chunks trong bucket `bizcore-logs`. Có thể cấu hình Lifecycle Policy trên MinIO để nén hoặc xóa log cũ hơn 1 năm.

### 4.3. Truy vấn Log (Grafana Loki)

Truy cập Grafana (`http://localhost:3001`), vào phần **Explore** và chọn data source **Loki**.

**Một số query mẫu:**

- Tìm log của một request cụ thể: `{CorrelationId="abc-123"}`
- Tìm tất cả lỗi của Service Invoice: `{service="invoice-api", level="Error"}`
- Thống kê tỷ lệ lỗi login: `sum(count_over_time({service="admin-api"} |= "AuthLoginFailed" [5m]))`

---

## 5. ⚠️ Lưu ý quan trọng

- **Audit Log != Operational Log**: Loki chỉ dùng cho Operational Logs (debug, monitoring). Dữ liệu kiểm soát tài chính (Audit Log) phải được lưu vào **Audit Service (SQL Server + Hash Chain)** để đảm bảo tính pháp lý và không thể thay đổi.
- **Sampling**: Trong môi trường Production cao tải, có thể điều chỉnh `MinimumLevel` của các namespace `Microsoft` hoặc `EntityFrameworkCore` lên `Warning` để giảm chi phí lưu trữ.
