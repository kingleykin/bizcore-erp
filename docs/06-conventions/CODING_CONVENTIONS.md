# 📋 CODING CONVENTIONS & STYLE GUIDE - BIZCORE ERP

> **Mục đích**: Cung cấp các quy tắc lập trình thống nhất cho toàn bộ dự án Bizcore ERP.
> Tài liệu này định nghĩa style code, naming conventions, architecture patterns, và best practices để đảm bảo tính nhất quán, dễ bảo trì, và chất lượng code cao.

---

## 📖 MỤC LỤC

1. [Giới thiệu chung](#1-giới-thiệu-chung)
2. [Quy tắc đặt tên (Naming Conventions)](#2-quy-tắc-đặt-tên)
3. [Cấu trúc mã nguồn (Project Organization)](#3-cấu-trúc-mã-nguồn)
4. [Clean Code & Architecture](#4-clean-code--architecture)
5. [Exception Handling](#5-exception-handling)
6. [Logging & Observability](#6-logging--observability)
7. [Asynchronous & Event-Driven](#7-asynchronous--event-driven)
8. [Security & Authorization](#8-security--authorization)
9. [Database & Entity Framework Core](#9-database--entity-framework-core)
10. [Testing Conventions](#10-testing-conventions)
11. [Code Review Checklist](#11-code-review-checklist)
12. [Frontend (React/TypeScript)](#12-frontend-reacttypescript)
13. [Localization & Error Governance](#13-localization--error-governance)

---

## 1. 🎯 Giới thiệu chung

### Đặt tính nhất quán lên hàng đầu

- Tất cả code phải tuân theo quy tắc trong tài liệu này
- Nếu nghi ngờ, hãy xem code cũ trong cùng service hoặc BuildingBlocks
- Điều chỉnh style cũ để phù hợp với convention mới khi có cơ hội refactor

### Công cụ hỗ trợ

- **EditorConfig**: `.editorconfig` định nghĩa indentation, line length, encoding
- **StyleCop**: `.editorconfig` chứa các rule StyleCop
- **SonarQube/SonarCloud** (nếu có): Kiểm tra quality gates
- **Code Reviews**: Bắt buộc review trước merge để kiểm tra convention

---

## 2. 🏷️ Quy tắc đặt tên (Naming Conventions)

### 2.1. PascalCase cho Public Members

**Quy tắc:**

- Classes, Interfaces, Methods, Public Properties: `PascalCase`
- Enums, Enum Values: `PascalCase`
- Constants: `SCREAMING_SNAKE_CASE` (hoặc `PascalCase` nếu const static readonly trong class)

**Ví dụ:**

```csharp
// ✅ ĐÚNG
public class InvoiceService { }
public interface IInvoiceService { }
public decimal TotalAmount { get; set; }
public void CreateInvoice() { }
public enum InvoiceStatus { Pending, Paid, Cancelled }

public const int MAX_INVOICE_AMOUNT = 1_000_000;
public const string DEFAULT_CURRENCY = "VND";

// ❌ SAI
public class invoice_service { }  // Không phải PascalCase
public interface InvoiceService { }  // Interface phải có I prefix
public decimal total_amount { get; set; }  // Không phải PascalCase
```

### 2.2. camelCase cho Private/Local Variables

**Quy tắc:**

- Private fields: `_camelCase` (với underscore prefix)
- Local variables: `camelCase` (không có underscore)
- Method parameters: `camelCase`

**Ví dụ:**

```csharp
public class InvoiceService
{
    private readonly AppDbContext _context;  // Private field
    private readonly ILogger _logger;         // Private field

    public async Task<Invoice> CreateAsync(string customerName, decimal amount)
    {
        var invoice = Invoice.Create(customerName, amount);  // Local variable
        await _context.Invoices.AddAsync(invoice);           // Local variable
        return invoice;
    }
}
```

### 2.3. Interface Naming

**Quy tắc:**

- Tất cả interfaces bắt đầu với `I` prefix
- Đặt tên theo hành động (Service, Handler, Repository, Client) hoặc chức năng

**Ví dụ:**

```csharp
public interface IInvoiceService { }
public interface IAuditServiceClient { }
public interface IReversalPolicy { }
public interface IEventPublisher { }
public interface IIdempotencyService { }
```

### 2.4. Event & Command Naming

**Quy tắc:**

- Events: `{EntityName}{Action}Event` (ví dụ: `InvoiceCreatedEvent`, `PaymentCompletedEvent`)
- Commands: `{Action}{EntityName}Command` (ví dụ: `CreateInvoiceCommand`, `ValidateInvoiceCommand`)
- Consumers: `{EventName}Consumer` (ví dụ: `PaymentCompletedConsumer`)

**Ví dụ:**

```csharp
// Events
public interface IInvoiceCreatedEvent { }
public interface IPaymentCompletedEvent { }
public interface IPaymentCompensationRequestedEvent { }

// Commands (trong contracts)
public record ValidateInvoiceCommand(Guid InvoiceId, string Reason);
public record ApplyPaymentToInvoiceRequest(Guid InvoiceId, Guid PaymentId);

// Consumers
public class InvoiceCreatedConsumer : IConsumer<IInvoiceCreatedEvent> { }
public class PaymentCompletedConsumer : IConsumer<IPaymentCompletedEvent> { }
```

### 2.5. File Naming

**Quy tắc:**

- File name = Class name (1 class public per file ngoài trường hợp nested classes)
- Folders theo tầng kiến trúc: `Domain/`, `Application/`, `Infrastructure/`, `Controllers/`, `DTOs/`

**Ví dụ:**

```
Invoice.API/
├── Domain/
│   ├── Entities/
│   │   ├── Invoice.cs              (class Invoice)
│   │   └── InvoiceLineItem.cs      (class InvoiceLineItem)
│   └── Enums/
│       └── InvoiceStatus.cs         (enum InvoiceStatus)
├── Application/
│   ├── Services/
│   │   └── InvoiceService.cs        (class InvoiceService + IInvoiceService)
│   ├── Policies/
│   │   └── InvoiceReversalPolicy.cs (class InvoiceReversalPolicy)
│   └── Consumers/
│       ├── PaymentCompletedConsumer.cs
│       └── ApplyPaymentToInvoiceConsumer.cs
├── Infrastructure/
│   ├── Data/
│   │   └── AppDbContext.cs
│   └── Clients/
│       └── AuditServiceClient.cs
└── Controllers/
    └── InvoicesController.cs
```

### 2.6. Database Column Naming

**Quy tắc:**

- Column names: `PascalCase` (match Property name)
- Foreign keys: `{EntityName}Id` (ví dụ: `InvoiceId`, `PaymentId`)
- Audit columns: `CreatedAt`, `UpdatedAt`, `DeletedAt`, `CreatedBy`, `UpdatedBy`
- Timestamps: `DateTime` UTC

**Ví dụ:**

```csharp
[Table("Invoices")]
public class Invoice
{
    [Key]
    public Guid Id { get; set; }

    public string CustomerName { get; set; }
    public decimal Amount { get; set; }
    public InvoiceStatus Status { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime CreatedAt { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }  // For concurrency control
}
```

---

## 3. 📁 Cấu trúc mã nguồn

### 3.1. DDD Lite - 4 Layer Architecture

Mỗi Microservice được chia thành 4 layer chính:

```
{ServiceName}.API/
├── Domain/                      # Layer 1: Pure business logic
│   ├── Entities/               # Aggregate roots, entities
│   ├── Enums/                  # Business enums
│   ├── Interfaces/             # Domain interfaces (không dependency ngoài)
│   ├── Exceptions/             # Domain exceptions (nếu service-specific)
│   └── ValueObjects/           # ValueObjects (immutable, no ID)
│
├── Application/                 # Layer 2: Use cases & orchestration
│   ├── Services/               # Domain services, command handlers
│   ├── Consumers/              # MassTransit event consumers
│   ├── Policies/               # Business policies (reversal, authorization)
│   ├── Clients/                # External service clients (HTTP, gRPC)
│   ├── BackgroundServices/     # Hosted background tasks
│   ├── DTOs/                   # Data Transfer Objects
│   └── Validators/             # FluentValidation validators
│
├── Infrastructure/              # Layer 3: Technical implementations
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   └── Migrations/
│   ├── Services/               # Implementation of domain services
│   ├── Repositories/           # Data access (nếu cần)
│   └── ExternalClients/        # External integrations
│
└── API/                         # Layer 4: HTTP endpoints
    ├── Controllers/            # REST controllers
    ├── Filters/                # Custom filters/middleware
    ├── Middleware/             # Middleware definitions
    ├── Program.cs              # Dependency injection, configuration
    └── appsettings.json
```

### 3.2. Layer Responsibilities

#### **Domain Layer** (Layer 1)

- ✅ Entities, Aggregates, ValueObjects
- ✅ Business rules & validations
- ✅ Domain Exceptions
- ✅ Domain Interfaces (abstractions)
- ❌ **KHÔNG** phụ thuộc vào framework (EF Core, ASP.NET, etc.)
- ❌ **KHÔNG** inject DbContext, Logger, HttpClient

#### **Application Layer** (Layer 2)

- ✅ Service classes & handlers
- ✅ Business logic orchestration
- ✅ MassTransit consumers
- ✅ Policies & authorization logic
- ✅ External client calls (HTTP, gRPC)
- ✅ DTOs & validation
- ❌ **KHÔNG** HTTP response logic
- ❌ **KHÔNG** dependency trực tiếp vào HTTP context (ngoài IHttpContextAccessor)

#### **Infrastructure Layer** (Layer 3)

- ✅ DbContext, EF Core configuration
- ✅ Migrations
- ✅ Repository implementations
- ✅ External service integrations
- ✅ Cache implementations
- ✅ MassTransit configuration
- ❌ **KHÔNG** business logic
- ❌ **KHÔNG** HTTP concerns

#### **API Layer** (Layer 4)

- ✅ Controllers & REST endpoints
- ✅ Request/Response mapping
- ✅ Authorization checks
- ✅ HTTP status codes
- ✅ Global exception handling (via middleware)
- ❌ **KHÔNG** business logic
- ❌ **KHÔNG** database queries trực tiếp

---

## 4. 🧠 Clean Code & Architecture

### 4.1. General Principles

#### 🚫 **KHÔNG** viết logic nghiệp vụ ở Controllers

**SAI:**

```csharp
[HttpPost]
public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceRequest req)
{
    // ❌ KHÔNG: Logic nghiệp vụ ở controller
    if (req.Amount > 1_000_000)
        return BadRequest("Exceeds limit");

    var invoice = new Invoice
    {
        CustomerName = req.CustomerName,
        Amount = req.Amount
    };
    await _context.Invoices.AddAsync(invoice);
    await _context.SaveChangesAsync();
    return Ok(invoice);
}
```

**ĐÚNG:**

```csharp
[HttpPost]
public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceRequest req)
{
    // ✅ Delegate tới service
    var invoice = Invoice.Create(req.CustomerName, req.Amount);  // Factory method
    var created = await _invoiceService.CreateAsync(invoice);
    return CreatedAtAction(nameof(GetInvoice), new { id = created.Id }, created);
}
```

#### 🚫 **KHÔNG** gọi HTTP trực tiếp giữa services

**SAI:**

```csharp
// ❌ KHÔNG: HTTP call giữa services
var paymentService = httpClient.GetAsync($"http://payment-service/api/payment/{id}");
```

**ĐÚNG:**

```csharp
// ✅ Dùng event-driven
// Invoice service publish: InvoiceCreatedEvent
// Payment service consume: InvoiceCreatedConsumer

public class InvoiceCreatedConsumer : IConsumer<IInvoiceCreatedEvent>
{
    public async Task Consume(ConsumeContext<IInvoiceCreatedEvent> context)
    {
        // Handle event
    }
}
```

#### ✅ Domain Layer phải "sạch" (Pure)

**SAI:**

```csharp
public class Invoice
{
    private readonly AppDbContext _context;  // ❌ KHÔNG: Framework dependency
    private readonly ILogger _logger;         // ❌ KHÔNG: Logger in Domain

    public void MarkAsPaid()
    {
        _context.Invoices.Update(this);      // ❌ KHÔNG: DB call
        _logger.LogInformation("...");       // ❌ KHÔNG: Logging in Domain
    }
}
```

**ĐÚNG:**

```csharp
public class Invoice
{
    // ✅ Pure domain logic, no dependencies
    public void MarkAsPaid()
    {
        if (Status != InvoiceStatus.Pending)
            throw new DomainException("Cannot mark non-pending invoice as paid");

        Status = InvoiceStatus.Paid;
    }
}

// ✅ Service layer handles DB and logging
public class InvoiceService
{
    public async Task<Invoice> MarkAsPaidAsync(Guid invoiceId)
    {
        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice is null) throw new NotFoundException("Invoice not found");

        invoice.MarkAsPaid();  // Call domain logic
        await _context.SaveChangesAsync();
        _logger.LogInformation("Invoice marked as paid: {InvoiceId}", invoiceId);
        return invoice;
    }
}
```

### 4.2. SOLID Principles

#### **S - Single Responsibility**

Mỗi class chỉ có 1 lý do để thay đổi.

**SAI:**

```csharp
public class InvoiceService
{
    // ❌ Quá nhiều trách nhiệm
    public async Task CreateInvoice() { }
    public async Task SendEmail() { }
    public async Task GenerateReport() { }
    public async Task UpdateAuditLog() { }
}
```

**ĐÚNG:**

```csharp
public class InvoiceService { }              // Chỉ tạo invoice
public class EmailService { }                // Chỉ gửi email
public class ReportService { }               // Chỉ tạo report
public class AuditEventConsumer { }          // Chỉ xử lý audit events
```

#### **O - Open/Closed**

Code phải mở để mở rộng, đóng để chỉnh sửa.

**ĐÚNG:**

```csharp
public interface IReversalPolicy
{
    RestoreDecision CanRestore(string field, Invoice invoice, ClaimsPrincipal user);
}

public class InvoiceReversalPolicy : IReversalPolicy
{
    public RestoreDecision CanRestore(string field, Invoice invoice, ClaimsPrincipal user)
    {
        // Logic cụ thể cho Invoice
    }
}

// Nếu cần policy mới, không sửa class cũ, tạo class mới
public class PaymentReversalPolicy : IReversalPolicy { }
```

#### **I - Interface Segregation**

Không ép implement interface quá lớn.

**SAI:**

```csharp
// ❌ Interface quá lớn
public interface IInvoiceService
{
    Task<Invoice> CreateAsync(Invoice invoice);
    Task<Invoice> UpdateAsync(Invoice invoice);
    Task<bool> DeleteAsync(Guid id);
    Task<IEnumerable<Invoice>> GetAllAsync();
    Task<Invoice> GetByIdAsync(Guid id);
    Task<IEnumerable<Invoice>> SearchAsync(string query);
    Task<ReportDto> GenerateReportAsync();
    Task SendEmailAsync(Invoice invoice);
}
```

**ĐÚNG:**

```csharp
// ✅ Tách thành interface nhỏ hơn
public interface IInvoiceService
{
    Task<Invoice> CreateAsync(Invoice invoice);
    Task<Invoice> GetByIdAsync(Guid id);
}

public interface IReportService
{
    Task<ReportDto> GenerateReportAsync();
}

public interface IEmailService
{
    Task SendEmailAsync(Invoice invoice);
}
```

#### **D - Dependency Inversion**

Depend on abstractions, không concretions.

**ĐÚNG:**

```csharp
public class InvoiceService
{
    // ✅ Depend on interfaces
    private readonly IInvoiceRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<InvoiceService> _logger;

    public InvoiceService(
        IInvoiceRepository repository,
        IPublishEndpoint publishEndpoint,
        ILogger<InvoiceService> logger)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }
}
```

### 4.3. DRY - Don't Repeat Yourself

- Tạo reusable utilities trong `BuildingBlocks` cho logic dùng chung
- Sử dụng extension methods cho thao tác lặp lại

**Ví dụ:**

```csharp
// ✅ Shared in BuildingBlocks
public static class DateTimeExtensions
{
    public static bool IsUtc(this DateTime dt)
        => dt.Kind == DateTimeKind.Utc;
}

// ✅ Use everywhere
var isUtc = DateTime.UtcNow.IsUtc();
```

---

## 5. ⚠️ Exception Handling

### 5.1. Exception Types

**Defined in `Bizcore.BuildingBlocks.Exceptions`:**

```csharp
// ✅ Business rule violation
public class DomainException : Exception { }

// ✅ Resource not found
public class NotFoundException : Exception
{
    public string ErrorCode { get; }
    public object Parameters { get; }
    // ... constructors supporting code and params
}

// ✅ Access denied
public class UnauthorizedException : Exception { }

// ✅ Validation failed
public class ValidationException : Exception { }
```

### 5.2. Exception Throwing Conventions

#### **Throw từ Domain Layer** (Business logic)

```csharp
public class Invoice
{
    public static Invoice Create(string customerName, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(customerName))
            throw new DomainException("Customer name cannot be empty");

        if (amount <= 0)
            throw new DomainException("Amount must be greater than 0");

        if (amount > 1_000_000)
            throw new DomainException("Amount exceeds maximum limit");

        return new Invoice
        {
            Id = Guid.NewGuid(),
            CustomerName = customerName,
            Amount = amount
        };
    }
}
```

#### **Throw từ Application Layer** (Service)

```csharp
public class InvoiceService
{
    public async Task<Invoice> GetByIdAsync(Guid id)
    {
        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == id);

        if (invoice is null)
            throw new NotFoundException(ErrorCodes.Invoice.NotFound, "Invoice not found", new { id });

        return invoice;
    }
}
```

#### **Throw từ API Layer** (Controller)

```csharp
[HttpPost("{id}/restore-field")]
[Authorize(Policy = "Audit.View")]
public async Task<IActionResult> RestoreField(Guid id, [FromBody] RestoreFieldRequest request)
{
    if (string.IsNullOrWhiteSpace(request.Reason))
        throw new DomainException("Reason is required");

    // ...
}
```

### 5.3. Global Exception Handler

**Middleware xử lý tất cả exceptions và chuẩn hóa response:**

```csharp
// ✅ Defined in BuildingBlocks
public class GlobalExceptionMiddleware
{
    public async Task InvokeAsync(HttpContext context, ILogger<GlobalExceptionMiddleware> logger)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            logger.LogWarning("Domain exception: {Message}", ex.Message);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning("Not found: {Message}", ex.Message);
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
        // ... other exception types
    }
}

// ✅ Register in Program.cs
app.UseMiddleware<GlobalExceptionMiddleware>();
```

### 5.4. Exception vs Return Value

**Rule of thumb:**

- **Throw exception** khi: Invalid state, precondition violated, unrecoverable error
- **Return Result/Optional** khi: Expected failure, validation failed, not found

**Ví dụ:**

```csharp
// ✅ Throw khi precondition violated
public void MarkAsPaid()
{
    if (Status == InvoiceStatus.Cancelled)
        throw new DomainException("Cannot mark cancelled invoice as paid");

    Status = InvoiceStatus.Paid;
}

// ✅ Return Result khi expected failure
public record RestoreFieldResult(bool Success, string Message, Guid? NewAuditEntryId = null);

public async Task<RestoreFieldResult> RestoreFieldAsync(...)
{
    if (invoice is null)
        return new RestoreFieldResult(false, "Invoice not found");

    // ...
    return new RestoreFieldResult(true, "Field restored successfully", auditEntryId);
}
```

---

## 6. 📊 Logging & Observability

### 6.1. Logging Best Practices

#### **Structured Logging với Serilog**

```csharp
using Serilog;

// ✅ ĐÚNG: Structured logging
_logger.LogInformation("Invoice created: {InvoiceId}, {CustomerName}, {Amount}",
    invoice.Id, invoice.CustomerName, invoice.Amount);

// ❌ SAI: String interpolation
_logger.LogInformation($"Invoice created: {invoice.Id}, {invoice.CustomerName}");

// ❌ SAI: Concatenation
_logger.LogInformation("Invoice created: " + invoice.Id);
```

#### **Log Levels**

```csharp
// 📌 Debug: Development troubleshooting
_logger.LogDebug("Checking payment status: {PaymentId}", paymentId);

// ℹ️ Information: Normal business flow
_logger.LogInformation("Invoice created: {InvoiceId}", invoiceId);

// ⚠️ Warning: Unexpected condition, recoverable
_logger.LogWarning("Invoice not found: {InvoiceId}", invoiceId);

// ❌ Error: Exception, recoverable error
_logger.LogError(ex, "Failed to save invoice: {InvoiceId}", invoiceId);

// 🚨 Critical: System is unstable
_logger.LogCritical(ex, "Database connection failed");
```

#### **Correlation ID Tracking**

```csharp
// ✅ All logs include Correlation ID
_logger.LogInformation(
    "Processing payment X-Correlation-ID={CorrelationId}, PaymentId={PaymentId}",
    context.CorrelationId,
    paymentId);
```

### 6.2. What to Log

✅ **Log these:**

- Request start/end with duration
- Business state changes (Invoice created, Payment completed, etc.)
- Authorization decisions (Success, Denied, etc.)
- Important milestones (Event published, Consumer processed, etc.)
- Errors and exceptions with stack trace

❌ **DON'T log:**

- Sensitive data (passwords, API keys, SSN, credit cards)
- PII (Personal Identifiable Information) unless absolutely necessary
- Extremely verbose debug info in production

### 6.3. Sensitive Data Masking

```csharp
// ✅ Use SensitiveFieldMasker from BuildingBlocks
public class SensitiveFieldMasker
{
    private static readonly HashSet<string> SensitiveFields = new()
    {
        "password", "token", "creditcard", "ssn", "apikey"
    };

    public static string MaskIfNeeded(string fieldName, object? value)
    {
        if (SensitiveFields.Contains(fieldName.ToLower()))
            return "***MASKED***";

        return value?.ToString() ?? "null";
    }
}
```

---

## 7. 🔄 Asynchronous & Event-Driven

### 7.1. Async/Await Conventions

#### **Always use async for I/O operations**

```csharp
// ✅ ĐÚNG
public async Task<Invoice> GetByIdAsync(Guid id)
    => await _context.Invoices.FirstOrDefaultAsync(i => i.Id == id);

// ❌ SAI: Blocking call
public Invoice GetById(Guid id)
    => _context.Invoices.FirstOrDefault(i => i.Id == id);
```

#### **Method naming: Async suffix**

```csharp
// ✅ ĐÚNG
public async Task<Invoice> CreateAsync(Invoice invoice) { }
public async Task<bool> DeleteAsync(Guid id) { }
public async Task<IEnumerable<Invoice>> GetAllAsync() { }

// ❌ SAI: Missing Async suffix
public async Task<Invoice> Create(Invoice invoice) { }
```

#### **ConfigureAwait(false) for libraries**

```csharp
// ✅ ĐÚNG: Library code should not capture context
await _context.Invoices.AddAsync(invoice).ConfigureAwait(false);
await _context.SaveChangesAsync().ConfigureAwait(false);

// Acceptable: ASP.NET Core automatically uses ConfigureAwait(false)
public async Task<IActionResult> GetInvoice(Guid id)
{
    var invoice = await _invoiceService.GetByIdAsync(id);  // OK in controller
    return Ok(invoice);
}
```

### 7.2. Event-Driven Architecture

#### **Publishing Events**

```csharp
// ✅ ĐÚNG: Publish via IPublishEndpoint (MassTransit)
public class InvoiceService
{
    private readonly IPublishEndpoint _publishEndpoint;

    public async Task<Invoice> CreateAsync(Invoice invoice)
    {
        await _context.Invoices.AddAsync(invoice);
        await _context.SaveChangesAsync();

        // ✅ Publish event after successful save
        await _publishEndpoint.Publish<IInvoiceCreatedEvent>(new
        {
            Id = invoice.Id,
            CustomerName = invoice.CustomerName,
            Amount = invoice.Amount,
            CreatedAt = invoice.CreatedAt
        });

        return invoice;
    }
}
```

#### **Consuming Events**

```csharp
// ✅ ĐÚNG: MassTransit Consumer
public class PaymentCompletedConsumer : IConsumer<IPaymentCompletedEvent>
{
    private readonly IInvoiceService _invoiceService;

    public PaymentCompletedConsumer(IInvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
    }

    public async Task Consume(ConsumeContext<IPaymentCompletedEvent> context)
    {
        var @event = context.Message;
        await _invoiceService.MarkAsPaidAsync(@event.InvoiceId);
    }
}

// ✅ Register in Program.cs
services.AddMassTransit(x =>
{
    x.AddConsumer<PaymentCompletedConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});
```

### 7.3. Idempotency in Consumers

```csharp
// ✅ ĐÚNG: Check idempotency before processing
public class PaymentCompletedConsumer : IConsumer<IPaymentCompletedEvent>
{
    private readonly AppDbContext _context;

    public async Task Consume(ConsumeContext<IPaymentCompletedEvent> context)
    {
        var idempotencyKey = context.Message.PaymentId;

        // Check if already processed
        var existing = await _context.ProcessedEvents
            .FirstOrDefaultAsync(pe => pe.EventId == idempotencyKey);

        if (existing is not null)
            return;  // Already processed

        // Process event
        // ...

        // Record as processed
        await _context.ProcessedEvents.AddAsync(
            new ProcessedEvent { EventId = idempotencyKey });
        await _context.SaveChangesAsync();
    }
}
```

---

## 8. 🔐 Security & Authorization

### 8.1. Authorization Attributes

#### **Always require explicit authorization**

```csharp
// ✅ ĐÚNG: Explicit authorization policy
[ApiController]
[Route("api/v{version:apiVersion}/invoice")]
[Authorize(Policy = "Invoice.View")]
public class InvoicesController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetInvoices() { }

    [HttpPost]
    [Authorize(Policy = "Invoice.Create")]
    public async Task<IActionResult> CreateInvoice() { }

    [HttpPut("{id}")]
    [Authorize(Policy = "Invoice.Update")]
    public async Task<IActionResult> UpdateInvoice(Guid id) { }
}

// ❌ SAI: No authorization
public class InvoicesController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetInvoices() { }  // ❌ Không kiểm tra quyền
}

// ❌ SAI: Generic authorization
[Authorize]
public async Task<IActionResult> UpdateInvoice(Guid id) { }  // ❌ Chỉ check authenticated
```

### 8.2. Permission Definitions

**Centralized in `Bizcore.BuildingBlocks.Permissions`:**

```csharp
public static class Permissions
{
    public static class Invoice
    {
        public const string View   = "Invoice.View";
        public const string Create = "Invoice.Create";
        public const string Update = "Invoice.Update";
        public const string Delete = "Invoice.Delete";
    }

    public static class Payment
    {
        public const string View   = "Payment.View";
        public const string Create = "Payment.Create";
    }

    public static class Audit
    {
        public const string View      = "Audit.View";
        public const string AdminMode = "Audit.AdminMode";
    }
}

// ✅ Usage in controllers
[Authorize(Policy = Permissions.Invoice.Create)]
```

### 8.3. Sensitive Data Protection

#### **Mask sensitive data in responses**

```csharp
// ✅ ĐÚNG: Mask PII in DTOs
public class AuditLogDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; }

    [JsonIgnore]  // Exclude from response unless explicitly needed
    public string? BeforeJson { get; set; }

    public string? BeforeJsonMasked { get; set; }  // Sensitive fields masked
}

// ✅ Mask during serialization
public static string MaskSensitiveJson(string json)
{
    var masked = json
        .Replace("\"password\":", "\"password\":\"***\"")
        .Replace("\"creditCard\":", "\"creditCard\":\"***\"");
    return masked;
}
```

---

## 9. 🗄️ Database & Entity Framework Core

### 9.1. Entity Configuration

#### **Fluent API over Data Annotations (when possible)**

```csharp
// ✅ ĐÚNG: Use Fluent API in OnModelCreating
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Invoice>(entity =>
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.CustomerName)
            .IsRequired()
            .HasMaxLength(255);

        entity.Property(e => e.Amount)
            .HasColumnType("decimal(18,2)");

        entity.Property(e => e.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        entity.Property(e => e.RowVersion)
            .IsRowVersion();
    });
}

// Acceptable: Simple cases can use data annotations
public class Invoice
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string CustomerName { get; set; }
}
```

### 9.2. Query Performance

#### **Use AsNoTracking() for read-only queries**

```csharp
// ✅ ĐÚNG: AsNoTracking for read-only
public async Task<IEnumerable<Invoice>> GetAllAsync()
    => await _context.Invoices
        .AsNoTracking()
        .ToListAsync();

// ✅ ĐÚNG: Tracking for update
public async Task<Invoice?> GetByIdForUpdateAsync(Guid id)
    => await _context.Invoices
        .FirstOrDefaultAsync(i => i.Id == id);
```

#### **Select only needed columns**

```csharp
// ✅ ĐÚNG: Select specific columns
public async Task<IEnumerable<InvoiceSummaryDto>> GetSummaryAsync()
    => await _context.Invoices
        .AsNoTracking()
        .Select(i => new InvoiceSummaryDto
        {
            Id = i.Id,
            CustomerName = i.CustomerName,
            Amount = i.Amount
        })
        .ToListAsync();

// ❌ SAI: Select entire entity then map
var invoices = await _context.Invoices.ToListAsync();
var dtos = invoices.Select(i => new InvoiceSummaryDto { ... }).ToList();
```

### 9.3. Transactions & Concurrency

#### **Local Transaction Pattern**

```csharp
// ✅ ĐÚNG: Multiple writes within transaction
public async Task<(Invoice, Payment)> CreateInvoiceAndPaymentAsync(
    Invoice invoice, Payment payment)
{
    using (var transaction = await _context.Database.BeginTransactionAsync())
    {
        try
        {
            await _context.Invoices.AddAsync(invoice);
            await _context.SaveChangesAsync();

            await _context.Payments.AddAsync(payment);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return (invoice, payment);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
```

#### **Outbox Pattern (recommended)**

```csharp
// ✅ ĐÚNG: Use MassTransit Outbox for atomic DB + Event publish
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.AddInboxStateEntity();
    modelBuilder.AddOutboxMessageEntity();
    modelBuilder.AddOutboxStateEntity();
}

public async Task<Invoice> CreateAsync(Invoice invoice)
{
    await _context.Invoices.AddAsync(invoice);

    // Event will be published atomically with SaveChanges
    await _publishEndpoint.Publish<IInvoiceCreatedEvent>(new { ... });

    await _context.SaveChangesAsync();  // Outbox ensures atomic publish
}
```

#### **Concurrency Control - RowVersion**

```csharp
// ✅ ĐÚNG: Check RowVersion to detect concurrent modifications
public async Task<Invoice> MarkAsPaidAsync(Guid id, byte[]? expectedRowVersion)
{
    var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == id);
    if (invoice is null)
        throw new NotFoundException("Invoice not found");

    // Check if changed by another process
    if (expectedRowVersion != null && !invoice.RowVersion.SequenceEqual(expectedRowVersion))
        throw new DomainException("Invoice was modified by another user");

    invoice.MarkAsPaid();
    await _context.SaveChangesAsync();

    return invoice;
}
```

### 9.4. Migrations

#### **Naming convention: Timestamp_Description**

```bash
Add-Migration 20240507132500_AddInvoiceTable
Add-Migration 20240507134200_AddRowVersionToInvoice
Add-Migration 20240508091000_AddAuditColumns
```

---

## 10. 🧪 Testing Conventions

### 10.1. Test Project Structure

```
Tests/
├── Bizcore.UnitTests/
│   ├── Services/
│   │   ├── InvoiceServiceTests.cs
│   │   └── PaymentServiceTests.cs
│   ├── Consumers/
│   │   ├── PaymentCompletedConsumerTests.cs
│   │   └── InvoiceCreatedConsumerTests.cs
│   ├── Domain/
│   │   └── InvoiceTests.cs
│   └── Fixtures/
│       ├── TestDbContextFactory.cs
│       └── MockDataGenerator.cs
│
└── Bizcore.IntegrationTests/
    └── (Integration tests for end-to-end flows)
```

### 10.2. Unit Test Naming

**Pattern: `{MethodName}_{Scenario}_{ExpectedResult}`**

```csharp
public class InvoiceServiceTests
{
    [Fact]
    public async Task CreateAsync_WithValidData_ReturnsInvoice()
    {
        // Arrange
        var service = new InvoiceService(mockContext, mockPublisher);
        var invoice = Invoice.Create("John", 1000);

        // Act
        var result = await service.CreateAsync(invoice);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(invoice.Id, result.Id);
    }

    [Fact]
    public async Task CreateAsync_WithExcessiveAmount_ThrowsDomainException()
    {
        // Arrange
        var invoice = Invoice.Create("John", 2_000_000);  // Exceeds limit

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => service.CreateAsync(invoice));
    }
}
```

### 10.3. Test Best Practices

#### **Use xUnit + Moq + FluentAssertions**

```csharp
using Xunit;
using Moq;
using FluentAssertions;

public class PaymentServiceTests
{
    private readonly Mock<IPaymentRepository> _mockRepository;
    private readonly Mock<IPublishEndpoint> _mockPublisher;
    private readonly PaymentService _service;

    public PaymentServiceTests()
    {
        _mockRepository = new Mock<IPaymentRepository>();
        _mockPublisher = new Mock<IPublishEndpoint>();
        _service = new PaymentService(_mockRepository.Object, _mockPublisher.Object);
    }

    [Fact]
    public async Task CompletePaymentAsync_PublishesEvent()
    {
        // Arrange
        var payment = new Payment { Id = Guid.NewGuid(), Amount = 1000 };
        _mockRepository.Setup(r => r.GetByIdAsync(payment.Id))
            .ReturnsAsync(payment);

        // Act
        await _service.CompletePaymentAsync(payment.Id);

        // Assert
        _mockPublisher.Verify(
            p => p.Publish(It.IsAny<IPaymentCompletedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
```

#### **Avoid test interdependencies**

```csharp
// ❌ SAI: Tests depend on each other
[Fact]
public async Task Test1_CreateInvoice() { /* ... */ }

[Fact]
public async Task Test2_UpdateInvoice()  // Depends on Test1
{
    // Assumes invoice created in Test1
}

// ✅ ĐÚNG: Each test is independent
[Fact]
public async Task CreateInvoice_WithValidData_Succeeds() { /* ... */ }

[Fact]
public async Task UpdateInvoice_WithValidData_Succeeds()
{
    // Create its own test data
    var invoice = new Invoice { /* ... */ };
    // ...
}
```

### 10.4. Test Reporting & Automation

**Quy tắc:**

- Tất cả Integration Tests phải có khả năng chạy tự động trên CI/CD.
- Kết quả test **BẮT BUỘC** phải được xuất ra định dạng HTML và XML (Junit).
- Sử dụng script `run-tests.ps1` (Windows) hoặc `run-tests.sh` (macOS/Linux) tại thư mục gốc để thực thi và tạo báo cáo đồng nhất.

**Cách chạy và xem report:**

```powershell
# Cho Windows (PowerShell)
./run-tests.ps1

# Cho macOS/Linux (Bash)
chmod +x run-tests.sh
./run-tests.sh

# Xem kết quả:
# - Report HTML (Human-readable): TestResults/test-report.html
# - Report XML (CI/CD integration): TestResults/test-report.xml
# - Coverage (Độ bao phủ code): TestResults/coverage.xml
```

**Yêu cầu chi tiết đối với Developer:**

#### 1. Quy tắc đặt tên (Naming Convention)

Tuân thủ nghiêm ngặt pattern `{Method}_{Scenario}_{Expected}`.

- **Method**: Tên phương thức hoặc chức năng đang được test.
- **Scenario**: Điều kiện đầu vào, trạng thái hệ thống hoặc hành vi người dùng.
- **Expected**: Kết quả kỳ vọng hoặc hành vi mong đợi.

_Ví dụ:_

```csharp
// ✅ ĐÚNG
[Fact]
public async Task Login_WithInvalidCredentials_ShouldReturnUnauthorized() { ... }

[Fact]
public async Task CreateInvoice_WhenAmountExceedsLimit_ShouldThrowDomainException() { ... }

[Fact]
public async Task GetById_WhenInvoiceDoesNotExist_ShouldReturnNotFound() { ... }
```

#### 2. Tính độc lập của Test (Test Independence)

Mỗi test case phải là một đơn vị độc lập hoàn toàn. Không được viết các test case phụ thuộc vào kết quả hoặc dữ liệu của nhau.

_Ví dụ:_

```csharp
// ❌ SAI: Test Update phụ thuộc vào việc Test Create đã chạy trước đó
[Fact] public async Task Step1_CreateUser() { ... }
[Fact] public async Task Step2_UpdateUser() { ... } // Giả định ID 1 đã tồn tại

// ✅ ĐÚNG: Mỗi test tự chuẩn bị dữ liệu cho chính mình
[Fact]
public async Task UpdateUser_WhenUserExists_ShouldUpdateSuccessfully()
{
    // Arrange: Tự tạo user mới phục vụ riêng cho test này
    var user = await CreateTestUserAsync();

    // Act & Assert...
}
```

#### 3. Sử dụng FluentAssertions

Ưu tiên sử dụng thư viện `FluentAssertions` để các câu lệnh kiểm tra (Assert) trở nên tự nhiên, dễ đọc như ngôn ngữ nói và cung cấp thông tin lỗi chi tiết khi test fail.

_Ví dụ:_

```csharp
// ✅ ĐÚNG (Fluent style)
result.StatusCode.Should().Be(StatusCodes.Status200OK);
items.Should().NotBeEmpty().And.HaveCount(3);
user.Email.Should().Match("*@gmail.com");

// ❌ HẠN CHẾ (Classic style)
Assert.Equal(200, result.StatusCode);
Assert.True(items.Count == 3);
```

#### 4. Dọn dẹp môi trường (Database Clean-up)

Để tránh việc dữ liệu "rác" từ lần chạy trước ảnh hưởng đến lần chạy sau, bắt buộc sử dụng `Respawn` trong `ApiTestBase` để reset database về trạng thái ban đầu trước mỗi test case.

_Ví dụ trong Base Class:_

```csharp
public async Task InitializeAsync()
{
    // Reset DB về trạng thái sạch trước khi chạy Act
    await _respawner.ResetAsync(_connectionString);
}
```

---

## 11. ✅ Code Review Checklist

Trước khi merge pull request, xác nhận:

### Architecture & Design

- [ ] Code tuân thủ 4-layer DDD architecture
- [ ] Không có business logic ở controllers
- [ ] Domain layer không phụ thuộc framework
- [ ] Events được sử dụng cho inter-service communication
- [ ] SOLID principles được áp dụng

### Naming Conventions

- [ ] Classes, methods: PascalCase
- [ ] Private fields: `_camelCase`
- [ ] Local variables, parameters: `camelCase`
- [ ] Interfaces: `I{Name}`
- [ ] Events: `{Entity}{Action}Event`
- [ ] File names match class names

### Exception Handling

- [ ] Domain exceptions thrown for business violations
- [ ] Meaningful error messages
- [ ] No generic `catch (Exception ex)`
- [ ] Global exception middleware handles all exceptions
- [ ] Logging includes proper context

### Database & EF Core

- [ ] Async/await used for all I/O
- [ ] Method names have `Async` suffix
- [ ] AsNoTracking() used for read-only queries
- [ ] Transactions used for multi-entity operations
- [ ] RowVersion used for concurrency control
- [ ] No N+1 queries

### Security

- [ ] Authorization policies explicitly defined
- [ ] Sensitive data masked in logs
- [ ] No hardcoded credentials
- [ ] Input validation via FluentValidation
- [ ] Output encoded to prevent injection

### Performance

- [ ] Async/await properly used
- [ ] Queries are optimized
- [ ] No unnecessary database calls
- [ ] Proper caching strategies
- [ ] Logging is not excessive

### Testing

- [ ] Unit tests for domain logic
- [ ] Tests are independent
- [ ] Proper use of mocks/stubs
- [ ] Test names describe scenario
- [ ] Edge cases covered

### Documentation

- [ ] Complex logic has comments
- [ ] Public methods have XML docs
- [ ] Architecture decisions documented
- [ ] Breaking changes noted in PR

---

## 12. 💻 Frontend (React/TypeScript)

### 12.1. Component Structure

```typescript
// components/Invoice/InvoiceList.tsx
import React, { useEffect, useState } from 'react';
import { Invoice } from '@/types/invoice';
import { InvoiceService } from '@/services/invoiceService';
import { InvoiceCard } from './InvoiceCard';

export const InvoiceList: React.FC = () => {
    const [invoices, setInvoices] = useState<Invoice[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        loadInvoices();
    }, []);

    const loadInvoices = async () => {
        try {
            setLoading(true);
            setError(null);
            const data = await InvoiceService.getAll();
            setInvoices(data);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Failed to load invoices');
        } finally {
            setLoading(false);
        }
    };

    if (loading) return <div>Loading...</div>;
    if (error) return <div className="error">{error}</div>;

    return (
        <div className="invoice-list">
            {invoices.map((invoice) => (
                <InvoiceCard key={invoice.id} invoice={invoice} />
            ))}
        </div>
    );
};
```

### 12.2. Naming Conventions (Frontend)

- **Components**: PascalCase (e.g., `InvoiceList.tsx`, `PaymentForm.tsx`)
- **Functions/Variables**: camelCase (e.g., `handleSubmit`, `totalAmount`)
- **Constants**: SCREAMING_SNAKE_CASE (e.g., `API_BASE_URL`, `MAX_RETRY_ATTEMPTS`)
- **Interfaces**: PascalCase with `I` prefix (e.g., `IInvoice`, `IPayment`)

### 12.3. API Client Pattern

```typescript
// services/invoiceService.ts
import { API_BASE_URL } from "@/config/constants";

export interface Invoice {
    id: string;
    customerName: string;
    amount: number;
    status: "pending" | "paid" | "cancelled";
    createdAt: string;
}

export class InvoiceService {
    static async getAll(): Promise<Invoice[]> {
        const response = await fetch(`${API_BASE_URL}/invoice`, {
            headers: this.getHeaders(),
        });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        return response.json();
    }

    static async getById(id: string): Promise<Invoice> {
        const response = await fetch(`${API_BASE_URL}/invoice/${id}`, {
            headers: this.getHeaders(),
        });
        if (!response.ok) throw new Error(`Invoice not found`);
        return response.json();
    }

    static async create(
        invoice: Omit<Invoice, "id" | "createdAt">,
    ): Promise<Invoice> {
        const response = await fetch(`${API_BASE_URL}/invoice`, {
            method: "POST",
            headers: this.getHeaders(),
            body: JSON.stringify(invoice),
        });
        if (!response.ok) throw new Error(`Failed to create invoice`);
        return response.json();
    }

    private static getHeaders(): HeadersInit {
        return {
            "Content-Type": "application/json",
            Authorization: `Bearer ${this.getAuthToken()}`,
        };
    }

    private static getAuthToken(): string {
        return localStorage.getItem("authToken") || "";
    }
}
```

### 12.4. Error Handling (Frontend)

```typescript
// utils/errorHandler.ts
export class ApiError extends Error {
    constructor(
        public statusCode: number,
        message: string,
        public details?: Record<string, unknown>,
    ) {
        super(message);
        this.name = "ApiError";
    }
}

export async function handleApiResponse<T>(response: Response): Promise<T> {
    if (!response.ok) {
        const error = await response
            .json()
            .catch(() => ({ error: "Unknown error" }));
        throw new ApiError(
            response.status,
            error.error || response.statusText,
            error,
        );
    }
    return response.json();
}

// Usage in component
try {
    const invoice = await InvoiceService.getById(id);
} catch (error) {
    if (error instanceof ApiError) {
        console.error(`Error ${error.statusCode}: ${error.message}`);
    }
}
```

### 12.5. Component Best Practices

```typescript
// ✅ ĐÚNG: Functional component with hooks
export const InvoiceForm: React.FC<{ onSubmit: (invoice: Invoice) => void }> = ({
    onSubmit,
}) => {
    const [formData, setFormData] = useState({ customerName: '', amount: 0 });
    const [errors, setErrors] = useState<Record<string, string>>({});

    const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        const { name, value } = e.target;
        setFormData((prev) => ({ ...prev, [name]: value }));
    };

    const handleSubmit = (e: React.FormEvent) => {
        e.preventDefault();
        // Validation...
        onSubmit(formData as Invoice);
    };

    return (
        <form onSubmit={handleSubmit}>
            <input name="customerName" onChange={handleChange} />
            {errors.customerName && <span className="error">{errors.customerName}</span>}
            <button type="submit">Submit</button>
        </form>
    );
};

// ❌ SAI: No proptypes or typing
export function InvoiceForm(props) {
    // ...
}
```

---

## 📝 Summary

| Aspek             | Quy tắc                    |
| ----------------- | -------------------------- |
| **Class/Method**  | PascalCase                 |
| **Variables**     | camelCase                  |
| **Constants**     | SCREAMING_SNAKE_CASE       |
| **Interfaces**    | I{Name}                    |
| **Events**        | {Entity}{Action}Event      |
| **Files**         | Match class name           |
| **Layers**        | Domain → App → Infra → API |
| **Async**         | Always use Async suffix    |
| **Exceptions**    | Domain-specific types      |
| **Transactions**  | Outbox Pattern preferred   |
| **Communication** | Events via MassTransit     |
| **Authorization** | Explicit policies required |
| **Logging**       | Structured with SeriLog    |

---

## 📚 Related Documents

- [PROJECT_INDEX.md](../02-project-overview/PROJECT_INDEX.md) - Project overview
- [PROJECT_STRUCTURE.md](../02-project-overview/PROJECT_STRUCTURE.md) - Detailed structure
- [TRANSACTION_MANAGEMENT_DESIGN.md](../05-transactions/TRANSACTION_MANAGEMENT_DESIGN.md) - Transaction patterns
- [IDEMPOTENCY_DESIGN.md](../03-architecture/IDEMPOTENCY_DESIGN.md) - Idempotency patterns

---

**Last Updated**: 2026-05-12  
**Version**: 1.2 (Added Cross-platform Test Scripts)  
**Maintained by**: Architecture Team

---

## 13. 🌍 Localization & Error Governance

Hệ thống ERP yêu cầu sự nhất quán về thông điệp lỗi và khả năng đa ngôn ngữ toàn diện.

### 13.1. Nguyên tắc Backend

- **KHÔNG** trả về các chuỗi ký tự đã được dịch (localized strings) từ Backend.
- **BẮT BUỘC** sử dụng **ErrorCode** từ `Bizcore.BuildingBlocks.ErrorCodes`.
- **Cung cấp tham số**: Nếu mã lỗi cần thông tin động (ví dụ: tên user, ID hóa đơn), hãy truyền qua đối tượng `Parameters`.

### 13.2. Cấu trúc ErrorCodes

Mã lỗi được tổ chức theo phân cấp: `{MODULE}.{ENTITY}.{REASON}`

```csharp
public static class ErrorCodes
{
    public static class Invoice
    {
        public const string NotFound = "INVOICE.NOT_FOUND";
        public const string AlreadyPaid = "INVOICE.ALREADY_PAID";
    }
}
```

### 13.3. Culture Propagation (Lan truyền ngôn ngữ)

- Ngôn ngữ được tự động truyền qua HTTP Header `Accept-Language`.
- Đối với MassTransit, hệ thống tự động đính kèm `X-Culture` vào Message Header.
- **Quy tắc**: Mọi tác vụ background (gửi email, tạo báo cáo) phải sử dụng `CultureInfo.CurrentUICulture` để lấy đúng ngôn ngữ của người dùng đã kích hoạt tác vụ đó.

### 13.4. Nguyên tắc Frontend

- Toàn bộ bản dịch được lưu tại `public/locales/{lng}/{namespace}.json`.
- Sử dụng mã lỗi nhận được từ API để tìm bản dịch tương ứng trong `errors.json`.

```javascript
// Ví dụ dịch lỗi
const message = t(`errors:${errorCode}`, parameters);
```

