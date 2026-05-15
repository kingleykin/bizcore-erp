# 📊 DASHBOARD MANAGEMENT GUIDE

Tài liệu này hướng dẫn cách quản lý, sử dụng và khắc phục sự cố cho các Dashboard giám sát (Grafana) trong hệ thống Bizcore ERP.

---

## 📂 Danh sách Dashboard

Hệ thống cung cấp sẵn các file JSON tại thư mục này để Import vào Grafana:

1. **[api_performance.json](./api_performance.json)**: Giám sát lưu lượng API, tỷ lệ lỗi và độ trễ (P95).
2. **[infrastructure_health.json](./infrastructure_health.json)**: Giám sát tài nguyên hệ thống (CPU, RAM, .NET GC).
3. **[business_operations.json](./business_operations.json)**: Giám sát nghiệp vụ ERP (Giao dịch thành công/thất bại, Tổng tiền...).

---

## 📥 Hướng dẫn Import

1. Truy cập Grafana (Mặc định: `http://localhost:3001`).
2. Đăng nhập (`admin` / `admin`).
3. Vào menu **Dashboards** -> **New** -> **Import**.
4. Copy nội dung file JSON và dán vào ô **"Import via panel json"**.
5. Chọn Data Source là **Prometheus** và nhấn **Import**.

---

## 🛠️ Xử lý lỗi "No Data" (Sai lệch UID)

Lỗi phổ biến nhất khi mang Dashboard sang server mới là sai lệch **Data Source UID**.

### Cách 1: Cấu hình Provisioning (Khuyên dùng)

Để tránh sai lệch UID, bạn nên ép UID cố định trong file `docker-compose.yml` hoặc file cấu hình Data Source:

```yaml
# Ví dụ cấu hình provisioning
datasources:
  - name: Prometheus
    uid: bizcore-prometheus-static-uid
    ...
```

### Cách 2: Lấy UID thủ công

Nếu bạn đã lỡ tạo Data Source tự động, hãy lấy UID để cập nhật vào file JSON:

1. Vào **Administration** -> **Data Sources** -> Chọn **Prometheus**.
2. UID nằm trong URL trình duyệt: `.../edit/<UID_CỦA_BẠN>`.
3. Mở file JSON Dashboard, tìm kiếm từ khóa `"uid": "..."` trong các thẻ `datasource` và thay thế bằng UID mới.

---

## 📈 Các Metric quan trọng (OpenTelemetry)

Hệ thống đã chuẩn hóa theo OpenTelemetry. Dưới đây là các metric bạn có thể dùng để tự tạo Dashboard mới:

| Loại | Metric Name | Giải thích |
| :--- | :--- | :--- |
| **API** | `http_server_request_duration_seconds_count` | Tổng số lượng request nhận được. |
| **Latency** | `http_server_request_duration_seconds_bucket` | Phân bổ thời gian phản hồi (dùng cho P95). |
| **Runtime** | `dotnet_total_memory_bytes` | Tổng bộ nhớ RAM mà ứng dụng đang chiếm dụng. |
| **Payment** | `payment_completed_total` | Tổng số giao dịch thanh toán thành công. |
| **Reversal** | `payment_reversed_total` | Tổng số giao dịch bị hoàn tác (Saga compensation). |

---

## 🚀 Mẹo tối ưu hóa

* **Refresh Rate**: Nên để mặc định là `10s` hoặc `30s` để tránh quá tải cho Prometheus server.
* **Time Range**: Xem ở mức `Last 5-15 minutes` để thấy dữ liệu biến động gần nhất.
* **Variables**: Bạn có thể thêm biến `$service` để lọc dữ liệu cho từng Microservice cụ thể.

---
*Cập nhật lần cuối: 2026-05-14*
