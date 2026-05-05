# 📘 Tài liệu Cấu trúc Dự án (Project Structure)

Tài liệu này giải thích chi tiết về cách tổ chức mã nguồn, kiến trúc hệ thống và lý do đằng sau các quyết định thiết kế cho dự án BizCore ERP.

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
* **BuildingBlocks (Bizcore.BuildingBlocks)**: Thư viện dùng chung chứa các thành phần có thể tái sử dụng giữa các Microservices.
  * **Contracts**: Định nghĩa Event interfaces cho giao tiếp EDA.
  * **Permissions**: Định nghĩa tập trung toàn bộ các hành động (Fine-grained actions) của hệ thống phục vụ Permission-based Authorization.
* **Message Broker (RabbitMQ)**: Cung cấp cơ chế giao tiếp bất đồng bộ. Giúp các service giảm bớt sự phụ thuộc trực tiếp vào nhau (Decoupling).

### 🔹 Security Architecture (Kiến trúc Bảo mật)

Hệ thống áp dụng mô hình bảo mật nhiều lớp:

1. **Edge Security (Gateway)**:
    * **Rate Limiting**: Ngăn chặn spam request ở tầng Gateway.
    * **Mock Identity Provider**: Tích hợp sẵn endpoint `/auth/login` để cấp mã JWT Token cho mục đích demo và kiểm thử.
2. **Zero Trust (Services)**:
    * Mọi Microservice đều tự thực hiện việc kiểm tra chữ ký của JWT Token (không chỉ tin tưởng Gateway).
    * Áp dụng **Permission-based Authorization**: Mỗi API Endpoint yêu cầu một Policy cụ thể (ví dụ: `Invoice.Create`). User phải có đúng claim `permission` mới có thể thực hiện.

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
* **Observability**: Tích hợp **Serilog** đồng nhất cho toàn bộ hệ thống, hỗ trợ ghi log có cấu trúc (Structured Logging) ra Console và có thể mở rộng ra ELK Stack.
* **Hardening**: Cấu hình Security Headers và giới hạn kích thước Payload để bảo vệ các service.

---

## ❓ 4. Tại sao lại cấu trúc như vậy? (The "Why")

| Quyết định | Lý do (Rationale) |
| :--- | :--- |
| **Tại sao dùng 1 Project/Service?** | Để giảm thiểu overhead của việc quản lý quá nhiều project trong Solution cho một dự án demo 2 ngày, trong khi vẫn đảm bảo phân lớp folder bên trong. |
| **Tại sao tách lớp Application?** | Để khi bạn cần chuyển sang Unit Test, bạn chỉ cần test lớp Application Service mà không cần quan tâm đến HTTP Request/Response của Controller. |
| **Tại sao dùng YARP?** | YARP linh hoạt hơn các Gateway tĩnh, cho phép chúng ta can thiệp vào pipeline (như Transforms, Auth, RateLimit) bằng code C# quen thuộc. |
| **Tại sao dùng Permission-based?** | Để tránh tình trạng **Role Explosion**. Permission-based cho phép phân quyền chi tiết (Granular) và dễ dàng scale khi số lượng chức năng của hệ thống tăng lên. |
| **Tại sao dùng Shared DB?** | Việc duy trì 3 DB riêng biệt cho demo 2 ngày sẽ gây khó khăn cho việc migrate và chạy local (tốn tài nguyên). Shared DB với naming convention tốt là sự cân bằng hoàn hảo giữa tốc độ và chuẩn hóa. |
| **Tại sao cần BuildingBlocks?** | Trong Microservices, khi Service A gửi message cho Service B, cả hai cần đồng thuận về cấu trúc dữ liệu (Contract). Việc để Contract ở một thư viện dùng chung giúp tránh lỗi sai lệch schema và giảm thiểu code dư thừa (DRY). |
| **Tại sao dùng RabbitMQ?** | Để thực hiện luồng cập nhật trạng thái Hóa đơn một cách bất đồng bộ. Payment Service không cần biết Invoice Service xử lý thế nào, nó chỉ cần "thông báo" rằng thanh toán đã xong. |
| **Tại sao tách Validation?** | Tách biệt giữa **Input Validation** (FluentValidation) và **Domain Validation** (Business Rules) giúp mã nguồn sạch hơn, dễ bảo trì và thể hiện tư duy kiến trúc phân lớp chuyên nghiệp. |

---

## 🛡️ 5. Chiến lược Validation (Validation Strategy)

Hệ thống phân tách rõ ràng hai loại validation để đảm bảo tính scale và bảo trì:

1. **Input Validation (FluentValidation)**:
    * *Vị trí*: Nằm ở tầng API/Application thông qua các `Validator`.
    * *Mục tiêu*: Kiểm tra định dạng dữ liệu, các trường bắt buộc, độ dài chuỗi... trước khi dữ liệu đi vào logic nghiệp vụ. Trả về `400 Bad Request` với danh sách lỗi chi tiết.
2. **Domain Validation (Business Rules)**:
    * *Vị trí*: Nằm trong các **Entities** (ví dụ: `Invoice.Create()`).
    * *Mục tiêu*: Kiểm tra các quy tắc nghiệp vụ logic (ví dụ: "Hạn mức hóa đơn không được vượt quá 1 triệu"). Ném ra `DomainException` và được bắt bởi `HttpExceptionFilter` để trả về lỗi thân thiện cho client.

## 🚀 6. Lộ trình Mở rộng (Scaling Roadmap)

Nếu dự án cần scale lên 100k+ người dùng:

1. **Database per Service**: Tách DB SQL Server ra 3 instance riêng.
2. **Advanced EDA**: Áp dụng các pattern như Outbox Pattern để đảm bảo tính nhất quán dữ liệu (Data Consistency) khi gửi message.
3. **Clean Architecture Full**: Tách các folder `Domain`, `Application`, `Infrastructure` thành các Project `.csproj` riêng biệt.

---
*Tài liệu này phục vụ mục đích hiểu sâu về tư duy thiết kế hệ thống.*
