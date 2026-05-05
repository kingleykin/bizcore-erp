# 📘 Tài liệu Cấu trúc Dự án (Project Structure)

Tài liệu này giải thích chi tiết về cách tổ chức mã nguồn, kiến trúc hệ thống và lý do đằng sau các quyết định thiết kế cho dự án BizCore CRM.

---

## 🎯 1. Mục tiêu kiến trúc (Architecture Goals)

Kiến trúc này được thiết kế để giải quyết 3 bài toán chính:

1. **Khả năng Scale (Scalability)**: Dễ dàng tách các service ra các server riêng biệt hoặc repository riêng biệt khi hệ thống phát triển.
2. **Khả năng Bảo trì (Maintainability)**: Phân tách rõ ràng giữa Logic nghiệp vụ (Domain) và Công nghệ (Infrastructure).
3. **Tốc độ Phát triển (Velocity)**: Cấu trúc đủ đơn giản để triển khai nhanh trong 2 ngày nhưng đủ chuẩn để không phải đập đi xây lại sau này.

---

## 🧱 2. Cấu trúc Hệ thống (System Structure)

Dự án tuân thủ mô hình **Macro-level: Microservices** và **Micro-level: Domain-Driven Design (DDD) Lite**.

### 🔹 Macro-level (Kiến trúc tổng thể)

* **API Gateway (YARP)**: Đóng vai trò là "người gác cổng". Toàn bộ WebUI chỉ giao tiếp qua Gateway này. Giúp ẩn đi sự phức tạp của các port nội bộ và tập trung xử lý CORS/Auth tại một điểm.
* **Microservices**: Mỗi service (Invoice, Payment, Report) quản lý một vùng dữ liệu và nghiệp vụ độc lập (Bounded Context).
* **BuildingBlocks (Bizcore.BuildingBlocks)**: Thư viện dùng chung chứa các thành phần có thể tái sử dụng giữa các Microservices. Đây là nơi định nghĩa các **Contracts** (Event interfaces) để đảm bảo tính nhất quán khi giao tiếp qua Message Broker.
* **Message Broker (RabbitMQ)**: Cung cấp cơ chế giao tiếp bất đồng bộ. Giúp các service giảm bớt sự phụ thuộc trực tiếp vào nhau (Decoupling).

### 🔹 Micro-level (Cấu trúc nội bộ Service)

Mỗi service được tổ chức thành 4 lớp (folders) bên trong project API:

1. **Domain Layer**:
    * *Nội dung*: Entities, Enums, Interfaces cốt lõi.
    * *Quy tắc*: Không phụ thuộc vào bất kỳ thư viện bên ngoài nào (kể cả EF Core hay ASP.NET). Đây là "trái tim" của ứng dụng.
2. **Application Layer**:
    * *Nội dung*: Interfaces Services, Implementation của Services.
    * *Quy tắc*: Chứa logic nghiệp vụ chính (Use Cases). Điều phối dữ liệu từ Infrastructure để trả về cho API.
3. **Infrastructure Layer**:
    * *Nội dung*: DbContext, Migrations, External Clients.
    * *Quy tắc*: Chứa các chi tiết triển khai kỹ thuật (Data Access).
4. **API Layer (Controllers)**:
    * *Nội dung*: Controllers, Program.cs, Configuration.
    * *Quy tắc*: Chỉ làm nhiệm vụ nhận request và trả về response. **Tuyệt đối không viết logic nghiệp vụ tại đây.**

---

## ⚙️ 3. Chức năng chính (Key Functions)

* **Gateway Routing**: Điều phối thông minh các request dựa trên Prefix URL.
* **Shared DB Strategy**: Sử dụng chung một SQL Server Instance nhưng phân tách bảng theo Domain. Điều này giúp tối ưu chi phí và tốc độ trong giai đoạn đầu nhưng vẫn sẵn sàng để tách DB bất cứ lúc nào.
* **DI (Dependency Injection)**: Toàn bộ Services được đăng ký trong DI Container để đảm bảo tính Loose Coupling (kết nối lỏng lẻo).

---

## ❓ 4. Tại sao lại cấu trúc như vậy? (The "Why")

| Quyết định | Lý do (Rationale) |
| :--- | :--- |
| **Tại sao dùng 1 Project/Service?** | Để giảm thiểu overhead của việc quản lý quá nhiều project trong Solution cho một dự án demo 2 ngày, trong khi vẫn đảm bảo phân lớp folder bên trong. |
| **Tại sao tách lớp Application?** | Để khi bạn cần chuyển sang Unit Test, bạn chỉ cần test lớp Application Service mà không cần quan tâm đến HTTP Request/Response của Controller. |
| **Tại sao dùng YARP?** | YARP linh hoạt hơn các Gateway tĩnh, cho phép chúng ta can thiệp vào pipeline (như Transforms, Auth) bằng code C# quen thuộc. |
| **Tại sao dùng Shared DB?** | Việc duy trì 3 DB riêng biệt cho demo 2 ngày sẽ gây khó khăn cho việc migrate và chạy local (tốn tài nguyên). Shared DB với naming convention tốt là sự cân bằng hoàn hảo giữa tốc độ và chuẩn hóa. |
| **Tại sao cần BuildingBlocks?** | Trong Microservices, khi Service A gửi message cho Service B, cả hai cần đồng thuận về cấu trúc dữ liệu (Contract). Việc để Contract ở một thư viện dùng chung giúp tránh lỗi sai lệch schema và giảm thiểu code dư thừa (DRY). |
| **Tại sao dùng RabbitMQ?** | Để thực hiện luồng cập nhật trạng thái Hóa đơn một cách bất đồng bộ. Payment Service không cần biết Invoice Service xử lý thế nào, nó chỉ cần "thông báo" rằng thanh toán đã xong. |

---

## 🚀 5. Lộ trình Mở rộng (Scaling Roadmap)

Nếu dự án cần scale lên 100k+ người dùng:

1. **Database per Service**: Tách DB SQL Server ra 3 instance riêng.
2. **Advanced EDA**: Áp dụng các pattern như Outbox Pattern để đảm bảo tính nhất quán dữ liệu (Data Consistency) khi gửi message.
3. **Clean Architecture Full**: Tách các folder `Domain`, `Application`, `Infrastructure` thành các Project `.csproj` riêng biệt.

---
*Tài liệu này phục vụ mục đích hiểu sâu về tư duy thiết kế hệ thống.*
