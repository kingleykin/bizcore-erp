# 🔍 CODE REVIEW GUIDE - BIZCORE ERP

> **Purpose**: Provides standards and checklists for conducting effective code reviews
> ensuring all PRs meet quality, architecture, and convention standards before merge.

---

## 📋 Code Review Checklist

### 1️⃣ Architecture & Design (Critical)

#### Domain Layer Isolation
- [ ] **Domain entities don't reference external libraries** (no EF Core, no ASP.NET, no MassTransit)
- [ ] **Domain exceptions are thrown** for business violations (not return codes)
- [ ] **Factory methods used** for entity creation with validation
- [ ] **No injected dependencies** into domain entities (DbContext, Logger, HTTP client, etc.)
- [ ] **Domain enums used** for status fields (not strings)

**Example Check:**
```csharp
// ❌ REJECT: Domain entity with infrastructure dependency
public class Invoice
{
    private readonly AppDbContext _context;  // ❌ Framework dependency
    
    public void Save()
    {
        _context.SaveChangesAsync();  // ❌ DB call in domain
    }
}

// ✅ ACCEPT: Pure domain logic
public class Invoice
{
    public void MarkAsPaid()
    {
        if (Status != Pending) throw new DomainException("...");
        Status = Paid;
    }
}
```

#### Service Layer Responsibilities
- [ ] **No business logic in controllers** (validation, data transformation, queries)
- [ ] **Services orchestrate** domain logic and infrastructure
- [ ] **Dependencies injected via constructor**
- [ ] **Interface defined** for each service (IInvoiceService, etc.)
- [ ] **One service per aggregate** (not multi-aggregate god services)

**Example Check:**
```csharp
// ❌ REJECT: Business logic in controller
[HttpPost]
public async Task<IActionResult> CreateInvoice(CreateInvoiceRequest req)
{
    if (req.Amount > 1_000_000) return BadRequest("Limit exceeded");
    var invoice = new Invoice { Amount = req.Amount };
    await _context.SaveChangesAsync();
    return Ok(invoice);
}

// ✅ ACCEPT: Controller delegates to service
[HttpPost]
[Authorize(Policy = Permissions.Invoice.Create)]
public async Task<IActionResult> CreateInvoice(CreateInvoiceRequest req)
{
    var invoice = Invoice.Create(req.CustomerName, req.Amount);
    var created = await _invoiceService.CreateAsync(invoice);
    return CreatedAtAction(nameof(GetInvoice), new { id = created.Id }, created);
}
```

#### Event-Driven Communication
- [ ] **Inter-service communication uses events** (not direct HTTP calls)
- [ ] **Events published after successful persistence** (Outbox Pattern)
- [ ] **Consumers are idempotent** (can be replayed safely)
- [ ] **Events captured in BuildingBlocks.Contracts** (shared)
- [ ] **No tight coupling** between services

**Example Check:**
```csharp
// ❌ REJECT: Direct HTTP call between services
var payment = await _httpClient.GetAsync($"http://payment/api/{id}");

// ✅ ACCEPT: Event-based communication
public class PaymentCompletedConsumer : IConsumer<IPaymentCompletedEvent>
{
    public async Task Consume(ConsumeContext<IPaymentCompletedEvent> context)
    {
        await _invoiceService.MarkAsPaidAsync(context.Message.InvoiceId);
    }
}
```

### 2️⃣ Naming Conventions (Important)

#### PascalCase Enforcement
- [ ] **Classes**: `PascalCase` (InvoiceService, PaymentController)
- [ ] **Methods**: `PascalCase` with `Async` suffix (CreateAsync, GetByIdAsync)
- [ ] **Properties**: `PascalCase` (CustomerName, TotalAmount)
- [ ] **Enum values**: `PascalCase` (Pending, Paid, Cancelled)

#### camelCase for Private/Local
- [ ] **Private fields**: `_camelCase` (_context, _logger)
- [ ] **Local variables**: `camelCase` (invoiceId, totalAmount)
- [ ] **Parameters**: `camelCase` (customerId, invoiceAmount)

#### Interface Naming
- [ ] **Interfaces start with `I`**: IInvoiceService, IAuditClient
- [ ] **No `I` suffix for implementations**: InvoiceService (not IInvoiceServiceImpl)

#### File Naming
- [ ] **File name matches public class name**: Invoice.cs, InvoiceService.cs
- [ ] **One public class per file** (nested classes OK)
- [ ] **Organized by layer**: Domain/, Application/, Infrastructure/, Controllers/

**Example Check:**
```csharp
// ❌ REJECT: Wrong naming conventions
public class invoice_service { }              // Not PascalCase
private Invoice invoice;                      // Private field without underscore
public void process() { }                     // Not Async suffix
public void process_invoice() { }             // Not camelCase

// ✅ ACCEPT: Correct naming
public class InvoiceService { }
private Invoice _invoice;
public async Task ProcessAsync() { }
public async Task ProcessInvoiceAsync() { }
```

