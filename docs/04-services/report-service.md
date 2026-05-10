# Report Service

## 🎯 Tổng quan (Overview)
**Report Service** là dịch vụ chịu trách nhiệm tổng hợp, thống kê số liệu và cung cấp góc nhìn toàn cảnh về tình hình kinh doanh cho ban giám đốc. Nó được thiết kế chuyên biệt cho việc Đọc (Read-heavy) thay vì Ghi (Write-heavy).

## 🧱 Cấu trúc (Architecture)
* **Port nội bộ**: `5003`
* **Cơ sở dữ liệu**: `ReportDb` (SQL Server) - Tuy nhiên dữ liệu của nó chủ yếu là các bảng tổng hợp (Aggregated Tables).

## 🚀 Tối ưu hóa hiệu năng (Performance Optimization)
Report Service thường phải đối mặt với các truy vấn phức tạp (grouping, sum, count) trên tập dữ liệu lớn. Để đảm bảo trải nghiệm người dùng tức thời, service sử dụng:
1. **Memory Caching**: Cache lại các kết quả thống kê chậm thay đổi hoặc báo cáo Dashboard theo phiên (session). Giúp giảm tải đáng kể cho Database.
2. **Read Models**: Dữ liệu có thể được chuẩn bị sẵn thông qua việc consume events từ Invoice/Payment, lưu dưới dạng Read Models thay vì query JOIN trực tiếp vào các hệ thống Transactional.

## 🔗 Endpoint API (API Endpoints)
| Endpoint | Method | Chức năng | Phân quyền yêu cầu |
| --- | --- | --- | --- |
| `/api/v1/report/summary` | GET | Cung cấp KPI, thống kê doanh thu, hóa đơn Dashboard | `Report.View` |
