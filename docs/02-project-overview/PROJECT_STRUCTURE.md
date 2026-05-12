# 📘 Tài liệu Cấu trúc Dự án (Project Structure)

Tài liệu này giải thích chi tiết về cách tổ chức mã nguồn, kiến trúc hệ thống và lý do đằng sau các quyết định thiết kế cho dự án BizCore ERP.

---

## 🎯 1. Mục tiêu kiến trúc (Architecture Goals)

Kiến trúc này được thiết kế để giải quyết 3 bài toán chính:

1. **Khả năng Scale (Scalability)**: Dễ dàng tách các service ra các server riêng biệt hoặc repository riêng biệt khi hệ thống phát triển.
2. **Khả năng Bảo trì (Maintainability)**: Phân tách rõ ràng giữa Logic nghiệp vụ (Domain) và Công nghệ (Infrastructure).
3. **Production-Ready**: Hệ thống áp dụng các best practices (Identity, RBAC, JWT, Outbox Pattern) sẵn sàng cho môi trường thực tế.

---

## 🧱 2. Cấu trúc Hệ thống (System Structure)

Dự án tuân thủ mô hình **Macro-level: Microservices** và **Micro-level: Domain-Driven Design (DDD) Lite**.

### 🔹 Macro-level (Kiến trúc tổng thể)

* **API Gateway (YARP)**: Đóng vai trò là "người gác cổng". Toàn bộ WebUI chỉ giao tiếp qua Gateway này. Giúp ẩn đi sự phức tạp của các port nội bộ và tập trung xử lý CORS/Auth tại một điểm.
* **Microservices**: Mỗi service quản lý một vùng dữ liệu và nghiệp vụ độc lập (Bounded Context). Bao gồm: **Admin** (Master Data, Auth), **Accounting** (Core Ledger & Batch), **Invoice** (AR/AP), **Payment** (Treasury), **Report**. Thêm **Orchestration.API** chỉ làm **read-side orchestration**: lắng nghe các event domain và lưu timeline để luồng giao dịch minh bạch. Thêm **Audit.API** làm Centralized Audit để lưu vết mọi thao tác với Hash chain.
* **BuildingBlocks (Bizcore.BuildingBlocks)**: Thư viện dùng chung chứa các thành phần tái sử dụng.
  * **Contracts**: Định nghĩa Event/Command interfaces.
  * **Permissions**: Định nghĩa tập trung toàn bộ các hành động.
  * **Infrastructure**: Chứa các Extension Methods cho `Program.cs` và `IServiceModule`.
* **gRPC**: Cung cấp giao tiếp đồng bộ hiệu năng cao cho các truy vấn Read-only.
* **Message Broker (RabbitMQ)**: Cung cấp cơ chế giao tiếp bất đồng bộ. Giúp các service giảm bớt sự phụ thuộc trực tiếp vào nhau (Decoupling).

### 🔹 Security Architecture (Kiến trúc Bảo mật)

Hệ thống áp dụng mô hình bảo mật nhiều lớp:

1. **Edge Security (Gateway)**:
    * **Rate Limiting**: Ngăn chặn spam request ở tầng Gateway.
    * **Admin Service**: Microservice độc lập chuyên xử lý Authentication (JWT, BCrypt) và phân quyền **Dynamic Authorization** (Roles/Permissions) với Redis Caching.
2. **Zero Trust (Services)**:
    * Mọi Microservice đều tự thực hiện việc kiểm tra chữ ký của JWT Token (không chỉ tin tưởng Gateway).
    * Áp dụng **Permission-based Authorization**: Mỗi API Endpoint yêu cầu một Permission cụ thể (ví dụ: `Invoice.Create`). User phải có quyền tương ứng trong Redis Cache hoặc JWT mới có thể thực hiện.
    * **Real-time Refresh**: Quyền hạn được cập nhật tức thì toàn hệ thống mà không cần người dùng đăng nhập lại.

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
* **Logical DB Isolation**: Mặc dù sử dụng chung một SQL Server Engine để tiết kiệm tài nguyên, nhưng mỗi Microservice kết nối đến một logical Database riêng biệt (IdentityDb, InvoiceDb...). Điều này chuẩn bị sẵn sàng cho việc tách DB vật lý.
* **DI (Dependency Injection)**: Toàn bộ Services được đăng ký trong DI Container để đảm bảo tính Loose Coupling (kết nối lỏng lẻo).
* **Observability**: Tích hợp **LGTM Stack** (Loki, Grafana, Tempo, Prometheus) kết hợp với **OTEL Collector**. Cho phép theo dõi hành trình của một request xuyên suốt các microservices, phân tích performance, và tìm root cause nhanh chóng qua Distributed Tracing.
* **Operability**: Hệ thống cung cấp các endpoint `/health` theo chuẩn Cloud-native, giúp các công cụ điều phối (Docker, K8s) nhận biết tình trạng sức khỏe của service.
* **Resilience**: Áp dụng **Global Exception Middleware** để đảm bảo hệ thống không bao giờ bị "sập" và luôn trả về phản hồi có cấu trúc cho người dùng.
* **Hardening**: Cấu hình Security Headers và giới hạn kích thước Payload để bảo vệ các service.
* **Business Compensation**: Nếu luồng bất đồng bộ liên service lỗi nghiệp vụ, hệ thống dùng event compensation để đưa trạng thái thanh toán về `Reversed` thay vì rollback transaction xuyên service.
* **Compliance & Security**: Áp dụng **Centralized Audit**.
* **Module Pattern**: Sử dụng `IServiceModule` để tách biệt cấu hình host (`Program.cs`) khỏi đăng ký dịch vụ (`Module.cs`), giúp codebase sạch và dễ scale theo module.

