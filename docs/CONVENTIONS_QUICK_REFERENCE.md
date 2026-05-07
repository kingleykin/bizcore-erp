# ⚡ THAM KHẢO NHANH - CODING CONVENTIONS

> **Dành cho các lập trình viên muốn hiểu nhanh những điều cần thiết**

## 🎯 5 Quy Tắc Quan Trọng Nhất (Bắt Buộc Tuân Thủ)

### 1. **KHÔNG ĐƯỢC Đặt Logic Nghiệp Vụ trong Controllers**
```csharp
// ❌ WRONG
[HttpPost]
public async Task<IActionResult> CreateInvoice(CreateInvoiceRequest req)
{
    if (req.Amount > 1_000_000) return BadRequest("Exceeds limit");
    var invoice = new Invoice { Amount = req.Amount };
    await _context.SaveChangesAsync();
}

// ✅ RIGHT
[HttpPost]
public async Task<IActionResult> CreateInvoice(CreateInvoiceRequest req)
{
    var invoice = Invoice.Create(req.CustomerName, req.Amount);
    await _invoiceService.CreateAsync(invoice);
    return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id }, invoice);
}
```

### 2. **ALWAYS Add Authorization Attributes**
```csharp
// ❌ WRONG - Missing authorization
[HttpPost]
public async Task<IActionResult> CreateInvoice(CreateInvoiceRequest req) { }

// ✅ RIGHT - Explicit policy
[HttpPost]
[Authorize(Policy = Permissions.Invoice.Create)]
public async Task<IActionResult> CreateInvoice(CreateInvoiceRequest req) { }
```

### 3. **Use Events for Service Communication**
```csharp
// ❌ WRONG - Direct HTTP call between services
var payment = await _httpClient.GetAsync($"http://payment-service/api/{id}");

// ✅ RIGHT - Use Events
public class PaymentCompletedConsumer : IConsumer<IPaymentCompletedEvent>
{
    public async Task Consume(ConsumeContext<IPaymentCompletedEvent> context)
    {
        // Handle event
    }
}
```

### 4. **Throw Domain Exceptions, Not Return Codes**
```csharp
// ❌ WRONG
public void MarkAsPaid()
{
    if (Status != Pending) return false;
}

// ✅ RIGHT
public void MarkAsPaid()
{
    if (Status != Pending)
        throw new DomainException("Cannot mark non-pending invoice as paid");
}
```

### 5. **Use Async/Await Everywhere**
```csharp
// ❌ WRONG - Blocking call
public Invoice GetById(Guid id) => _context.Invoices.FirstOrDefault(i => i.Id == id);

// ✅ RIGHT - Async all the way
public async Task<Invoice?> GetByIdAsync(Guid id) 
    => await _context.Invoices.FirstOrDefaultAsync(i => i.Id == id);
```

---

## 📋 Naming Conventions at a Glance

| Type | Convention | Example |
|------|-----------|---------|
| Classes | PascalCase | `InvoiceService`, `PaymentController` |
| Interfaces | I + PascalCase | `IInvoiceService`, `IAuditClient` |
| Methods | PascalCase + Async suffix | `GetByIdAsync()`, `CreateAsync()` |
| Private fields | _camelCase | `_invoiceService`, `_context` |
| Local variables | camelCase | `invoiceId`, `totalAmount` |
| Constants | SCREAMING_SNAKE_CASE | `MAX_INVOICE_AMOUNT`, `DEFAULT_CURRENCY` |
| Events | {Entity}{Action}Event | `InvoiceCreatedEvent`, `PaymentCompletedEvent` |
| Consumers | {Event}Consumer | `PaymentCompletedConsumer` |
| Files | Match class name | `Invoice.cs`, `InvoiceService.cs` |

---

## 🏗️ Layer Responsibilities

```
┌─────────────────────────────────────────┐
│ API Layer (Controllers)                 │
│ • Accept requests                       │
│ • Return HTTP responses                 │
│ ✗ NO business logic                    │
└─────────────────────────────────────────┘
              ↓ ↑
┌─────────────────────────────────────────┐
│ Application Layer (Services)            │
│ • Orchestrate business logic            │
│ • Consume/publish events                │
│ • Call external services                │
│ ✗ NO database queries directly         │
└─────────────────────────────────────────┘
              ↓ ↑
┌─────────────────────────────────────────┐
│ Infrastructure Layer (DbContext, Repos) │
│ • Database operations                   │
│ • External integrations                 │
│ ✗ NO business logic                    │
└─────────────────────────────────────────┘
              ↓ ↑
┌─────────────────────────────────────────┐
│ Domain Layer (Entities)                 │
│ • Domain entities & enums               │
│ • Business validation                   │
│ ✗ NO external dependencies              │
│ ✗ NO framework dependencies             │
└─────────────────────────────────────────┘
```

---

## 📝 Code Templates

### Service Class Template
```csharp
public interface IInvoiceService
{
    Task<Invoice> CreateAsync(Invoice invoice);
    Task<Invoice?> GetByIdAsync(Guid id);
    Task<bool> UpdateStatusAsync(Guid id, InvoiceStatus status);
}

public class InvoiceService : IInvoiceService
{
    private readonly AppDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<InvoiceService> _logger;

    public InvoiceService(
        AppDbContext context,
        IPublishEndpoint publishEndpoint,
        ILogger<InvoiceService> logger)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<Invoice> CreateAsync(Invoice invoice)
    {
        _logger.LogInformation("Creating invoice: {InvoiceId}", invoice.Id);
        
        await _context.Invoices.AddAsync(invoice);
        await _context.SaveChangesAsync();
        
        await _publishEndpoint.Publish<IInvoiceCreatedEvent>(new
        {
            Id = invoice.Id,
            CustomerName = invoice.CustomerName,
            Amount = invoice.Amount
        });

        return invoice;
    }
}
```

