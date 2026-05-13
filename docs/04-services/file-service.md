# 📂 File Service (Quản lý Tệp tin)

Dịch vụ File Service cung cấp cơ chế quản lý tệp tin tập trung cho toàn bộ hệ thống BizCore ERP, sử dụng **MinIO** làm hạ tầng lưu trữ Object Storage.

---

## 🏗️ 1. Cấu trúc Thành phần

Hệ thống quản lý tệp tin được chia làm hai phần:

### 🔹 Building Block: `Bizcore.BuildingBlocks.Storage`
Thư viện dùng chung cung cấp các trừu tượng hóa và logic xử lý MinIO.
- **`IStorageService`**: Interface định nghĩa các thao tác (Upload, Download, Delete, GetUrl).
- **`MinioStorageService`**: Triển khai thực tế sử dụng MinIO SDK. Hỗ trợ cơ chế tự động chuyển đổi giữa URL nội bộ (cho server) và URL bên ngoài (cho browser).
- **`MinioOptions`**: Chứa cấu hình kết nối (Endpoint, AccessKey, SecretKey, BucketName, ExternalEndpoint).
- **`StorageModule`**: Tự động đăng ký các dịch vụ cần thiết vào DI Container.

### 🔹 Microservice: `File.API`
Cung cấp các API HTTP để các Client (WebUI) hoặc Microservices khác thao tác với tệp tin.
- Chịu trách nhiệm tiếp nhận file upload qua `MultipartFormData`.
- Phối hợp với Gateway (YARP) để điều phối request.

---

## 🚀 2. API Endpoints

Mọi request đều được đi qua Gateway tại prefix `/api/v1/files/`.

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| **POST** | `/api/v1/files/upload` | Tải tệp lên hệ thống. Trả về `fileName` (GUID). |
| **GET** | `/api/v1/files/view-url/{fileName}` | Lấy URL công khai để hiển thị ảnh/tài liệu trên trình duyệt. |
| **GET** | `/api/v1/files/download/{fileName}` | Tải file về dưới dạng stream (binary). |
| **DELETE** | `/api/v1/files/{fileName}` | Xóa tệp khỏi hệ thống. |

---

## ⚙️ 3. Cấu hình (Configuration)

Các tham số cấu hình trong `appsettings.json` hoặc Environment Variables:

```json
"Minio": {
  "Endpoint": "minio:9000",
  "ExternalEndpoint": "http://localhost:9005",
  "AccessKey": "admin",
  "SecretKey": "password",
  "BucketName": "bizcore-uploads",
  "UseSSL": false
}
```

*   **Endpoint**: Địa chỉ MinIO trong mạng nội bộ (Docker).
*   **ExternalEndpoint**: Địa chỉ MinIO mà trình duyệt bên ngoài có thể truy cập (dùng để sinh link hiển thị).
*   **BucketName**: Tên vùng chứa dữ liệu (mặc định: `bizcore-uploads`).

---

## 🛠️ 4. Hướng dẫn sử dụng cho Developer

### Đăng ký dịch vụ (trong Microservice mới)
Để sử dụng Storage trong một service khác (VD: Invoice), hãy đăng ký module:

```csharp
builder.Services.AddBizcoreModule<StorageModule>(builder);
```

### Sử dụng trong Code
Tiêm `IStorageService` vào Constructor:

```csharp
public class MyService(IStorageService storageService)
{
    public async Task DoSomething()
    {
        // Tải link hiển thị
        var url = await storageService.GetPresignedUrlAsync("my-file.jpg");
    }
}
```

### Luồng Upload từ WebUI
1.  WebUI gọi `POST /api/v1/files/upload` kèm file.
2.  `File.API` lưu vào MinIO và trả về `fileName`.
3.  WebUI lưu `fileName` này vào database của service nghiệp vụ (ví dụ cột `AvatarUrl` trong `Admin.API`).
4.  Khi hiển thị, WebUI gọi `GET /api/v1/files/view-url/{fileName}` để lấy link ảnh thật.

---

## 🛡️ 5. Bảo mật & Lưu ý
*   **Public Access**: Hiện tại bucket `bizcore-uploads` được cấu hình Public để tối ưu tốc độ hiển thị ảnh đại diện (không cần chữ ký Signature).
*   **Cơ chế Bù trừ**: Nếu upload thành công nhưng update DB nghiệp vụ thất bại, cần có cơ chế xóa file rác (cleanup job).
