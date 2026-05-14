# Transaction Management - Quick Reference

## 🚀 Quick Start

### Creating a Transactional Command

```csharp
// 1. Define command (implements ITransactionalCommand)
public record CreateInvoiceCommand(
    string CustomerName,
    decimal Amount
) : IRequest<InvoiceDto>, ITransactionalCommand;

// 2. Create handler (no transaction code needed!)
public class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, InvoiceDto>
{
    private readonly InvoiceDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;

    public async Task<InvoiceDto> Handle(CreateInvoiceCommand request, CancellationToken ct)
    {
        // Create entity
        var invoice = new Invoice { ... };
        _context.Invoices.Add(invoice);

        // Publish event INSIDE transaction (Outbox)
        await _publishEndpoint.Publish(new InvoiceCreatedEvent { ... }, ct);

        // NO SaveChangesAsync here!
        // TransactionBehavior's UnitOfWork.CommitAsync handles it

        return new InvoiceDto { ... };
    }
}
```

### Service Registration

```csharp
// Program.cs

// 1. Register UnitOfWork
builder.Services.AddScoped<IUnitOfWork, InvoiceUnitOfWork>();

// 2. Register MediatR with pipeline
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    
    // Order matters: Logging → Validation → Transaction
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
});
```

### Controller Usage

```csharp
[HttpPost]
public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceRequest request)
{
    var command = new CreateInvoiceCommand(request.CustomerName, request.Amount);
    var result = await _mediator.Send(command);
    return Ok(result);
}
```

## 📋 Patterns

### ✅ DO: Outbox Pattern

```csharp
// ✅ CORRECT: Publish INSIDE transaction
await using var tx = await _context.Database.BeginTransactionAsync();

_context.Payments.Add(payment);

// Event saved to OutboxMessage table (not sent yet)
await _publishEndpoint.Publish(new PaymentCreatedEvent { ... });

await _context.SaveChangesAsync(); // Commits Payment + OutboxMessage
await tx.CommitAsync();

// MassTransit Outbox Delivery Service sends message async
```

### ❌ DON'T: Publish After Commit

```csharp
// ❌ WRONG: Publish AFTER commit
await _context.SaveChangesAsync();
await tx.CommitAsync();

// Process crash here → Event lost forever!
await _publishEndpoint.Publish(event);
```

### ✅ DO: Idempotency with Response Caching

```csharp
// Check idempotency
var check = await _idempotencyService.CheckOrCreateAsync(key, payload, id, ttl);

if (!check.IsNew)
{
    // Return cached response
    return new Result(check.PaymentId, check.CachedResponse, check.StatusCode);
}

// Process request
var result = ProcessPayment(...);

// Cache response
await _idempotencyService.CacheResponseAsync(key, result, statusCode: 202);

return result;
```

### ✅ DO: Idempotent Consumers (Transactional Inbox)

> **⚠️ QUY TẮC VÀNG**: Trong Consumer, **KHÔNG** gọi `BeginTransactionAsync()` thủ công.
> MassTransit đã tự động bọc toàn bộ hàm `Consume` trong một DB Transaction thông qua `UseEntityFrameworkOutbox`.
> Việc gọi thêm sẽ gây lỗi `InvalidOperationException: The connection is already in a transaction`.

```csharp
// ✅ CORRECT: Chỉ gọi SaveChangesAsync, KHÔNG cần BeginTransaction
public async Task Consume(ConsumeContext<PaymentCompletedEvent> context)
{
    var invoice = await _context.Invoices.FindAsync(context.Message.InvoiceId);

    // ✅ Idempotent check - bắt buộc vì Consumer có thể retry
    if (invoice.Status == InvoiceStatus.Paid)
    {
        _logger.LogInformation("Invoice already paid (idempotent)");
        return; // Safe to return - MassTransit sẽ tự commit
    }

    invoice.Status = InvoiceStatus.Paid;

    // ✅ Chỉ SaveChanges, MassTransit lo Commit/Rollback
    await _context.SaveChangesAsync();
}
```

### ❌ DON'T: Mở Transaction Thủ Công trong Consumer

```csharp
// ❌ WRONG: Gây lỗi "The connection is already in a transaction"
public async Task Consume(ConsumeContext<PaymentCompletedEvent> context)
{
    // ❌ MassTransit đã mở transaction rồi, gọi thêm sẽ lỗi!
    await using var tx = await _context.Database.BeginTransactionAsync();

    var invoice = await _context.Invoices.FindAsync(context.Message.InvoiceId);
    invoice.Status = InvoiceStatus.Paid;
    await _context.SaveChangesAsync();
    await tx.CommitAsync(); // ❌ Dư thừa và gây xung đột
}
```

## 🏗️ UnitOfWork Implementation Template

```csharp
public class YourServiceUnitOfWork : IUnitOfWork
{
    private readonly YourDbContext _context;
    private IDbContextTransaction? _currentTransaction;

    public YourServiceUnitOfWork(YourDbContext context)
    {
        _context = context;
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
            throw new InvalidOperationException("Transaction already started");

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        return _currentTransaction;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
            throw new InvalidOperationException("No active transaction");

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            _currentTransaction?.Dispose();
            _currentTransaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null) return;

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            _currentTransaction?.Dispose();
            _currentTransaction = null;
        }
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
```

## 📊 Monitoring

### Prometheus Queries

```promql
# Transaction success rate
rate(transaction_total{status="committed"}[5m]) 
/ 
rate(transaction_total[5m])

# Transaction duration p95
histogram_quantile(0.95, 
  rate(transaction_duration_seconds_bucket[5m])
)

# Outbox backlog
outbox_pending_count{service="payment"}

# Duplicate rate
rate(inbox_duplicate_count[5m])
```