### 3️⃣ Async/Await Conventions (Critical)

#### Async Method Naming
- [ ] **All I/O operations use `async`**: Database, HTTP, File, Message Queue
- [ ] **Method names end with `Async`**: GetByIdAsync(), CreateAsync()
- [ ] **No blocking calls**: No `.Wait()`, `.Result`, `.GetAwaiter().GetResult()`
- [ ] **ConfigureAwait(false) in libraries** (OK to omit in ASP.NET Core controllers)

**Example Check:**
```csharp
// ❌ REJECT: Missing Async suffix, blocking call
public Task<Invoice> GetById(Guid id)
{
    return _context.Invoices.FirstOrDefaultAsync(i => i.Id == id);
}

var invoice = invoiceService.GetByIdAsync(id).Result;  // Blocking!

// ✅ ACCEPT: Async suffix, proper await
public async Task<Invoice> GetByIdAsync(Guid id)
{
    return await _context.Invoices.FirstOrDefaultAsync(i => i.Id == id);
}

var invoice = await invoiceService.GetByIdAsync(id);
```

#### ConfigureAwait for Library Code
- [ ] **Library classes use ConfigureAwait(false)**
- [ ] **OK to omit in ASP.NET Core controllers** (automatic)

```csharp
// ✅ ACCEPT: Library code
public async Task<Invoice> GetByIdAsync(Guid id)
{
    return await _context.Invoices
        .FirstOrDefaultAsync(i => i.Id == id)
        .ConfigureAwait(false);
}

// ✅ ACCEPT: ASP.NET Core controller (automatic)
[HttpGet("{id}")]
public async Task<IActionResult> GetInvoice(Guid id)
{
    var invoice = await _invoiceService.GetByIdAsync(id);
    return Ok(invoice);
}
```

### 4️⃣ Exception Handling (Important)

#### Exception Types
- [ ] **Domain exceptions thrown** for business violations
- [ ] **NotFoundException thrown** when resource not found
- [ ] **UnauthorizedException thrown** for access denied
- [ ] **ValidationException thrown** for input validation
- [ ] **No generic `Exception` catch**

**Example Check:**
```csharp
// ❌ REJECT: Return codes, generic exception handling
public async Task<int> CreateInvoiceAsync(Invoice invoice)
{
    try
    {
        if (string.IsNullOrEmpty(invoice.CustomerName)) return -1;  // ❌ Return code
        await _context.Invoices.AddAsync(invoice);
        return 0;
    }
    catch (Exception ex)  // ❌ Too generic
    {
        return -2;
    }
}

// ✅ ACCEPT: Typed exceptions
public async Task<Invoice> CreateInvoiceAsync(Invoice invoice)
{
    if (string.IsNullOrEmpty(invoice.CustomerName))
        throw new DomainException("Customer name is required");
    
    await _context.Invoices.AddAsync(invoice);
    await _context.SaveChangesAsync();
    return invoice;
}
```

#### Meaningful Error Messages
- [ ] **Error messages are specific** (not "Error occurred")
- [ ] **Messages include context** (IDs, values when relevant)
- [ ] **Messages are actionable** (user can understand what went wrong)

```csharp
// ❌ REJECT: Vague message
throw new DomainException("Invoice error");

// ✅ ACCEPT: Specific, actionable message
throw new DomainException($"Invoice '{id}' exceeds maximum amount of 1,000,000 VND");
```

### 5️⃣ Authorization & Security (Critical)

#### Authorization Policies
- [ ] **All public endpoints require authorization** (except /auth, /health)
- [ ] **Policies explicitly defined** (not just `[Authorize]`)
- [ ] **Policies use constants** from Permissions class
- [ ] **GET endpoints require `View` policy** at minimum
- [ ] **CREATE/UPDATE/DELETE require specific policies**

**Example Check:**
```csharp
// ❌ REJECT: Missing authorization
[HttpGet]
public async Task<IActionResult> GetInvoices() { }

// ❌ REJECT: Generic authorization
[Authorize]
public async Task<IActionResult> CreateInvoice() { }

// ✅ ACCEPT: Explicit policy
[HttpGet]
[Authorize(Policy = Permissions.Invoice.View)]
public async Task<IActionResult> GetInvoices() { }

[HttpPost]
[Authorize(Policy = Permissions.Invoice.Create)]
public async Task<IActionResult> CreateInvoice() { }
```

#### Sensitive Data Protection
- [ ] **No passwords, tokens, credit cards logged**
- [ ] **PII (names, emails, phones) logged only when necessary**
- [ ] **Use masking for sensitive data** in audit logs
- [ ] **No credentials in appsettings.json** (use secrets manager)
- [ ] **API keys stored in secure configuration**

