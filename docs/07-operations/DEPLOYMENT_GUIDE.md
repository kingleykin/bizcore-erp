# 🚢 DEPLOYMENT GUIDE - BIZCORE ERP

> **Mục đích**: Hướng dẫn quy trình triển khai ứng dụng lên các môi trường khác nhau, đảm bảo tính nhất quán và giảm thiểu rủi ro.

---

## 🌐 1. Các Môi trường (Environments)

| Môi trường | Mục đích | Cách triển khai |
| :--- | :--- | :--- |
| **Development** | Lập trình viên phát triển và test local. | `docker compose up` |
| **Staging** | Kiểm thử tích hợp (QC) và Demo. | Tự động (GitHub Actions) |
| **Production** | Môi trường vận hành thực tế. | Thủ công có kiểm soát / CD |

---

## 🏗️ 2. Quy trình CI/CD (Đề xuất)

Dự án hướng tới việc tự động hóa hoàn toàn quy trình triển khai qua **GitHub Actions**.

### Luồng xử lý

1. **Code Push**: Lập trình viên đẩy code lên nhánh `develop` hoặc `main`.
2. **Build & Test**: Hệ thống tự động chạy `dotnet build` và `dotnet test`.
3. **Dockerize**: Build Docker images cho từng microservice.
4. **Push Registry**: Đẩy images lên Docker Hub hoặc Private Registry.
5. **Deploy**: Cập nhật images mới trên server và khởi động lại dịch vụ.

---

## 🛠️ 3. Triển khai Thủ công (Docker Compose)

Đây là cách nhanh nhất để deploy toàn bộ hệ thống lên một server mới.

### Bước 1: Chuẩn bị server

- Đã cài đặt Docker và Docker Compose.
- Mở các cổng cần thiết (5001, 3000, 3001, 15672...).

### Bước 2: Cấu hình biến môi trường

Tạo file `.env` từ file mẫu để cấu hình các thông số nhạy cảm:

- `MSSQL_SA_PASSWORD`: Mật khẩu cơ sở dữ liệu.
- `RABBITMQ_DEFAULT_PASS`: Mật khẩu RabbitMQ.
- `JWT_SECRET`: Khóa bí mật cho mã hóa token.

### Bước 3: Khởi chạy

```bash
# Pull code mới nhất
git pull origin main

# Khởi chạy hệ thống ở chế độ background
docker compose up -d --build
```

---

## 🔍 4. Kiểm tra sau khi Deploy (Post-Deployment)

Sau khi deploy, cần thực hiện các bước kiểm tra (Smoke Test):

1. **Health Check**: Truy cập endpoint `/health` của các service để đảm bảo chúng đang hoạt động.
2. **Log Review**: Kiểm tra logs qua Grafana/Loki để phát hiện sớm các lỗi khởi động.
3. **Database Migration**: Đảm bảo các script migration đã được thực thi thành công.
4. **Connectivity**: Kiểm tra kết nối giữa Gateway và các Microservices.

---

## 🚨 5. Quy trình Rollback

Nếu phát hiện lỗi nghiêm trọng sau khi deploy:

1. **Xác định phiên bản ổn định gần nhất** (tag docker image).
2. **Cập nhật cấu hình**: Sửa file `docker-compose.yml` để trỏ về image version cũ.
3. **Deploy lại**: `docker compose up -d`.
4. **Điều tra**: Tìm nguyên nhân lỗi trên môi trường Dev trước khi thử lại.

---

> **Tài liệu liên quan**:
>
> - [Monitoring Guide](../07-operations/MONITORING_GUIDE.md)
> - [System Standards](../07-operations/SYSTEM_STANDARDS.md)