### Logging

```csharp
// Automatic logs from TransactionBehavior:
[INFO] Begin transaction for CreateInvoiceCommand
[INFO] Committed transaction for CreateInvoiceCommand. TransactionId: xxx

// On error:
[ERROR] Rolled back transaction for CreateInvoiceCommand. TransactionId: xxx
```

## 🔍 Troubleshooting

### Issue: Transaction Not Starting

**Symptom:** No transaction logs, changes committed immediately

**Cause:** Command doesn't implement `ITransactionalCommand` or doesn't end with "Command"

**Fix:**
```csharp
// Option 1: Marker interface
public record MyCommand(...) : IRequest<Result>, ITransactionalCommand;

// Option 2: Naming convention
public record MyCommand(...) : IRequest<Result>; // Must end with "Command"
```

### Issue: Outbox Messages Not Delivered

**Symptom:** OutboxMessage table has pending messages, events not received

**Cause:** Missing `UseBusOutbox()` configuration

**Fix:**
```csharp
x.AddEntityFrameworkOutbox<YourDbContext>(o =>
{
    o.UseSqlServer();
    o.UseBusOutbox(); // ✅ Must be present
});
```

### Issue: Duplicate Messages Processed

**Symptom:** Consumer processes same message multiple times

**Cause:** Consumer not idempotent

**Fix:**
```csharp
// Add idempotent check
if (entity.Status == TargetStatus)
{
    return; // Already processed
}
```

### Issue: Transaction Timeout

**Symptom:** `Transaction timeout (default: 30 seconds)`

**Cause:** Long-running operation in transaction

**Fix:**
```csharp
// Move expensive operations OUTSIDE transaction
var data = await ExpensiveApiCall(); // Before transaction

await using var tx = await _context.Database.BeginTransactionAsync();
_context.Add(data); // Fast operation
await _context.SaveChangesAsync();
await tx.CommitAsync();
```

## 📚 Common Mistakes

### ❌ Calling SaveChangesAsync in Handler

```csharp
// ❌ WRONG
public async Task<Result> Handle(Command request, CancellationToken ct)
{
    _context.Add(entity);
    await _context.SaveChangesAsync(ct); // ❌ Don't do this!
    return result;
}

// ✅ CORRECT
public async Task<Result> Handle(Command request, CancellationToken ct)
{
    _context.Add(entity);
    // UnitOfWork.CommitAsync will call SaveChangesAsync
    return result;
}
```

### ❌ Publishing After Commit

```csharp
// ❌ WRONG
await _context.SaveChangesAsync();
await tx.CommitAsync();
await _publishEndpoint.Publish(event); // ❌ After commit!

// ✅ CORRECT
await _publishEndpoint.Publish(event); // ✅ Inside transaction
await _context.SaveChangesAsync();
await tx.CommitAsync();
```

### ❌ Generic DbContext Injection

```csharp
// ❌ WRONG
public class TransactionBehavior<TRequest, TResponse>
{
    private readonly DbContext _context; // ❌ Which DbContext?
}

// ✅ CORRECT
public class TransactionBehavior<TRequest, TResponse>
{
    private readonly IUnitOfWork _unitOfWork; // ✅ Abstraction
}
```

### ❌ Modifying Multiple Aggregates

```csharp
// ❌ WRONG: Cross-aggregate transaction
await using var tx = await _context.Database.BeginTransactionAsync();
payment.MarkAsCompleted();
invoice.MarkAsPaid(); // ❌ Different aggregate!
await _context.SaveChangesAsync();
await tx.CommitAsync();

// ✅ CORRECT: Use events
await using var tx = await _context.Database.BeginTransactionAsync();
payment.MarkAsCompleted();
await _publishEndpoint.Publish(new PaymentCompletedEvent { ... });
await _context.SaveChangesAsync();
await tx.CommitAsync();

// Invoice service handles in separate transaction
```

## 🎯 Checklist

### New Service Setup
- [ ] Create `YourServiceUnitOfWork : IUnitOfWork`
- [ ] Register `IUnitOfWork` in DI
- [ ] Register MediatR with pipeline behaviors
- [ ] Add MassTransit Outbox/Inbox to DbContext
- [ ] Create migration for Outbox/Inbox tables

### New Command
- [ ] Implement `ITransactionalCommand` or end with "Command"
- [ ] Handler doesn't call `SaveChangesAsync`
- [ ] Events published inside handler (not after)
- [ ] Idempotency check if needed
- [ ] Response caching if needed

### New Consumer
- [ ] **KHÔNG** gọi `BeginTransactionAsync()` — MassTransit quản lý Transaction tự động (Transactional Inbox)
- [ ] Idempotent check ở đầu hàm `Consume` (bắt buộc vì Consumer có thể bị retry)
- [ ] Chỉ gọi `SaveChangesAsync()` sau khi thực hiện thay đổi dữ liệu
- [ ] Không cần gọi `CommitAsync()` hay `RollbackAsync()` thủ công
- [ ] Log đầy đủ: nhận message, skip (idempotent), thành công

## 📖 References

- **Design:** `docs/TRANSACTION_MANAGEMENT_DESIGN.md`
- **Migration:** `docs/PAYMENT_SERVICE_MIGRATION_GUIDE.md`
- **Summary:** `TRANSACTION_IMPLEMENTATION_SUMMARY.md`
- **Status:** `IMPLEMENTATION_STATUS.md`

---

*Keep this reference handy when implementing transaction management in new services!*
