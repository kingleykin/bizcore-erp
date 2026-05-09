# bizcore-erp

# Triển khai

## Triển khai dự án bằng Docker

- Copy dự án nếu chạy Docker local dùng WSL: ```cp -r /mnt/d/Project/bizcore-erp ~/projects/```
- Run lệnh để build và chạy dự án:  ```docker compose up -d --build```
- Truy cập vào Giao diện (WebUI) tại: <http://localhost:3000>
- Truy cập vào API Gateway tại: <http://localhost:5001>
- Truy cập Portainer tại: <http://localhost:9000> mật khẩu: admin123456789
- Truy cập RabbitMQ tại: <http://localhost:15672>

## Clear project

- Xóa toàn bộ: ```dotnet clean```
- Remove all folderbin

```
    cd /src
    Remove-Item -Path "Services\Invoice\Invoice.API\obj" -Recurse -Force
    Remove-Item -Path "Services\Invoice\Invoice.API\bin" -Recurse -Force

    Remove-Item -Path "Services\Payment\Payment.API\obj" -Recurse -Force
    Remove-Item -Path "Services\Payment\Payment.API\bin" -Recurse -Force

    Remove-Item -Path "Services\Report\Report.API\obj" -Recurse -Force
    Remove-Item -Path "Services\Report\Report.API\bin" -Recurse -Force

    Remove-Item -Path "Services\Orchestration\Orchestration.API\obj" -Recurse -Force
    Remove-Item -Path "Services\Orchestration\Orchestration.API\bin" -Recurse -Force

    Remove-Item -Path "BuildingBlocks\Bizcore.BuildingBlocks\obj" -Recurse -Force
    Remove-Item -Path "BuildingBlocks\Bizcore.BuildingBlocks\bin" -Recurse -Force

    Remove-Item -Path "Services\Identity\Identity.API\obj" -Recurse -Force
    Remove-Item -Path "Services\Identity\Identity.API\bin" -Recurse -Force

    Remove-Item -Path "Gateway\Gateway.API\obj" -Recurse -Force
    Remove-Item -Path "Gateway\Gateway.API\bin" -Recurse -Force

    Remove-Item -Path "Services\Audit\Audit.API\obj" -Recurse -Force
    Remove-Item -Path "Services\Audit\Audit.API\bin" -Recurse -Force

    Remove-Item -Path "Tests\Bizcore.UnitTests\obj" -Recurse -Force
    Remove-Item -Path "Tests\Bizcore.UnitTests\bin" -Recurse -Force

```

# Demoz

## Tạo Hóa đơn

- Truy cập vào dự án tại: <http://localhost:5001>
- Login: "user" / "password"
- Tạo hóa đơn

## Thanh toán hóa đơn

- Truy cập vào dự án tại: <http://localhost:5001>
- Login: "user" / "password"
- Thanh toán hóa đơn

---

## Xử lý sự cố (Troubleshooting)

### Lỗi xung đột cổng 5000 trên macOS
Trên các phiên bản macOS mới (Monterey trở đi), cổng **5001** thường được sử dụng bởi tính năng **AirPlay Receiver**. Nếu bạn không thể truy cập API Gateway hoặc Docker báo lỗi cổng đã bị chiếm dụng, hãy thực hiện:
1. Vào **System Settings** (Cài đặt hệ thống).
2. Chọn **General** -> **AirDrop & Handoff**.
3. Tắt **AirPlay Receiver** (Bộ thu AirPlay).
4. Khởi động lại Docker containers: `docker compose up -d`.