### 6️⃣ Logging & Observability (Important)

#### Structured Logging
- [ ] **Structured logging used** (SeriLog with properties)
- [ ] **Avoid string interpolation** in log messages
- [ ] **Use logging levels correctly**: Debug, Info, Warning, Error, Critical
- [ ] **Correlation IDs included** in distributed tracing
- [ ] **No sensitive data logged**

**Example Check:**
```csharp
// ❌ REJECT: String interpolation, no structure
_logger.LogInformation($"Invoice {invoice.Id} created for {invoice.CustomerName}");

// ✅ ACCEPT: Structured logging
_logger.LogInformation("Invoice created: InvoiceId={InvoiceId}, CustomerName={CustomerName}", 
    invoice.Id, invoice.CustomerName);

// ✅ ACCEPT: Appropriate log level
_logger.LogDebug("Processing event: {EventId}", eventId);              // Development detail
_logger.LogInformation("Invoice created: {InvoiceId}", invoiceId);     // Business event
_logger.LogWarning("Invoice not found: {InvoiceId}", invoiceId);       // Recoverable
_logger.LogError(ex, "Failed to save invoice: {InvoiceId}", invoiceId); // Error with context
```

#### Log Levels
- [ ] **Debug**: Detailed troubleshooting, variable values
- [ ] **Information**: Important business events, workflow milestones
- [ ] **Warning**: Unexpected but recoverable (not found, retrying)
- [ ] **Error**: Exception with recovery possible
- [ ] **Critical**: System unstable, immediate action needed

### 7️⃣ Database & ORM (Important)

#### Entity Framework Usage
- [ ] **AsNoTracking() used for read-only queries**
- [ ] **Select() used** to fetch only needed columns
- [ ] **Transactions used** for multi-entity operations
- [ ] **No N+1 queries** (use .Include() for related entities)
- [ ] **Migrations named descriptively** (timestamp_Description)

**Example Check:**
```csharp
// ❌ REJECT: No AsNoTracking, extra columns loaded
public async Task<IEnumerable<Invoice>> GetAllAsync()
    => await _context.Invoices.ToListAsync();  // Loads all columns, enables tracking

// ❌ REJECT: N+1 query
var invoices = await _context.Invoices.ToListAsync();
foreach (var invoice in invoices)
{
    var payments = await _context.Payments.Where(p => p.InvoiceId == invoice.Id).ToListAsync();
}

// ✅ ACCEPT: AsNoTracking, Select specific columns
public async Task<IEnumerable<InvoiceSummaryDto>> GetSummaryAsync()
    => await _context.Invoices
        .AsNoTracking()
        .Select(i => new InvoiceSummaryDto { Id = i.Id, CustomerName = i.CustomerName })
        .ToListAsync();

// ✅ ACCEPT: Include for related entities
var invoices = await _context.Invoices
    .Include(i => i.Payments)
    .ToListAsync();
```

#### Concurrency Control
- [ ] **RowVersion used** for optimistic concurrency
- [ ] **Concurrency checks performed** before updates
- [ ] **DbUpdateConcurrencyException handled** appropriately

### 8️⃣ Testing (Important)

#### Test Coverage
- [ ] **Unit tests for domain logic** (Happy path + edge cases)
- [ ] **Tests are independent** (no shared setup/teardown state)
- [ ] **Mocks used appropriately** (not over-mocked)
- [ ] **Test names describe scenario** (GetByIdAsync_WithInvalidId_ThrowsNotFoundException)

**Example Check:**
```csharp
// ❌ REJECT: Vague test name, depends on other tests
[Fact]
public async Task Test1()
{
    // Assumes data from Test0
}

// ✅ ACCEPT: Clear name, independent setup
[Fact]
public async Task GetByIdAsync_WithInvalidId_ThrowsNotFoundException()
{
    // Arrange: Create test data
    var invalidId = Guid.NewGuid();
    _mockRepository.Setup(r => r.GetByIdAsync(invalidId))
        .ReturnsAsync((Invoice)null);
    
    // Act & Assert
    await Assert.ThrowsAsync<NotFoundException>(
        () => _service.GetByIdAsync(invalidId));
}
```

#### Test Naming Convention
- [ ] **Pattern: `{MethodName}_{Scenario}_{ExpectedResult}`**
- [ ] Examples: `CreateAsync_WithValidData_ReturnsInvoice`
- [ ] Examples: `UpdateAsync_WithNonexistentId_ThrowsNotFoundException`

### 9️⃣ Code Quality (Guideline)

#### DRY - Don't Repeat Yourself
- [ ] **Duplicated code extracted** to methods/utilities
- [ ] **Shared utilities placed** in BuildingBlocks
- [ ] **Extension methods used** for common operations

