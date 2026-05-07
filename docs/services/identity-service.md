# Identity Service

## 🎯 Tổng quan (Overview)
**Identity Service** là trái tim bảo mật của BizCore ERP. Nó chịu trách nhiệm quản lý định danh người dùng (Authentication) và phân quyền chi tiết (Authorization) thông qua cơ chế Role-Based Access Control (RBAC) kết hợp Permission-based claims.

## 🧱 Cấu trúc (Architecture)
* **Port nội bộ**: `5005`
* **Cơ sở dữ liệu**: `IdentityDb` (SQL Server)
* **Công nghệ cốt lõi**:
  * **JWT (JSON Web Token)**: Cấp phát Access Token và Refresh Token cho xác thực phi trạng thái (Stateless Authentication).
  * **BCrypt**: Băm mật khẩu một chiều để bảo vệ an toàn thông tin đăng nhập.
  * **MassTransit (RabbitMQ)**: Publish các Audit Events (Login, Change Password, etc.) về Audit Service.

## 🔑 Các tính năng chính (Key Features)
1. **Xác thực (Authentication)**:
   - Đăng nhập với Username/Password.
   - Cấp phát Access Token (sống ngắn hạn) và Refresh Token (sống dài hạn).
   - Cơ chế xoay vòng Refresh Token (Refresh Token Rotation) để chống đánh cắp token.
   - Theo dõi số lần đăng nhập sai và khóa tài khoản tạm thời (Lockout Policy).
2. **Quản lý phân quyền (Authorization - RBAC)**:
   - Quản lý `Users`, `Roles` và `Permissions`.
   - Một User có thể có nhiều Role. Một Role chứa nhiều Permission (ví dụ: `Invoice.Create`, `Payment.View`).
   - Cấp phát các claim `permission` trực tiếp vào JWT Token để các Microservices khác có thể kiểm tra quyền tại chỗ mà không cần gọi lại Identity Service (Zero Trust).
3. **Quản lý Tài khoản (Account Management)**:
   - Đổi mật khẩu.
   - Revoke (thu hồi) Refresh Token khi cần thiết.

## 🔗 Endpoint API (API Endpoints)
Tất cả các endpoint được expose qua API Gateway với prefix `/api/v1/...`

| Endpoint | Method | Chức năng | Phân quyền yêu cầu |
| --- | --- | --- | --- |
| `/auth/login` | POST | Đăng nhập và lấy JWT | N/A (Anonymous) |
| `/auth/refresh` | POST | Làm mới Access Token | N/A |
| `/auth/change-password` | POST | Đổi mật khẩu | Yêu cầu JWT hợp lệ |
| `/users` | GET/POST | Quản lý người dùng | `Identity.Users.View/Create` |
| `/roles` | GET/POST | Quản lý Role và Permission | `Identity.Roles.View/Create` |

## 🛡️ Tích hợp Audit (Audit Integration)
Identity Service tạo ra rất nhiều dữ liệu nhạy cảm. Nó đã được tích hợp việc Publish `AuditEvent` về Audit Service cho các thao tác quan trọng ở mức **Security** như:
- `Auth.Login.Success`
- `Auth.Login.Failed`
- `Auth.ChangePassword`

Tất cả các event này đều trải qua cơ chế **Sensitive Field Masking** tự động trước khi serialize sang JSON để đảm bảo mật khẩu hoặc Token không bao giờ bị lộ trong hệ thống Audit.
