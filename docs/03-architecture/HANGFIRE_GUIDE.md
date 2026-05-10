# Hangfire Background Jobs Guide

Tài liệu này hướng dẫn cách sử dụng **Hangfire** để quản lý các tác vụ chạy ngầm (background jobs) và tác vụ định kỳ (scheduled tasks) trong hệ thống Bizcore ERP.

---

## 1. Tổng quan (Overview)

Hangfire là một thư viện mã nguồn mở giúp thực hiện các tác vụ nền trong ASP.NET Core một cách bền bỉ (persistent). Khác với `IHostedService` hay `BackgroundService` mặc định, Hangfire lưu trữ thông tin job vào Database, đảm bảo job không bị mất ngay cả khi Server bị sập.

Trong hệ thống này, Hangfire chủ yếu được sử dụng tại **Audit Service** để xử lý các nghiệp vụ quản trị dữ liệu quy mô lớn.

## 2. Cấu hình (Configuration)

### 2.1 Cài đặt Package
Các service cần dùng Hangfire cần cài đặt:
- `Hangfire.AspNetCore`
- `Hangfire.SqlServer`

### 2.2 Đăng ký Service (`Program.cs`)
Hangfire sử dụng SQL Server để lưu trữ trạng thái của các job.

```csharp
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connStr));

builder.Services.AddHangfireServer(options => {
    options.WorkerCount = 2; // Số lượng job chạy song song
});
```

### 2.3 Middleware & Dashboard
Hangfire cung cấp một Dashboard rất mạnh mẽ để theo dõi và quản lý job.

```csharp
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter() }
});
```
> [!NOTE]
> Trong môi trường Production, dashboard cần được bảo mật qua các Policy của Identity Service.

## 3. Các loại Job được sử dụng

### 3.1 Recurring Jobs (Tác vụ định kỳ)
Dùng cho các công việc cần lặp lại theo thời gian (giống Cron job). Được cấu hình tại cuối file `Program.cs`.

**Ví dụ tại Audit Service:**
```csharp
RecurringJob.AddOrUpdate<RetentionCleanupJob>(
    "audit-retention-cleanup",
    job => job.ExecuteAsync(CancellationToken.None),
    Cron.Daily(2, 0)); // Chạy lúc 02:00 sáng hàng ngày
```

### 3.2 Background Jobs (Tác vụ chạy một lần)
Dùng để đẩy một công việc nặng ra khỏi luồng xử lý chính của API.

```csharp
BackgroundJob.Enqueue(() => Console.WriteLine("Hello, Hangfire!"));
```

## 4. Triển khai Job thực tế (Example)

Mỗi Job nên được tách ra thành một Class riêng trong thư mục `Application/Jobs` và đăng ký Scoped trong DI.

```csharp
public class RetentionCleanupJob
{
    private readonly AuditDbContext _db;
    private readonly ILogger<RetentionCleanupJob> _logger;

    public RetentionCleanupJob(AuditDbContext db, ILogger<RetentionCleanupJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Bắt đầu dọn dẹp dữ liệu Audit cũ...");
        // Logic nghiệp vụ (Xóa/Lưu trữ dữ liệu > 180 ngày)
    }
}
```

## 5. Giám sát & Quản trị (Monitoring)

Khi ứng dụng đang chạy, bạn có thể truy cập: `http://localhost:5xxx/hangfire` (tương ứng với port của service) để:
- **Jobs**: Xem danh sách các job đang chờ, đang chạy hoặc đã lỗi.
- **Retries**: Xem các job bị lỗi đang được hệ thống tự động thử lại.
- **Servers**: Xem danh sách các instance đang xử lý job.

## 6. Lưu ý quan trọng (Best Practices)

1. **Idempotency**: Các job có thể bị chạy lại nếu Server bị ngắt quãng. Hãy đảm bảo code trong Job có tính **idempotent** (chạy 1 lần hay 10 lần đều cho kết quả như nhau).
2. **CancellationToken**: Luôn truyền `CancellationToken` vào các phương thức Async để Hangfire có thể dừng job một cách an toàn khi Server tắt.
3. **Database Pre-creation**: Hangfire sẽ báo lỗi nếu Database chưa tồn tại khi nó khởi tạo. Luôn đảm bảo Database được tạo trước khi đăng ký Hangfire Service (Xem fix tại `Audit.API/Program.cs`).
4. **No UI Logic**: Không thực hiện các tác vụ liên quan đến Response/Request trong Job vì Job chạy độc lập với luồng Web.