#### SOLID Principles
- [ ] **Single Responsibility**: One reason to change
- [ ] **Open/Closed**: Open to extension, closed to modification
- [ ] **Liskov Substitution**: Subtypes can replace base types
- [ ] **Interface Segregation**: Clients don't depend on unused methods
- [ ] **Dependency Inversion**: Depend on abstractions, not concretions

#### Code Smells to Avoid
- [ ] **God Classes**: Class doing too much
- [ ] **Long Methods**: >20 lines usually needs refactoring
- [ ] **Deep Nesting**: >3 levels usually needs extraction
- [ ] **Magic Numbers**: Use named constants
- [ ] **Missing Comments**: Complex logic needs explanation

### 🔟 Performance (Guideline)

#### Query Performance
- [ ] **N+1 queries avoided** (.Include for related data)
- [ ] **Indexes used** for frequently queried fields
- [ ] **Pagination used** for large result sets
- [ ] **Caching considered** for frequently accessed data

#### Async Performance
- [ ] **Parallel operations** used when appropriate
- [ ] **No unnecessary allocations** (LINQ).
- [ ] **StringBuilders used** for string concatenation in loops

---

## 🎯 Review Process

### Step 1: Quick Scan (2 min)
1. Read PR title and description
2. Check file count (>10 files might indicate scope creep)
3. Skim changed files to understand context

### Step 2: Architecture Review (5 min)
1. Check for business logic in wrong layers
2. Verify interfaces defined for services
3. Confirm events used for inter-service communication
4. Check domain layer purity

### Step 3: Code Style Review (5 min)
1. Verify naming conventions followed
2. Check for async/await consistency
3. Review error handling
4. Verify authorization present

### Step 4: Detailed Code Review (10 min)
1. Read code line by line
2. Check for logic errors
3. Verify SQL query efficiency
4. Look for potential exceptions

### Step 5: Testing Review (3 min)
1. Verify unit tests present for changes
2. Check test quality and independence
3. Look for edge cases covered

### Step 6: Final Check (2 min)
1. Verify PR passes CI/CD
2. Check test coverage maintained
3. Confirm no TODOs or FIXMEs left
4. Approve or request changes

---

## ✅ Approval Criteria

Approve PR when:
- ✅ All critical issues resolved
- ✅ Naming conventions followed
- ✅ Tests added/updated
- ✅ Authorization properly configured
- ✅ Documentation updated (if needed)
- ✅ No performance regressions
- ✅ Code is maintainable and readable

---

## 🚫 Request Changes When

Request changes for:
- ❌ Business logic in wrong layer
- ❌ Missing authorization
- ❌ Blocking calls instead of async
- ❌ Generic exceptions caught
- ❌ Naming convention violations
- ❌ N+1 queries or inefficient queries
- ❌ Missing or poor test coverage
- ❌ Sensitive data logged

---

## 💬 Code Review Comments

### When Requesting Changes (Be Respectful)

**Good:**
```
Consider moving this business logic to the service layer for better separation of concerns.
Here's an example of the recommended pattern: [link]
```

**Bad:**
```
This is wrong. You always put logic in services, not controllers.
```

### Explain the "Why"

```csharp
// ❌ Comment doesn't help
var invoice = await _context.Invoices.AsNoTracking().ToListAsync();

// ✅ Good explanation
// AsNoTracking() improves query performance for read-only operations
// since EF Core doesn't need to track changes for these entities
var invoice = await _context.Invoices.AsNoTracking().ToListAsync();
```

### Suggest Solutions

```csharp
// Instead of:
// ❌ "This violates SOLID principles"

// Say:
// Consider extracting the validation logic to a separate IInvoiceValidator
// to follow the Single Responsibility principle. See [pattern link]

public class InvoiceService
{
    private readonly IInvoiceValidator _validator;
    
    public async Task<Invoice> CreateAsync(Invoice invoice)
    {
        var validationResult = _validator.Validate(invoice);
        if (!validationResult.IsValid) throw new DomainException(...);
    }
}
```

---

## 📊 Review Metrics

Track code review effectiveness:
- **Average Review Time**: Target <30 min per PR
- **Approval Rate**: Track % of PRs approved vs. requesting changes
- **Common Issues**: Track which issues appear most to provide targeted training
- **Review Comments**: Track comment quality (helpful vs. nitpicky)

---

## 🎓 Reviewer Training

### For New Reviewers
1. Read CODING_CONVENTIONS.md
2. Read CONVENTIONS_QUICK_REFERENCE.md
3. Review last 10 merged PRs
4. Co-review with experienced reviewer
5. Start reviewing independently

### Continuous Improvement
- Weekly sync on review patterns
- Share tricky reviews for discussion
- Update guidelines based on lessons learned
- Recognize good reviews and reviewers

---

**Last Updated**: 2024-05-07  
**Version**: 1.0
