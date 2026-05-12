# 🪲 Bizcore ERP - Debugging Guide

Tài liệu này hướng dẫn cách thiết lập và sử dụng trình gỡ lỗi (debugger) hiệu quả nhất cho hệ thống Bizcore ERP trên Visual Studio.

---

## 1. Yêu cầu hệ thống

- **Visual Studio 2022** (phiên bản 17.10 trở lên).
- **Docker Desktop** (hoặc Docker engine chạy qua WSL2) đang hoạt động.
- **RAM**: Khuyến nghị từ 16GB để chạy toàn bộ các service ổn định.

---

## 2. Các phương pháp Debug

### 2.1. Debug toàn bộ hệ thống (Docker Compose - Khuyên dùng)

Đây là cách tốt nhất để kiểm tra sự tương tác giữa các Microservices và hạ tầng (Redis, RabbitMQ, SQL Server).

**Cách thực hiện:**

1. Mở Solution [Bizcore.slnx](../../src/Bizcore.slnx).
2. Tìm project **docker-compose** (biểu tượng cá voi) trong Solution Explorer.
3. Chuột phải vào project này và chọn **Set as Startup Project**.
4. Nhấn **F5** (hoặc nút Start).

**Cơ chế hoạt động:**

- Visual Studio sẽ chạy lệnh `docker-compose up`.
- Tự động attach debugger vào code chạy bên trong container.
- Bạn có thể đặt breakpoint, xem biến, call stack như ứng dụng thông thường.

---

### 2.2. Debug từng Service cụ thể (Multiple Startup Projects)

Dùng khi bạn chỉ quan tâm đến một vài service nhất định và muốn tiết kiệm tài nguyên máy.

**Cách thực hiện:**

1. Chuột phải vào **Solution 'Bizcore'** -> **Configure Startup Projects...**
2. Chọn **Multiple startup projects**.
3. Chỉnh `Action` sang **Start** cho các service cần debug (ví dụ: `Admin.API`, `Gateway.API`).
4. Nhấn **F5**.

> [!IMPORTANT]
> Với cách này, bạn cần đảm bảo các hạ tầng (Hệ quản trị DB, Message Broker) đã được chạy trước bằng lệnh:
> `docker-compose up -d sqlserver rabbitmq redis`

---

### 2.3. Debug Integration Tests

Cách nhanh nhất để debug logic xử lý dữ liệu và Business Rules mà không cần chạy toàn bộ UI hay Gateway.

**Cách thực hiện:**

1. Mở **Test Explorer** trong Visual Studio.
2. Tìm test case cần kiểm tra trong project `Bizcore.ApiTests`.
3. Chuột phải vào test đó và chọn **Debug**.

**Ưu điểm:**

- Sử dụng `Testcontainers` để tự tạo môi trường Database cô lập.
- Không cần cấu hình môi trường phức tạp.

---

## 3. Các công cụ hỗ trợ Debug

### 3.1. Dashboard Quản lý Containers

Trong Visual Studio, mở cửa sổ **View -> Other Windows -> Containers**. Tại đây bạn có thể:

- Xem log thời gian thực của Redis, RabbitMQ, SQL Server.
- Kiểm tra các biến môi trường (Environment Variables) của từng container.
- Truy cập trực tiếp vào Terminal bên trong container.

### 3.2. Hot Reload

Dự án hỗ trợ .NET Hot Reload. Bạn có thể sửa code C# trong khi đang debug và nhấn nút **Hot Reload** (biểu tượng ngọn lửa) để áp dụng thay đổi mà không cần khởi động lại toàn bộ hệ thống Docker.

---

## 4. Các lỗi thường gặp (Troubleshooting)

| Lỗi | Nguyên nhân | Cách xử lý |
| --- | ----------- | ---------- |
| **Docker not found** | Docker Desktop chưa chạy | Khởi động Docker Desktop và đảm bảo icon cá voi ở taskbar hiện màu xanh. |
| **Port already in use** | Có process khác đang chiếm port (ví dụ: SQL Server local) | Tắt các service local đang chạy hoặc dùng lệnh `docker-compose down` để giải phóng port. |
| **Breakpoint not hitting** | Symbol chưa được load hoặc file không khớp | Clean Solution và Rebuild lại project docker-compose. |

---

**Cập nhật lần cuối**: 12/05/2026  
**Duyệt bởi**: Kiến trúc sư trưởng
