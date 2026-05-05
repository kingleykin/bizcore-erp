# bizcore-erp

# Triển khai

## Triển khai dự án bằng Docker

- Copy dự án nếu chạy Docker local dùng WSL: ```cp -r /mnt/d/Project/bizcore-erp ~/projects/```
- Run lệnh để build và chạy dự án:  ```docker compose up -d --build```
- Truy cập vào Giao diện (WebUI) tại: <http://localhost:3000>
- Truy cập vào API Gateway tại: <http://localhost:5000>
- Truy cập Portainer tại: <http://localhost:9000> mật khẩu: admin123456789
- Truy cập RabbitMQ tại: <http://localhost:15672>

# Demo

## Tạo Hóa đơn

- Truy cập vào dự án tại: <http://localhost:5000>
- Login: "user" / "password"
- Tạo hóa đơn

## Thanh toán hóa đơn

- Truy cập vào dự án tại: <http://localhost:5000>
- Login: "user" / "password"
- Thanh toán hóa đơn