---

## ❓ 4. Tại sao lại cấu trúc như vậy? (The "Why")

| Quyết định | Lý do (Rationale) |
| :--- | :--- |
| **Tại sao dùng 1 Project/Service?** | Để giảm thiểu overhead của việc quản lý dự án demo, trong khi vẫn đảm bảo phân lớp folder bên trong. |
| **Tại sao dùng Correlation ID?** | Trong Microservices, việc debug một lỗi đi qua 3-4 service là cực kỳ khó khăn. Correlation ID giúp kết nối các dòng log rời rạc thành một câu chuyện hoàn chỉnh. |
| **Tại sao dùng Loki + Prometheus + Grafana?** | Để có khả năng observability toàn diện. Loki tập trung logs có cấu trúc, Prometheus thu thập metrics performance, Grafana cung cấp visualization tổng hợp. Giúp phát hiện và giải quyết sự cố nhanh trong môi trường distributed. |
| **Tại sao dùng Global Middleware?** | Để đảm bảo tính nhất quán (Consistency). Dù lỗi xảy ra ở đâu, client luôn nhận được một format JSON đồng nhất kèm theo `TraceId` để phản hồi cho hỗ trợ kỹ thuật. |
| **Tại sao dùng Memory Caching?** | Đối với các dữ liệu nặng về tính toán như báo cáo Dashboard, việc cache kết quả giúp giảm tải cho Database và mang lại trải nghiệm người dùng tức thì. |
| **Tại sao tách lớp Application?** | Để khi bạn cần chuyển sang Unit Test, bạn chỉ cần test lớp Application Service mà không cần quan tâm đến HTTP Request/Response của Controller. |
| **Tại sao dùng YARP?** | YARP linh hoạt hơn các Gateway tĩnh, cho phép chúng ta can thiệp vào pipeline (như Transforms, Auth, RateLimit) bằng code C# quen thuộc. |
| **Tại sao dùng Dynamic Authorization?** | Để tránh tình trạng **Role Explosion**. Permission-based kết hợp Redis giúp phân quyền chi tiết (Granular), hiệu năng cao và có thể thay đổi quyền hạn runtime mà không cần cấp lại JWT. |
| **Tại sao dùng Logical DB Isolation?** | Việc duy trì nhiều DB vật lý riêng biệt tốn tài nguyên. Dùng chung 1 Server nhưng cấp phát các Logical DB (AdminDb, InvoiceDb, PaymentDb) đảm bảo tính cách ly dữ liệu (Data Isolation) theo đúng chuẩn Microservices nhưng vẫn dễ cấu hình. |
| **Tại sao cần BuildingBlocks?** | Trong Microservices, khi Service A gửi message cho Service B, cả hai cần đồng thuận về cấu trúc dữ liệu (Contract). Việc để Contract ở một thư viện dùng chung giúp tránh lỗi sai lệch schema và giảm thiểu code dư thừa (DRY). |
| **Tại sao dùng RabbitMQ?** | Để thực hiện luồng cập nhật trạng thái Hóa đơn một cách bất đồng bộ. Payment Service không cần biết Invoice Service xử lý thế nào, nó chỉ cần "thông báo" rằng thanh toán đã xong. |
| **Tại sao tách Validation?** | Tách biệt giữa **Input Validation** (FluentValidation) và **Domain Validation** (Business Rules) giúp mã nguồn sạch hơn, dễ bảo trì và thể hiện tư duy kiến trúc phân lớp chuyên nghiệp. |
| **Tại sao dùng Outbox Pattern?** | Để giải quyết bài toán "Lưu DB xong nhưng mất điện không kịp bắn Message". Outbox đảm bảo message chỉ được gửi đi khi và chỉ khi DB đã commit thành công. |
| **Tại sao cần Compensation (`Reversed`)?** | Payment và Invoice là 2 bounded context độc lập nên không rollback bằng 1 transaction chung. Compensation giúp rollback ở mức nghiệp vụ khi Invoice không cập nhật được trạng thái sau khi Payment đã thành công. |
| **Tại sao dùng Centralized Audit Service thay vì Interceptor cục bộ?** | Audit data phát triển rất nhanh, cần DB riêng và policy lưu trữ (Retention) riêng. Việc tách ra giúp các service core không bị phình to DB. Hơn nữa, nó tăng tính bảo mật (Immutable, Hash chain) vì attacker dù có chiếm được service core cũng không sửa được log trên AuditDb. |
| **Tại sao dùng Hybrid Trigger cho Audit?** | Application layer publish event giúp hiểu rõ Business Intent (VD: "Approve Invoice"). EF Interceptor tự động catch field-level thay đổi (VD: "Amount 100 -> 200"). Kết hợp cả 2 cho cái nhìn hoàn hảo về compliance. |
| **Tại sao dùng Polly?** | Để hệ thống có khả năng tự phục hồi (Self-healing). Nếu service đích bận, Gateway sẽ tự động thử lại (Retry) thay vì trả lỗi ngay lập tức cho người dùng. |
| **Tại sao dùng Idempotency?** | Đặc biệt quan trọng với thanh toán. Nếu mạng lag và user bấm "Thanh toán" 2 lần, hệ thống sẽ chỉ xử lý 1 lần dựa trên Idempotency Key, tránh trừ tiền 2 lần. |
| **Tại sao dùng API Versioning?** | Để hỗ trợ tiến hóa hệ thống. Khi có thay đổi lớn (Breaking Change), chúng ta có thể triển khai V2 trong khi các Client cũ vẫn dùng V1 bình thường. |

