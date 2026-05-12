# 🧪 HƯỚNG DẪN KIỂM THỬ (TESTING GUIDE)

Tài liệu này hướng dẫn cách sử dụng và phát triển các loại test trong dự án **Bizcore ERP**. Hệ thống phân tách rõ ràng thành 3 cấp độ kiểm thử để đảm bảo tính ổn định và tin cậy.

---

## 1. 📂 Cấu trúc các Project Test

Tất cả các dự án test nằm trong thư mục `src/Tests/`:

- **`Bizcore.UnitTests`**: Kiểm thử logic nhỏ, cô lập (Units), sử dụng InMemory DB hoặc Mocks.
- **`Bizcore.ApiTests`**: Kiểm thử tích hợp (Integration), gọi trực tiếp vào API endpoints với các dependency thật (DB, Redis, RabbitMQ) chạy trong Docker containers.
- **`Bizcore.E2ETests`**: Kiểm thử xuyên suốt (End-to-End), giả lập hành vi người dùng trên trình duyệt thật (Playwright) tương tác với hệ thống đang chạy.

---

## 2. 🔗 API Integration Testing (`Bizcore.ApiTests`)

Dự án này sử dụng **Testcontainers** để tạo môi trường sạch cho mỗi lần chạy test.

### 🛠 Tech Stack

- **Framework**: xUnit
- **Hosting**: `Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory)
- **Infrastructure**: Docker + Testcontainers (MS SQL Server, Redis, RabbitMQ)
- **Assertion**: FluentAssertions

### 📋 Điều kiện tiên quyết

- Máy tính phải cài đặt và đang chạy **Docker Desktop** (hoặc Docker Engine).

### 🚀 Cách chạy

Mở Terminal tại root dự án và chạy:

```powershell
dotnet test src/Tests/Bizcore.ApiTests
```

### 💡 Lưu ý khi phát triển

- Các test class nên kế thừa từ `ApiTestBase<TEntryPoint>`.
- Mỗi lần khởi chạy, Testcontainers sẽ tự động cấp một Connection String mới -> đảm bảo tính cô lập hoàn toàn giữa các lần test.
- Hệ thống tự động chạy Migrations và Seed dữ liệu cho database container khi bắt đầu.

---

## 3. 🌐 End-to-End Testing (`Bizcore.E2ETests`)

Dự án này sử dụng **Playwright** để kiểm tra giao diện và luồng nghiệp vụ thực tế.

### 🛠 Tech Stack

- **Framework**: xUnit + Playwright for .NET
- **Browser**: Chromium (mặc định), Firefox, Webkit.

### 📋 Điều kiện tiên quyết

1. Toàn bộ hệ thống (Frontend, Gateway, Services, Infra) phải đang chạy (thường qua `docker-compose up -d`).
2. Cài đặt browsers cho Playwright (chỉ cần chạy một lần sau khi build):

```powershell
# Build project trước
dotnet build src/Tests/Bizcore.E2ETests

# Cài đặt browsers
pwsh src/Tests/Bizcore.E2ETests/bin/Debug/net10.0/playwright.ps1 install
```

### 🚀 Cách chạy

```powershell
# Chạy tất cả E2E tests
dotnet test src/Tests/Bizcore.E2ETests

# Chạy test và xem trình duyệt thực thi (Debug mode)
# Đặt biến môi trường HEADLESS=false hoặc chỉnh trong code
```

### 💡 Quy tắc viết Test

- Sử dụng `E2ETestBase` để có sẵn Page và Context.
- Sử dụng các selectors dựa trên `text` hoặc `role` để tăng tính ổn định (vd: `text=Hóa đơn`, `button:has-text('Xác nhận')`).
- Luôn sử dụng `await` cho mọi tương tác để tránh race condition.

---

## 4. 🛠 Xử lý sự cố (Troubleshooting)

### Lỗi Docker không chạy (ApiTests)

- **Triệu chứng**: Test treo lâu hoặc báo lỗi `Docker is not reachable`.
- **Xử lý**: Đảm bảo Docker Desktop đang chạy và User hiện tại có quyền thực thi docker.

### Lỗi Timeout hoặc Không tìm thấy Element (E2ETests)

- **Triệu chứng**: Test thất bại với lỗi `Timeout exceeded`.
- **Xử lý**:
  - Kiểm tra xem WebUI có đang chạy ở `http://localhost:5173` hay không.
  - Kiểm tra Gateway API có phản hồi không.
  - Tăng `SlowMo` hoặc chạy ở chế độ non-headless để quan sát.

### Lỗi gRPC/MassTransit trong ApiTests

- **Xử lý**: Kiểm tra log console của test để xem service có khởi tạo thành công không. Đôi khi cần override cấu hình gRPC endpoint trong `WebApplicationFactory`.

---

## 5. 📈 Kế hoạch mở rộng

- [ ] Tích hợp chạy Test vào CI/CD Pipeline (GitHub Actions).
- [ ] Bổ sung Code Coverage report.
- [ ] Triển khai Page Object Model (POM) cho dự án E2E để dễ bảo trì hơn.
