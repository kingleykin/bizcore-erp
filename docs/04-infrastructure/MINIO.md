# MinIO Storage Integration

Tài liệu này hướng dẫn cách sử dụng và cấu hình MinIO trong hệ thống BizCore ERP để quản lý tệp tin và hình ảnh.

## 1. Tổng quan
MinIO là một giải pháp lưu trữ đối tượng (Object Storage) mã nguồn mở, tương thích hoàn toàn với API của Amazon S3. Trong BizCore ERP, MinIO được sử dụng để:
- Lưu trữ ảnh đại diện người dùng (Avatars).
- Lưu trữ các tệp đính kèm hóa đơn (Invoices).
- Lưu trữ các báo cáo xuất bản (Reports).

## 2. Cấu hình Docker
MinIO được triển khai qua Docker Compose với hai dịch vụ chính:
- **minio**: Server chính chạy trên cổng `9000` (API) và `9001` (Console).
- **minio-setup**: Chạy một lần để tạo các bucket mặc định (`bizcore-uploads`).

```yaml
  minio:
    image: minio/minio:latest
    container_name: bizcore-minio
    ports:
      - "9000:9000"
      - "9001:9001"
    environment:
      - MINIO_ROOT_USER=admin
      - MINIO_ROOT_PASSWORD=password
    volumes:
      - minio_data:/data
    command: server /data --console-address ":9001"
```

## 3. Kiến trúc phía Backend

### Building Block: Storage
Chúng tôi cung cấp một thư viện dùng chung `Bizcore.BuildingBlocks.Storage` để trừu tượng hóa việc giao tiếp với MinIO.

**Interface chính:** `IStorageService`
```csharp
public interface IStorageService
{
    Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, ...);
    Task<Stream> DownloadAsync(string fileName, ...);
    Task<string> GetPresignedUrlAsync(string fileName, int expiryInSeconds = 3600, ...);
}
```

### File Service (File.API)
Một microservice độc lập quản lý các thao tác tệp tin:
- **Endpoint**: `POST /api/v1/files/upload`
- **Endpoint**: `GET /api/v1/files/download/{fileName}`
- **Endpoint**: `GET /api/v1/files/view-url/{fileName}`

## 4. Cách sử dụng trong WebUI
Để tải tệp lên từ giao diện người dùng:
1. Gửi tệp tới `File.API` qua gateway (`/api/v1/files/upload`).
2. Nhận lại `fileName` (định danh duy nhất).
3. Sử dụng `view-url` để lấy đường dẫn hiển thị hoặc lưu trữ `fileName` vào cơ sở dữ liệu của service nghiệp vụ (ví dụ: `Admin.API`).

## 5. Quản trị
Truy cập MinIO Console tại: [http://localhost:9001](http://localhost:9001)
- **User**: `admin`
- **Password**: `password`