### Event Consumer Template
```csharp
public class PaymentCompletedConsumer : IConsumer<IPaymentCompletedEvent>
{
    private readonly IInvoiceService _invoiceService;
    private readonly ILogger<PaymentCompletedConsumer> _logger;

    public PaymentCompletedConsumer(
        IInvoiceService invoiceService,
        ILogger<PaymentCompletedConsumer> logger)
    {
        _invoiceService = invoiceService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IPaymentCompletedEvent> context)
    {
        _logger.LogInformation("Processing payment completed event: {PaymentId}", 
            context.Message.PaymentId);
        
        try
        {
            await _invoiceService.MarkAsPaidAsync(context.Message.InvoiceId);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Invoice not found for payment");
            // Publish compensation event
        }
    }
}
```

### Controller Template
```csharp
[ApiController]
[Route("api/v{version:apiVersion}/invoice")]
[ApiVersion("1.0")]
[Authorize(Policy = Permissions.Invoice.View)]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly ILogger<InvoicesController> _logger;

    public InvoicesController(IInvoiceService invoiceService, ILogger<InvoicesController> logger)
    {
        _invoiceService = invoiceService;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Invoice>> GetInvoice(Guid id)
    {
        _logger.LogInformation("Getting invoice: {InvoiceId}", id);
        var invoice = await _invoiceService.GetByIdAsync(id);
        if (invoice is null)
            return NotFound();
        return Ok(invoice);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Invoice.Create)]
    public async Task<ActionResult<Invoice>> CreateInvoice([FromBody] CreateInvoiceRequest request)
    {
        var invoice = Invoice.Create(request.CustomerName, request.Amount);
        var created = await _invoiceService.CreateAsync(invoice);
        return CreatedAtAction(nameof(GetInvoice), new { id = created.Id }, created);
    }
}
```

### Domain Entity Template
```csharp
public class Invoice : IAuditable
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    // Factory method
    public static Invoice Create(string customerName, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(customerName))
            throw new DomainException("Customer name required");
        
        if (amount <= 0)
            throw new DomainException("Amount must be positive");
        
        if (amount > 1_000_000)
            throw new DomainException("Amount exceeds limit");

        return new Invoice
        {
            Id = Guid.NewGuid(),
            CustomerName = customerName,
            Amount = amount,
            Status = InvoiceStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    // Domain methods (pure logic)
    public void MarkAsPaid()
    {
        if (Status != InvoiceStatus.Pending)
            throw new DomainException("Cannot mark non-pending invoice as paid");
        
        Status = InvoiceStatus.Paid;
    }
}
```

### Logging Template
```csharp
// ✅ Structured logging
_logger.LogInformation("Invoice created: InvoiceId={InvoiceId}, Amount={Amount}", 
    invoice.Id, invoice.Amount);

// ✅ Warning for recoverable issues
_logger.LogWarning("Invoice not found: InvoiceId={InvoiceId}", invoiceId);

// ✅ Error with exception
_logger.LogError(ex, "Failed to process payment: PaymentId={PaymentId}", paymentId);

// ✅ Don't log sensitive data
// ❌ _logger.LogInformation("User password: {Password}", password);
```

---

## 🔍 Quick Checklist Before Commit

- [ ] No business logic in controllers
- [ ] All async methods named with `Async` suffix
- [ ] Domain layer is pure (no EF Core, no Logger)
- [ ] Events used for inter-service communication
- [ ] Authorization policies on all public endpoints
- [ ] Domain exceptions thrown (not return codes)
- [ ] Logging includes structured data
- [ ] No hardcoded strings (use constants or config)
- [ ] No N+1 queries
- [ ] Transactions used for multi-entity operations

---

## 🚀 Common Patterns

### Publishing an Event
```csharp
await _publishEndpoint.Publish<IInvoiceCreatedEvent>(new
{
    Id = invoice.Id,
    CustomerName = invoice.CustomerName,
    Amount = invoice.Amount,
    CreatedAt = invoice.CreatedAt
});
```

### Consuming an Event
```csharp
public class InvoiceCreatedConsumer : IConsumer<IInvoiceCreatedEvent>
{
    public async Task Consume(ConsumeContext<IInvoiceCreatedEvent> context)
    {
        // Use context.Message to access event data
    }
}
```

### Querying with AsNoTracking (Read-only)
```csharp
var invoices = await _context.Invoices
    .AsNoTracking()
    .Where(i => i.Status == InvoiceStatus.Paid)
    .ToListAsync();
```

### Transaction (Multiple operations)
```csharp
using (var transaction = await _context.Database.BeginTransactionAsync())
{
    try
    {
        // Multiple operations
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

### Custom Exception
```csharp
if (invoice is null)
    throw new NotFoundException($"Invoice '{id}' not found");

if (invoice.Amount > 1_000_000)
    throw new DomainException("Amount exceeds maximum limit");
```

---

## 📖 Full Documentation

Refer to [CODING_CONVENTIONS.md](./CODING_CONVENTIONS.md) for complete guidelines.