---

## 3. Kiến trúc Audit Service (Production-ready)

Hệ thống ERP yêu cầu truy xuất nguồn gốc (traceability) nghiêm ngặt để phục vụ Compliance và hỗ trợ sửa sai dữ liệu (Data Correction). Thiết kế được chọn là **Centralized Audit Service** kết hợp **Hybrid Trigger**.

### Quyết định Thiết kế cốt lõi

1.  **Hybrid Trigger (Event + Interceptor)**:
    *   **Application Layer**: Publish Business Events (ví dụ: `PaymentCompleted`, `LoginFailed`) để ghi nhận ý nghĩa nghiệp vụ.
    *   **EF Core `SaveChangesInterceptor`**: Tự động capture sự thay đổi ở cấp độ Field (Before/After) mỗi khi gọi `SaveChanges()`. Bảo vệ hệ thống khỏi rủi ro Dev quên viết log.

2.  **Toàn vẹn dữ liệu (Data Integrity)**:
    *   Database được cấu hình **Append-only** (DENY UPDATE/DELETE) đối với tài khoản service.
    *   Sử dụng **Hash Chain (SHA-256)**: Mỗi AuditEntry chứa Hash của chính nó + Hash của bản ghi trước đó. Nếu bất kỳ dữ liệu cũ nào bị thay đổi lén lút, toàn bộ chuỗi Hash sau đó sẽ bị sai lệch, giúp phát hiện lập tức (Tamper Detection).

3.  **Data Masking**:
    *   Sử dụng `SensitiveFieldMasker` để thay thế thông tin nhạy cảm (Password, Token, PII) bằng chuỗi `***` *trước khi* lưu vào Database.

4.  **Audit-Assisted Recovery (Data Correction / Reversal)**:
    *   Phân biệt rạch ròi giữa **Business Compensation** (hủy giao dịch tài chính tự động) và **Admin Data Correction** (sửa lỗi nhập liệu thủ công).
    *   **Không tự động ghi đè (No Snapshot Overwrite)**: Tránh việc revert lại một phiên bản Entity cũ đã bị lỗi thời (Stale Data).
    *   **Restore Suggestion**: Audit Service chỉ cung cấp `BeforeJson`. `RestoreDiffEngine` so sánh `BeforeJson` với State hiện tại để gợi ý các trường có thể khôi phục.
    *   **Dynamic Policy (`IReversalPolicy<T>`)**: Quyết định field nào được khôi phục dựa trên ngữ cảnh: chặn các field tài chính (`Amount`, `Status`), chỉ cho phép các field metadata (`CustomerName`), kiểm tra trạng thái Entity (`Cancelled`/`Paid`), và kiểm tra quyền (`Audit.SuperReverse`).
    *   **Semantic Domain Command**: Việc khôi phục thực sự được thực thi bởi các hàm trong Domain (ví dụ `RestoreCustomerName()`), kết hợp với Concurrency Token (`RowVersion`) để đảm bảo Thread-safe.

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

## 🔁 7. Luồng Compensation cho Payment

Luồng rollback nghiệp vụ hiện tại:

1. Payment Service ghi nhận thanh toán thành công (`Status = Completed`) và publish `PaymentCompletedEvent` (có `PaymentId`).
2. Invoice Service consume event:
    * Nếu cập nhật được hóa đơn -> hoàn tất luồng.
    * Nếu thất bại nghiệp vụ (ví dụ invoice không tồn tại) -> publish `PaymentCompensationRequestedEvent`.
3. Payment Service consume `PaymentCompensationRequestedEvent` và cập nhật `Payment.Status = Reversed`.

**Lưu ý cho dev**:
* Đây là rollback mức nghiệp vụ (compensation), không phải rollback ACID transaction liên service.
* Consumer compensation phải idempotent: nếu payment đã `Reversed` thì bỏ qua.
* `PaymentId` trong event là khóa chính để xác định giao dịch cần bù trừ.

---
*Tài liệu này phục vụ mục đích hiểu sâu về tư duy thiết kế hệ thống.*
