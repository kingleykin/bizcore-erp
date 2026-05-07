# Transaction Management Design - Production Implementation

## 🎯 Tổng quan

Document này mô tả chiến lược quản lý Transaction trong hệ thống Bizcore ERP để đảm bảo tính toàn vẹn dữ liệu (Data Integrity) khi có nhiều thao tác ghi trên nhiều bảng trong cùng một logic nghiệp vụ.

---

## ✅ 0. Quyết định Thiết kế Sau Review

Phần này là nguồn sự thật hiện tại nếu các ví dụ cũ trong tài liệu hoặc tài liệu liên quan còn khác nhau.

### 0.1. Transaction boundary theo entrypoint

| Entry point | Cách quản lý transaction | Ghi chú |
|-------------|--------------------------|---------|
| HTTP command qua MediatR | `TransactionBehavior` + `IUnitOfWork` | Handler không tự gọi `SaveChangesAsync()` nếu `CommitAsync()` đã save |
| MassTransit consumer | Consumer transaction/helper riêng | Không kỳ vọng MediatR pipeline chạy cho consumer |
| Background job/seeder | Explicit transaction/helper riêng | Giữ transaction ngắn, có retry strategy |
| Query/read-only | Không mở transaction thủ công | Dùng default read behavior của EF/DB |

**Quy ước quan trọng:** handler không gọi trực tiếp `_context.SaveChangesAsync()` khi đã dùng `TransactionBehavior`. `IUnitOfWork.CommitAsync()` sẽ save + commit ở cuối flow. Ngoại lệ duy nhất là helper hạ tầng có chủ đích flush trong cùng DbContext/transaction để bắt unique constraint, ví dụ idempotency reservation; helper đó không được tự commit transaction.

### 0.2. Outbox/Inbox là bắt buộc cho DB + message broker

Khi vừa ghi DB vừa publish event/command:

1. Publish/send phải xảy ra bên trong transaction.
2. MassTransit EF Outbox phải dùng cùng `DbContext`/database với aggregate đang ghi.
3. Consumer vừa ghi DB vừa publish tiếp phải dùng consumer outbox/inbox hoặc transaction helper tương đương.
4. Consumer vẫn phải idempotent vì hệ thống message broker là at-least-once.

### 0.3. Audit hash chain dùng partitioned append, không dùng global Serializable mặc định

Thiết kế hiện tại chọn **partitioned hash chain** thay vì global hash chain. Mỗi partition cần có thứ tự append xác định:

- `PartitionKey`: ví dụ `EntityType` hoặc `TenantId:EntityType`
- `Sequence`: số tăng đơn điệu trong partition
- Unique index: `(PartitionKey, Sequence)`
- Hash trước đó được lấy theo `Sequence`, không dựa vào `Timestamp`

`Read Committed` chỉ an toàn nếu có cơ chế serialize append trong từng partition, ví dụ row lock trên bảng `AuditHashChainHeads`, application lock theo `PartitionKey`, hoặc optimistic retry khi unique/concurrency conflict. Nếu chưa có cơ chế này, dùng `Serializable` theo partition là phương án an toàn tạm thời, nhưng không dùng global Serializable cho toàn bảng.

### 0.4. Idempotency cần response replay và trạng thái xử lý

`IdempotencyRecord` không chỉ map `Key -> PaymentId`. Record cần lưu:

- `RequestHash` để phát hiện cùng key nhưng payload khác.
- `Status`: `InProgress`, `Completed`, `Failed`, `Expired`.
- `ResponseJson` + `StatusCode` để replay response giống lần đầu.
- Unique constraint trên `Key`; nên có unique index trên `Payments.IdempotencyKey`.

Không xóa record hết hạn nếu business operation liên quan vẫn còn có thể hoàn tất muộn.

### 0.5. Saga không được làm hai aggregate "thành công một nửa"

Payment saga phải có ownership rõ:

- Khuyến nghị: Invoice service chỉ validate invoice; Payment completed xong mới publish event để Invoice mark paid trong transaction riêng.
- Nếu Invoice mark paid trước khi Payment confirmed, saga bắt buộc có compensation để revert/adjust invoice khi confirm payment thất bại.

Không để trạng thái `Invoice = Paid` nhưng `Payment = Processing/Failed` mà không có reconciliation hoặc compensation.

---

## 📊 1. Phân tích Vấn đề Hiện tại

### 1.1. Các Trường hợp Thiếu Transaction

Sau khi phân tích code, phát hiện các điểm yếu sau:

#### ❌ **Payment Service - InitiatePaymentAsync**
```csharp
// File: Payment.API/Application/Services/PaymentService.cs
public async Task<PaymentInitiationResult> InitiatePaymentAsync(...)
{
    // 1. Tạo IdempotencyRecord
    var idempotencyResult = await _idempotencyService.CheckOrCreateAsync(...);
    
    // 2. Tạo Payment
    _context.Payments.Add(payment);
    await _context.SaveChangesAsync();  // ❌ Không có transaction
    
    // 3. Publish Event
    await _publishEndpoint.Publish(new PaymentInitiatedEvent(...));
}
```

**Vấn đề:**
- Nếu `SaveChangesAsync()` thành công nhưng `Publish()` thất bại → Payment đã lưu nhưng không có event
- Nếu `CheckOrCreateAsync()` thành công nhưng `SaveChangesAsync()` thất bại → IdempotencyRecord tồn tại nhưng không có Payment

#### ❌ **Invoice Service - CreateInvoiceAsync**
```csharp
// File: Invoice.API/Application/Services/InvoiceService.cs
public async Task<InvoiceEntity> CreateInvoiceAsync(...)
{
    // 1. Tạo Invoice
    _context.Invoices.Add(invoice);
    
    // 2. Publish Event
    await _publishEndpoint.Publish(new InvoiceCreatedEvent(...));
    
    // 3. SaveChanges
    await _context.SaveChangesAsync();  // ❌ Event đã publish trước khi commit
}
```

**Vấn đề:**
- Event được publish trước khi transaction commit
- Nếu `SaveChangesAsync()` thất bại → Event đã được gửi nhưng Invoice không tồn tại trong DB

#### ❌ **Audit Service - AuditEventConsumer**
```csharp
// File: Audit.API/Application/Consumers/AuditEventConsumer.cs
public async Task Consume(ConsumeContext<IAuditEvent> context)
{
    // 1. Compute Hash (cần previous hash từ DB)
    await _hashChainService.ComputeAndSetHashAsync(entry);
    
    // 2. Save AuditEntry
    _db.AuditEntries.Add(entry);
    await _db.SaveChangesAsync();  // ❌ Không có transaction bảo vệ hash chain
}
```

**Vấn đề:**
- Nếu có 2 concurrent requests → Race condition khi đọc `PreviousHash`
- Hash chain có thể bị break nếu transaction không được bảo vệ

---

## 🏗️ 2. Kiến trúc Giải pháp

### 2.1. Phân loại Transaction Patterns

Dự án cần áp dụng **3 patterns** khác nhau tùy theo ngữ cảnh:

| Pattern | Khi nào dùng | Ví dụ |
|---------|--------------|-------|
| **Local Transaction** | Nhiều thao tác trong cùng 1 DB | Payment + IdempotencyRecord |
| **Outbox Pattern** | DB + Message Broker | Invoice + Publish Event |
| **Saga Pattern** | Cross-service transaction | Payment → Invoice → Report |

### 2.2. Quyết định Thiết kế

```
┌─────────────────────────────────────────────────────────────────┐
│                   Transaction Strategy                          │
└─────────────────────────────────────────────────────────────────┘

┌──────────────────┐
│  Single Service  │
│  Multiple Tables │
└────────┬─────────┘
         │
         ├─ Same Database? ──YES──> Local Transaction (DbContext.Database.BeginTransaction)
         │
         └─ Need Publish Event? ──YES──> Outbox Pattern (MassTransit Outbox)

┌──────────────────┐
│  Cross-Service   │
│  Coordination    │
└────────┬─────────┘
         │
         └─ Eventual Consistency ──> Saga Pattern (Compensation Events)
```

---

## 🔧 3. Implementation Chi tiết

### 3.1. MediatR Transaction Pipeline (Production Best Practice)

**Vấn đề với Manual Transaction:**
- Code lặp lại nhiều lần (BeginTransaction, Commit, Rollback)
- Khó maintain và dễ quên
- Không centralized logging/monitoring

**❌ Vấn đề với Generic DbContext Injection:**

```csharp
// ❌ ANTI-PATTERN: Inject DbContext generic
public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly DbContext _context; // ❌ Sai!
    
    public TransactionBehavior(DbContext context) // ❌ Sai!
    {
        _context = context;
    }
}
```

**Vấn đề:**
- Multiple DbContext trong microservice (InvoiceDbContext, PaymentDbContext, AuditDbContext)
- DI container không biết inject DbContext nào
- Nested DbContext
- Wrong transaction boundary

**✅ Giải pháp: IUnitOfWork Abstraction**

```csharp
// File: BuildingBlocks/Bizcore.BuildingBlocks/Abstractions/IUnitOfWork.cs
public interface IUnitOfWork
{
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

// File: BuildingBlocks/Bizcore.BuildingBlocks/Abstractions/ITransactionalCommand.cs
/// <summary>
/// Marker interface for commands that require transaction
/// </summary>
public interface ITransactionalCommand
{
}
```

**Implementation:**

```csharp
// File: Payment.API/Infrastructure/Data/PaymentUnitOfWork.cs
using Microsoft.EntityFrameworkCore.Storage;

public class PaymentUnitOfWork : IUnitOfWork
{
    private readonly PaymentDbContext _context;
    private IDbContextTransaction? _currentTransaction;

    public PaymentUnitOfWork(PaymentDbContext context)
    {
        _context = context;
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            throw new InvalidOperationException("Transaction already started");
        }

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        return _currentTransaction;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("No active transaction");
        }

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
        if (_currentTransaction == null)
        {
            return;
        }

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

**Transaction Behavior với IUnitOfWork:**

```csharp
// File: BuildingBlocks/Bizcore.BuildingBlocks/Behaviors/TransactionBehavior.cs
using MediatR;
using Microsoft.Extensions.Logging;

public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(
        IUnitOfWork unitOfWork,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var typeName = request.GetType().Name;

        // Skip transaction for queries or non-transactional commands
        if (!IsTransactionalCommand(request))
        {
            return await next();
        }

        _logger.LogInformation("Begin transaction for {CommandName}", typeName);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var response = await next();

            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Committed transaction for {CommandName}. TransactionId: {TransactionId}",
                typeName,
                transaction.TransactionId
            );

            return response;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);

            _logger.LogError(
                ex,
                "Rolled back transaction for {CommandName}. TransactionId: {TransactionId}",
                typeName,
                transaction.TransactionId
            );

            throw;
        }
    }

    private static bool IsTransactionalCommand(TRequest request)
    {
        // Option 1: Marker interface
        if (request is ITransactionalCommand)
        {
            return true;
        }

        // Option 2: Convention (Commands need transaction, Queries don't)
        var typeName = request.GetType().Name;
        return typeName.EndsWith("Command") && !typeName.EndsWith("Query");
    }
}
```

**Registration:**

```csharp
// File: Payment.API/Program.cs

// ✅ Register UnitOfWork
builder.Services.AddScoped<IUnitOfWork, PaymentUnitOfWork>();

// ✅ Register MediatR with Transaction Pipeline
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    
    // ✅ Add Transaction Pipeline (uses IUnitOfWork)
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
    
    // Optional: Add other behaviors
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
});
```

**Usage - Option 1: Marker Interface**

```csharp
// File: Payment.API/Application/Commands/InitiatePaymentCommand.cs
public record InitiatePaymentCommand(
    Guid InvoiceId,
    decimal Amount,
    string IdempotencyKey
) : IRequest<PaymentInitiationResult>, ITransactionalCommand; // ✅ Marker interface

public class InitiatePaymentCommandHandler : IRequestHandler<InitiatePaymentCommand, PaymentInitiationResult>
{
    private readonly PaymentDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IIdempotencyService _idempotencyService;

    public async Task<PaymentInitiationResult> Handle(
        InitiatePaymentCommand request,
        CancellationToken cancellationToken)
    {
        // ✅ NO manual transaction code!
        // TransactionBehavior handles it automatically via IUnitOfWork
        
        // 1. Check idempotency
        var idempotencyResult = await _idempotencyService.CheckOrCreateAsync(
            request.IdempotencyKey,
            new { request.InvoiceId, request.Amount },
            Guid.NewGuid(),
            TimeSpan.FromMinutes(30)
        );

        if (!idempotencyResult.IsNew)
        {
            return idempotencyResult.CachedResponse;
        }

        // 2. Create payment
        var payment = new Payment
        {
            Id = idempotencyResult.PaymentId,
            InvoiceId = request.InvoiceId,
            Amount = request.Amount,
            Status = PaymentStatus.Processing,
            IdempotencyKey = request.IdempotencyKey
        };

        _context.Payments.Add(payment);

        // 3. Publish event (saved to Outbox)
        await _publishEndpoint.Publish(new PaymentInitiatedEvent
        {
            PaymentId = payment.Id,
            InvoiceId = payment.InvoiceId,
            Amount = payment.Amount
        }, cancellationToken);

        // 4. Do NOT call SaveChangesAsync here.
        // UnitOfWork.CommitAsync in TransactionBehavior saves Payment + IdempotencyRecord + OutboxMessage.

        var result = new PaymentInitiationResult(true, payment.Id);
        
        // 5. Cache response
        await _idempotencyService.CacheResponseAsync(request.IdempotencyKey, result);

        return result;
    }
}
```

**Usage - Option 2: Convention-based**

```csharp
// File: Invoice.API/Application/Commands/CreateInvoiceCommand.cs
// ✅ No marker interface needed - convention: ends with "Command"
public record CreateInvoiceCommand(
    string CustomerName,
    decimal Amount
) : IRequest<InvoiceDto>;

public class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, InvoiceDto>
{
    private readonly InvoiceDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;

    public async Task<InvoiceDto> Handle(
        CreateInvoiceCommand request,
        CancellationToken cancellationToken)
    {
        // ✅ NO manual transaction code!
        
        var invoice = InvoiceEntity.Create(request.CustomerName, request.Amount);
        _context.Invoices.Add(invoice);

        await _publishEndpoint.Publish(new InvoiceCreatedEvent
        {
            Id = invoice.Id,
            CustomerName = invoice.CustomerName,
            Amount = invoice.Amount
        }, cancellationToken);

        // Do NOT call SaveChangesAsync here.
        // TransactionBehavior commits the Invoice and OutboxMessage together.

        return new InvoiceDto
        {
            Id = invoice.Id,
            CustomerName = invoice.CustomerName,
            Amount = invoice.Amount,
            Status = invoice.Status
        };
    }
}

// Query (no transaction)
public record GetInvoiceQuery(Guid Id) : IRequest<InvoiceDto>;

public class GetInvoiceQueryHandler : IRequestHandler<GetInvoiceQuery, InvoiceDto>
{
    private readonly InvoiceDbContext _context;

    public async Task<InvoiceDto> Handle(
        GetInvoiceQuery request,
        CancellationToken cancellationToken)
    {
        // ✅ No transaction for queries (automatically skipped by pipeline)
        
        var invoice = await _context.Invoices.FindAsync(request.Id);
        
        return new InvoiceDto
        {
            Id = invoice.Id,
            CustomerName = invoice.CustomerName,
            Amount = invoice.Amount,
            Status = invoice.Status
        };
    }
}
```

**Lợi ích của IUnitOfWork:**
- ✅ Centralized transaction logic
- ✅ Automatic rollback on exception
- ✅ Centralized logging
- ✅ No duplicated code
- ✅ **Correct DbContext injection** (mỗi service có UnitOfWork riêng)
- ✅ **No multiple DbContext issues**
- ✅ **Clear transaction boundary**
- ✅ Easy to test (mock IUnitOfWork)
- ✅ Easy to maintain

**Testing:**

```csharp
[Fact]
public async Task TransactionBehavior_RollbackOnException()
{
    // Arrange
    var mockUnitOfWork = new Mock<IUnitOfWork>();
    var mockTransaction = new Mock<IDbContextTransaction>();
    
    mockUnitOfWork
        .Setup(x => x.BeginTransactionAsync(default))
        .ReturnsAsync(mockTransaction.Object);
    
    var behavior = new TransactionBehavior<InitiatePaymentCommand, PaymentInitiationResult>(
        mockUnitOfWork.Object,
        Mock.Of<ILogger<TransactionBehavior<InitiatePaymentCommand, PaymentInitiationResult>>>()
    );
    
    // Act & Assert
    await Assert.ThrowsAsync<Exception>(() =>
        behavior.Handle(
            new InitiatePaymentCommand(Guid.NewGuid(), 1000m, "key-123"),
            () => throw new Exception("Handler failed"),
            default
        )
    );
    
    // Verify: Rollback was called
    mockUnitOfWork.Verify(x => x.RollbackAsync(default), Times.Once);
    mockUnitOfWork.Verify(x => x.CommitAsync(default), Times.Never);
}
```

---

### 3.2. Local Transaction Pattern

**Khi nào dùng:**
- Nhiều thao tác ghi trên nhiều bảng trong cùng 1 database
- Cần ACID guarantee
- Không có message broker involved

**Implementation:**

```csharp
// ✅ GOOD: Sử dụng Transaction với Outbox Pattern
public async Task<PaymentInitiationResult> InitiatePaymentAsync(
    Payment payment, 
    string idempotencyKey)
{
    PaymentInitiationResult result;

    // Bắt đầu transaction
    await using var transaction = await _context.Database.BeginTransactionAsync();
    
    try
    {
        // 1. Check/Create Idempotency Record
        var idempotencyResult = await _idempotencyService.CheckOrCreateAsync(
            idempotencyKey,
            new { payment.InvoiceId, payment.Amount },
            payment.Id,
            TimeSpan.FromMinutes(30)
        );
        
        if (!idempotencyResult.IsNew)
        {
            // Duplicate request - rollback và return existing response
            await transaction.RollbackAsync();
            return idempotencyResult.CachedResponse; // Response replay
        }
        
        // 2. Create Payment
        _context.Payments.Add(payment);
        
        // 3. Publish event (BÊN TRONG transaction - saved to Outbox)
        // ⚠️ QUAN TRỌNG: Event KHÔNG được gửi lên RabbitMQ ngay
        // MassTransit sẽ lưu vào OutboxMessage table
        await _publishEndpoint.Publish(new PaymentInitiatedEvent
        {
            PaymentId = payment.Id,
            InvoiceId = payment.InvoiceId,
            Amount = payment.Amount
        });
        
        // 4. Save changes (commits Payment + IdempotencyRecord + OutboxMessage)
        await _context.SaveChangesAsync();
        
        // 5. Commit transaction
        await transaction.CommitAsync();
        
        // 6. MassTransit Outbox Delivery Service sẽ tự động gửi message từ OutboxMessage
        //    lên RabbitMQ trong background (async, reliable)
        
        result = new PaymentInitiationResult(true, payment.Id);
        
        return result;
    }
    catch (Exception ex)
    {
        // Rollback on error
        await transaction.RollbackAsync();
        _logger.LogError(ex, "Failed to initiate payment. Transaction rolled back.");
        throw;
    }

    // 7. Cache response for idempotency replay
    await _idempotencyService.CacheResponseAsync(idempotencyKey, result);
}
```

**Lưu ý quan trọng:**
- ✅ Event được publish **BÊN TRONG** transaction (không phải sau commit)
- ✅ MassTransit intercept `Publish()` và lưu vào `OutboxMessage` table
- ✅ Outbox Delivery Service gửi message lên RabbitMQ sau khi transaction commit
- ✅ Nếu process crash sau commit → Message vẫn an toàn trong OutboxMessage table
- ✅ Idempotency check nằm trong transaction → Thread-safe
- ✅ Response được cache để replay cho duplicate requests

---

### 3.2. Outbox Pattern (MassTransit)

**Khi nào dùng:**
- Cần đảm bảo DB write và Message publish là atomic
- Tránh "dual write problem" (DB success nhưng message lost)

**Vấn đề của cách cũ:**
```csharp
// ❌ BAD: Dual Write Problem
await _context.SaveChangesAsync();           // DB committed
await _publishEndpoint.Publish(event);       // Network failure → Event lost!

// ❌ WORSE: Publish after commit
await transaction.CommitAsync();
await _publishEndpoint.Publish(event);       // Process crash → Event lost forever!
```

**Giải pháp: MassTransit Outbox**

#### Step 1: Enable Outbox trong DbContext

```csharp
// File: Payment.API/Infrastructure/Data/PaymentDbContext.cs
public class PaymentDbContext : DbContext
{
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // ✅ Enable MassTransit Outbox
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
    }
}
```

#### Step 2: Configure Outbox trong Program.cs

```csharp
// File: Payment.API/Program.cs
builder.Services.AddMassTransit(x =>
{
    x.AddConsumers(typeof(Program).Assembly);
    
    // ✅ Configure Outbox
    x.AddEntityFrameworkOutbox<PaymentDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
        
        // Delivery settings
        o.QueryDelay = TimeSpan.FromSeconds(1);
        o.MessageDeliveryLimit = 3;
        o.MessageDeliveryTimeout = TimeSpan.FromMinutes(5);
    });
    
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });
        
        cfg.ConfigureEndpoints(context);
    });
});
```

#### Step 3: Migration để tạo Outbox tables

```bash
# Tạo migration
dotnet ef migrations add AddMassTransitOutbox --project src/Services/Payment/Payment.API

# Apply migration
dotnet ef database update --project src/Services/Payment/Payment.API
```

**Outbox tables được tạo:**
- `InboxState` - Deduplication cho incoming messages
- `OutboxState` - Tracking outbox delivery
- `OutboxMessage` - Pending messages chờ gửi

#### Step 4: Sử dụng Outbox trong Service

```csharp
// ✅ GOOD: Outbox Pattern
public async Task<InvoiceEntity> CreateInvoiceAsync(
    string customerName, 
    decimal amount)
{
    // Bắt đầu transaction
    await using var transaction = await _context.Database.BeginTransactionAsync();
    
    try
    {
        // 1. Create Invoice
        var invoice = InvoiceEntity.Create(customerName, amount);
        _context.Invoices.Add(invoice);
        
        // 2. Publish Event (sẽ được lưu vào OutboxMessage table)
        await _publishEndpoint.Publish(new InvoiceCreatedEvent
        {
            Id = invoice.Id,
            CustomerName = invoice.CustomerName,
            Amount = invoice.Amount,
            CreatedAt = invoice.CreatedAt
        });
        
        // 3. SaveChanges (commit cả Invoice và OutboxMessage)
        await _context.SaveChangesAsync();
        
        // 4. Commit transaction
        await transaction.CommitAsync();
        
        // MassTransit Outbox sẽ tự động gửi message từ OutboxMessage table
        
        return invoice;
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        _logger.LogError(ex, "Failed to create invoice. Transaction rolled back.");
        throw;
    }
}
```

**Cách hoạt động:**
1. `Publish()` không gửi message ngay, mà lưu vào `OutboxMessage` table
2. `SaveChangesAsync()` commit cả Invoice và OutboxMessage trong cùng 1 transaction
3. MassTransit Outbox Delivery Service định kỳ quét `OutboxMessage` và gửi lên RabbitMQ
4. Sau khi gửi thành công, message được xóa khỏi Outbox

**Lợi ích:**
- ✅ Atomic: Invoice và Event cùng commit hoặc cùng rollback
- ✅ Reliable: Nếu RabbitMQ down, message vẫn an toàn trong DB
- ✅ Retry: MassTransit tự động retry nếu gửi thất bại
- ✅ No message loss: Nếu process crash sau commit, message vẫn trong OutboxMessage

---

### 3.4. Inbox Pattern (Consumer Deduplication)

**Vấn đề:**
RabbitMQ đảm bảo **at-least-once delivery** → Message có thể bị duplicate

**Ví dụ:**
```
1. Consumer nhận PaymentCompletedEvent
2. Consumer xử lý thành công
3. Consumer crash trước khi ACK
4. RabbitMQ gửi lại message
5. Consumer xử lý lần 2 → DUPLICATE!
```

**Giải pháp: MassTransit Inbox**

MassTransit tự động tạo `InboxState` table để track processed messages:

```csharp
// File: Payment.API/Infrastructure/Data/PaymentDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    // ✅ Enable Inbox for deduplication
    modelBuilder.AddInboxStateEntity();
    modelBuilder.AddOutboxStateEntity();
    modelBuilder.AddOutboxMessageEntity();
}
```

**Cách hoạt động:**

```
┌─────────────────────────────────────────────────────────────────┐
│                    Inbox Deduplication Flow                     │
└─────────────────────────────────────────────────────────────────┘

1. Message arrives: PaymentCompletedEvent (MessageId: abc-123)

2. MassTransit checks InboxState:
   SELECT * FROM InboxState WHERE MessageId = 'abc-123'

3a. NOT FOUND (First time):
    ├─ INSERT INTO InboxState (MessageId, Received, ...)
    ├─ Execute consumer logic
    ├─ UPDATE InboxState SET Consumed = NOW()
    └─ ACK message

3b. FOUND (Duplicate):
    ├─ Check Consumed timestamp
    ├─ Skip consumer logic (already processed)
    └─ ACK message immediately
```

**Consumer Implementation:**

```csharp
// File: Invoice.API/Application/Consumers/ApplyPaymentToInvoiceConsumer.cs
public class ApplyPaymentToInvoiceConsumer : IConsumer<IPaymentCompletedEvent>
{
    private readonly InvoiceDbContext _context;
    private readonly ILogger<ApplyPaymentToInvoiceConsumer> _logger;

    public async Task Consume(ConsumeContext<IPaymentCompletedEvent> context)
    {
        var message = context.Message;

        // ✅ MassTransit Inbox automatically handles deduplication
        // No manual duplicate check needed!

        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(i => i.Id == message.InvoiceId);

        if (invoice is null)
        {
            _logger.LogWarning("Invoice not found: {InvoiceId}", message.InvoiceId);
            
            // Publish compensation event
            await context.Publish(new PaymentCompensationRequestedEvent
            {
                PaymentId = message.PaymentId,
                Reason = "Invoice not found"
            });
            
            return;
        }

        // ✅ Idempotent check at business level
        if (invoice.Status == InvoiceStatus.Paid)
        {
            _logger.LogInformation(
                "Invoice already paid (idempotent). InvoiceId: {InvoiceId}",
                message.InvoiceId
            );
            return; // Safe to return, already processed
        }

        invoice.Status = InvoiceStatus.Paid;
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Invoice marked as paid. InvoiceId: {InvoiceId}, PaymentId: {PaymentId}",
            message.InvoiceId,
            message.PaymentId
        );
    }
}
```

**Best Practices:**

1. **Always make consumers idempotent:**
```csharp
// ✅ GOOD: Check current state
if (invoice.Status == InvoiceStatus.Paid)
{
    return; // Already processed
}

// ❌ BAD: Blindly update
invoice.Status = InvoiceStatus.Paid; // May cause issues
```

2. **Use optimistic concurrency:**
```csharp
// Add RowVersion to entity
public byte[] RowVersion { get; set; }

// EF Core will throw DbUpdateConcurrencyException if stale
await _context.SaveChangesAsync();
```

3. **Log duplicate detection:**
```csharp
_logger.LogInformation(
    "Duplicate message detected (idempotent). MessageId: {MessageId}",
    context.MessageId
);
```

**Monitoring:**

```csharp
// Prometheus metrics
inbox_duplicate_count{consumer="ApplyPaymentToInvoiceConsumer"} 5
inbox_processed_count{consumer="ApplyPaymentToInvoiceConsumer"} 1000
```

---

### 3.5. Enhanced Idempotency with Response Replay

**Vấn đề với Idempotency hiện tại:**
- Chỉ check duplicate
- Không cache response
- Client phải xử lý 409 Conflict

**Giải pháp: Response Replay**

```csharp
// File: Payment.API/Domain/Entities/IdempotencyRecord.cs
public class IdempotencyRecord
{
    public string Key { get; set; }              // Unique, max 256 chars
    public Guid PaymentId { get; set; }          // Associated payment
    public DateTime CreatedAt { get; set; }      // Creation timestamp
    public DateTime ExpiresAt { get; set; }      // TTL expiration
    public string? RequestHash { get; set; }     // SHA256 of request payload
    public string Status { get; set; }           // InProgress, Completed, Failed, Expired
    
    // ✅ ADD: Response caching
    public string? ResponseJson { get; set; }    // Cached response
    public int? StatusCode { get; set; }         // HTTP status code
}
```

**Service Implementation:**

```csharp
// File: Payment.API/Application/Services/IdempotencyService.cs
public interface IIdempotencyService
{
    Task<IdempotencyCheckResult> CheckOrCreateAsync(
        string idempotencyKey,
        object requestPayload,
        Guid paymentId,
        TimeSpan ttl);

    // ✅ ADD: Response caching
    Task CacheResponseAsync(
        string idempotencyKey,
        object response,
        int statusCode = 200);
}

public class IdempotencyService : IIdempotencyService
{
    public async Task<IdempotencyCheckResult> CheckOrCreateAsync(
        string idempotencyKey,
        object requestPayload,
        Guid paymentId,
        TimeSpan ttl)
    {
        var requestHash = ComputeRequestHash(requestPayload);

        var existing = await _context.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.Key == idempotencyKey);

        if (existing != null)
        {
            // Check expiration
            if (existing.ExpiresAt < DateTime.UtcNow)
            {
                _context.IdempotencyRecords.Remove(existing);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Validate request hash
                if (existing.RequestHash != requestHash)
                {
                    return new IdempotencyCheckResult(
                        IsNew: false,
                        PaymentId: existing.PaymentId,
                        ConflictReason: "Idempotency key already used with different request payload",
                        CachedResponse: null
                    );
                }

                // ✅ Return cached response
                object? cachedResponse = null;
                if (!string.IsNullOrEmpty(existing.ResponseJson))
                {
                    cachedResponse = JsonSerializer.Deserialize<PaymentInitiationResult>(
                        existing.ResponseJson
                    );
                }

                return new IdempotencyCheckResult(
                    IsNew: false,
                    PaymentId: existing.PaymentId,
                    ConflictReason: null,
                    CachedResponse: cachedResponse,
                    StatusCode: existing.StatusCode
                );
            }
        }

        // Create new record
        var record = new IdempotencyRecord
        {
            Key = idempotencyKey,
            PaymentId = paymentId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(ttl),
            RequestHash = requestHash,
            Status = "InProgress"
        };

        try
        {
            _context.IdempotencyRecords.Add(record);
            await _context.SaveChangesAsync();

            return new IdempotencyCheckResult(
                IsNew: true,
                PaymentId: paymentId,
                ConflictReason: null,
                CachedResponse: null
            );
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Race condition: Another thread created the record
            var raceRecord = await _context.IdempotencyRecords
                .FirstOrDefaultAsync(r => r.Key == idempotencyKey);

            object? cachedResponse = null;
            if (!string.IsNullOrEmpty(raceRecord?.ResponseJson))
            {
                cachedResponse = JsonSerializer.Deserialize<PaymentInitiationResult>(
                    raceRecord.ResponseJson
                );
            }

            return new IdempotencyCheckResult(
                IsNew: false,
                PaymentId: raceRecord!.PaymentId,
                ConflictReason: null,
                CachedResponse: cachedResponse,
                StatusCode: raceRecord.StatusCode
            );
        }
    }

    public async Task CacheResponseAsync(
        string idempotencyKey,
        object response,
        int statusCode = 200)
    {
        var record = await _context.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.Key == idempotencyKey);

        if (record != null)
        {
            record.ResponseJson = JsonSerializer.Serialize(response);
            record.StatusCode = statusCode;
            record.Status = statusCode < 500 ? "Completed" : "Failed";
            // No SaveChangesAsync here when called inside a TransactionBehavior flow.
            // UnitOfWork.CommitAsync persists the cached response with the business changes.
        }
    }
}
```

**Controller Usage:**

```csharp
// File: Payment.API/Controllers/PaymentController.cs
[HttpPost("pay")]
public async Task<IActionResult> InitiatePayment(
    [FromBody] InitiatePaymentRequest request,
    [FromHeader(Name = "X-Idempotency-Key")] string idempotencyKey)
{
    var result = await _mediator.Send(new InitiatePaymentCommand(
        request.InvoiceId,
        request.Amount,
        idempotencyKey
    ));

    if (!result.Accepted)
    {
        // ✅ Return cached response with same status code
        if (result.CachedResponse != null)
        {
            return StatusCode(
                result.StatusCode ?? 200,
                result.CachedResponse
            );
        }

        return Conflict(new { error = result.ErrorReason });
    }

    return Accepted(new
    {
        paymentId = result.PaymentId,
        status = "Processing"
    });
}
```

**Lợi ích:**
- ✅ Client nhận lại exact same response
- ✅ Không cần xử lý 409 Conflict
- ✅ Transparent retry cho client
- ✅ Better user experience

---

### 3.3. Audit Service - Partitioned Hash Chain (Production-Grade)

**Vấn đề với Global Hash Chain:**
- Serializable isolation gây bottleneck nghiêm trọng
- Lock contention cao khi volume lớn (1000+ events/sec)
- Deadlock risk cao
- Throughput giảm mạnh

**Giải pháp: Partitioned Hash Chain**

Thay vì 1 global chain, chia thành nhiều chains độc lập:

```csharp
// File: Audit.API/Domain/Entities/AuditEntry.cs
public class AuditEntry
{
    public Guid Id { get; set; }
    public string EntityType { get; set; }      // "Invoice", "Payment", "User"
    public string EntityId { get; set; }
    public string Action { get; set; }
    
    // ✅ Partition Key - Hash chain per entity type
    public string PartitionKey { get; set; }    // = EntityType
    public long Sequence { get; set; }          // Monotonic per PartitionKey
    
    public string Hash { get; set; }
    public string PreviousHash { get; set; }    // Previous hash IN SAME PARTITION
    
    public DateTime Timestamp { get; set; }
    public string Actor { get; set; }
    public string BeforeJson { get; set; }
    public string AfterJson { get; set; }
    public string CorrelationId { get; set; }
}
```

**Implementation:**

```csharp
// File: Audit.API/Application/Consumers/AuditEventConsumer.cs
public class AuditEventConsumer : IConsumer<IAuditEvent>
{
    private readonly AuditDbContext _db;
    private readonly IHashChainService _hashChainService;
    private readonly ILogger<AuditEventConsumer> _logger;

    public async Task Consume(ConsumeContext<IAuditEvent> context)
    {
        var message = context.Message;

        // ✅ Use ExecutionStrategy for retry on deadlock
        var strategy = _db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            // Use Read Committed with serialized append per partition.
            // Partitioning reduces contention, but the append step still needs a lock/sequence.
            await using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                // 1. Create AuditEntry with PartitionKey
                var entry = new AuditEntry
                {
                    Id = Guid.NewGuid(),
                    EntityType = message.EntityType,
                    EntityId = message.EntityId,
                    PartitionKey = message.EntityType, // Partition by entity type
                    Action = message.Action,
                    Actor = message.Actor,
                    BeforeJson = message.BeforeJson,
                    AfterJson = message.AfterJson,
                    Timestamp = message.Timestamp,
                    CorrelationId = message.CorrelationId
                };

                // 2. Assign Sequence and Hash within the same partition.
                // Do not order by Timestamp; use Sequence for deterministic chain order.
                await _hashChainService.AppendToPartitionAsync(entry, context.CancellationToken);

                // 3. Save AuditEntry
                _db.AuditEntries.Add(entry);
                await _db.SaveChangesAsync(context.CancellationToken);

                // 4. Commit transaction
                await transaction.CommitAsync(context.CancellationToken);

                _logger.LogInformation(
                    "AuditEntry persisted. Id: {Id}, Partition: {Partition}, Sequence: {Sequence}, Hash: {Hash}",
                    entry.Id,
                    entry.PartitionKey,
                    entry.Sequence,
                    entry.Hash
                );
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(context.CancellationToken);
                _logger.LogError(ex, "Failed to persist AuditEntry");
                throw;
            }
        });
    }
}
```

**HashChainService với Partitioning:**

```csharp
// File: Audit.API/Infrastructure/Services/HashChainService.cs
public class HashChainService : IHashChainService
{
    private readonly AuditDbContext _db;

    public async Task AppendToPartitionAsync(
        AuditEntry entry,
        CancellationToken cancellationToken)
    {
        // Lock one chain head row per partition before assigning Sequence.
        // SQL Server implementation can use UPDLOCK/HOLDLOCK on AuditHashChainHeads,
        // or sp_getapplock with resource = $"audit-chain:{entry.PartitionKey}".
        var head = await _db.AuditHashChainHeads
            .SingleAsync(h => h.PartitionKey == entry.PartitionKey, cancellationToken);

        entry.Sequence = head.LastSequence + 1;
        entry.PreviousHash = head.LastHash;

        var dataToHash = $"{entry.EntityType}|{entry.EntityId}|{entry.Action}|" +
                         $"{entry.Actor}|{entry.Timestamp:O}|{entry.BeforeJson}|" +
                         $"{entry.AfterJson}|{entry.PartitionKey}|{entry.Sequence}|" +
                         $"{entry.PreviousHash}";

        var bytes = Encoding.UTF8.GetBytes(dataToHash);
        var hashBytes = SHA256.HashData(bytes);
        entry.Hash = Convert.ToHexString(hashBytes);

        head.LastSequence = entry.Sequence;
        head.LastHash = entry.Hash;
        head.UpdatedAt = DateTime.UtcNow;
    }
}
```

**Lợi ích của Partitioned Chain:**

| Metric | Global Chain | Partitioned Chain |
|--------|--------------|-------------------|
| **Isolation/lock** | Global Serializable | Read Committed + per-partition append lock |
| **Lock Scope** | Entire table/range | One partition head |
| **Throughput** | ~100 req/s | ~1000+ req/s |
| **Deadlock Risk** | High | Low |
| **Scalability** | Poor | Excellent |

**Required indexes/tables:**

```sql
CREATE TABLE AuditHashChainHeads (
    PartitionKey NVARCHAR(200) NOT NULL PRIMARY KEY,
    LastSequence BIGINT NOT NULL,
    LastHash NVARCHAR(128) NULL,
    UpdatedAt DATETIME2 NOT NULL
);

CREATE UNIQUE INDEX UX_AuditEntries_Partition_Sequence
ON AuditEntries (PartitionKey, Sequence);
```

**Verification Query:**

```sql
-- Verify hash chain integrity per partition
WITH ChainValidation AS (
    SELECT 
        a.Id,
        a.PartitionKey,
        a.Hash,
        a.PreviousHash,
        LAG(a.Hash) OVER (PARTITION BY a.PartitionKey ORDER BY a.Sequence) AS ExpectedPreviousHash
    FROM AuditEntries a
)
SELECT 
    PartitionKey,
    COUNT(*) AS TotalEntries,
    SUM(CASE WHEN PreviousHash = ExpectedPreviousHash OR ExpectedPreviousHash IS NULL THEN 0 ELSE 1 END) AS BrokenLinks
FROM ChainValidation
GROUP BY PartitionKey;
```

**Alternative: Async Hash Computation**

Nếu cần throughput cực cao:

```csharp
// Option B: Append-only, compute hash async
public async Task Consume(ConsumeContext<IAuditEvent> context)
{
    var message = context.Message;

    // 1. Append immutable row (no hash yet)
    var entry = new AuditEntry
    {
        Id = Guid.NewGuid(),
        EntityType = message.EntityType,
        EntityId = message.EntityId,
        PartitionKey = message.EntityType,
        Hash = null, // Computed later
        PreviousHash = null,
        // ... other fields
    };

    _db.AuditEntries.Add(entry);
    await _db.SaveChangesAsync(); // Fast append

    // 2. Background worker computes hash chain later
    await _publishEndpoint.Publish(new ComputeHashChainCommand
    {
        AuditEntryId = entry.Id,
        PartitionKey = entry.PartitionKey
    });
}
```

**Recommendation:**
- **< 1000 events/sec**: Partitioned Chain với Read Committed + per-partition append lock/sequence
- **> 1000 events/sec**: Async Hash Computation
- **Never use**: Global Chain với Serializable (bottleneck nghiêm trọng)

---

### 3.6. Saga Pattern với State Machine (Production-Grade)

**Hiện tại đã implement đúng:**
- Payment → Invoice: Eventual Consistency
- Compensation: `PaymentCompensationRequestedEvent`

**Nhưng còn thiếu:**
- ❌ Saga state persistence
- ❌ Timeout handling
- ❌ Missing event detection
- ❌ Compensation retry
- ❌ Poison message handling
- ❌ Dead Letter Queue (DLQ)

**Giải pháp: MassTransit State Machine**

```csharp
// File: Orchestration.API/Domain/Sagas/PaymentSagaState.cs
public class PaymentSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }  // Saga instance ID
    public string CurrentState { get; set; }  // State machine state
    
    // Business data
    public Guid PaymentId { get; set; }
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
    
    // Timeout tracking
    public Guid? TimeoutTokenId { get; set; }
    
    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    
    // Retry tracking
    public int RetryCount { get; set; }
    public string? FailureReason { get; set; }
}
```

**State Machine Definition:**

```csharp
// File: Orchestration.API/Domain/Sagas/PaymentSagaStateMachine.cs
public class PaymentSagaStateMachine : MassTransitStateMachine<PaymentSagaState>
{
    public State PaymentInitiated { get; private set; }
    public State InvoiceValidating { get; private set; }
    public State PaymentCompleting { get; private set; }
    public State Completed { get; private set; }
    public State Failed { get; private set; }
    public State Compensating { get; private set; }

    public Event<IPaymentInitiatedEvent> PaymentInitiated { get; private set; }
    public Event<IInvoiceValidatedEvent> InvoiceValidated { get; private set; }
    public Event<IInvoiceValidationFailedEvent> InvoiceValidationFailed { get; private set; }
    public Event<IPaymentCompletedEvent> PaymentCompleted { get; private set; }
    
    // ✅ Timeout event
    public Event ValidationTimeout { get; private set; }

    public PaymentSagaStateMachine()
    {
        InstanceState(x => x.CurrentState);

        // ✅ Define saga flow
        Initially(
            When(PaymentInitiated)
                .Then(context =>
                {
                    context.Saga.PaymentId = context.Message.PaymentId;
                    context.Saga.InvoiceId = context.Message.InvoiceId;
                    context.Saga.Amount = context.Message.Amount;
                    context.Saga.CreatedAt = DateTime.UtcNow;
                })
                .TransitionTo(InvoiceValidating)
                // ✅ Schedule timeout (5 minutes)
                .Schedule(ValidationTimeout, context => new ValidationTimeoutScheduled
                {
                    PaymentId = context.Message.PaymentId
                }, context => TimeSpan.FromMinutes(5))
        );

        During(InvoiceValidating,
            When(InvoiceValidated)
                .Unschedule(ValidationTimeout) // Cancel timeout
                .TransitionTo(PaymentCompleting)
                .Publish(context => new ConfirmPaymentCommand
                {
                    PaymentId = context.Saga.PaymentId
                }),

            When(InvoiceValidationFailed)
                .Unschedule(ValidationTimeout)
                .Then(context =>
                {
                    context.Saga.FailureReason = context.Message.Reason;
                    context.Saga.FailedAt = DateTime.UtcNow;
                })
                .TransitionTo(Compensating)
                .Publish(context => new PaymentCompensationRequestedEvent
                {
                    PaymentId = context.Saga.PaymentId,
                    Reason = context.Message.Reason
                }),

            // ✅ Handle timeout
            When(ValidationTimeout.Received)
                .Then(context =>
                {
                    context.Saga.FailureReason = "Invoice validation timeout (5 minutes)";
                    context.Saga.FailedAt = DateTime.UtcNow;
                })
                .TransitionTo(Compensating)
                .Publish(context => new PaymentCompensationRequestedEvent
                {
                    PaymentId = context.Saga.PaymentId,
                    Reason = "Validation timeout"
                })
        );

        During(PaymentCompleting,
            When(PaymentCompleted)
                .Then(context =>
                {
                    context.Saga.CompletedAt = DateTime.UtcNow;
                })
                .TransitionTo(Completed)
                .Finalize() // Mark saga as complete
        );

        During(Compensating,
            When(PaymentCompensated)
                .Then(context =>
                {
                    context.Saga.FailedAt = DateTime.UtcNow;
                })
                .TransitionTo(Failed)
                .Finalize()
        );

        SetCompletedWhenFinalized();
    }
}
```

**Saga Repository Configuration:**

```csharp
// File: Orchestration.API/Program.cs
builder.Services.AddMassTransit(x =>
{
    // ✅ Register Saga State Machine
    x.AddSagaStateMachine<PaymentSagaStateMachine, PaymentSagaState>()
        .EntityFrameworkRepository(r =>
        {
            r.ExistingDbContext<OrchestrationDbContext>();
            r.UseSqlServer();
        });

    // ✅ Configure Quartz for timeout scheduling
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.UseMessageScheduler(new Uri("queue:quartz"));
        cfg.ConfigureEndpoints(context);
    });
});

// ✅ Add Quartz scheduler
builder.Services.AddQuartz(q =>
{
    q.UseMicrosoftDependencyInjectionJobFactory();
});

builder.Services.AddQuartzHostedService(options =>
{
    options.WaitForJobsToComplete = true;
});
```

**DbContext Configuration:**

```csharp
// File: Orchestration.API/Infrastructure/Data/OrchestrationDbContext.cs
public class OrchestrationDbContext : DbContext
{
    public DbSet<PaymentSagaState> PaymentSagas { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ✅ Configure Saga State
        modelBuilder.Entity<PaymentSagaState>(entity =>
        {
            entity.HasKey(e => e.CorrelationId);
            entity.Property(e => e.CurrentState).HasMaxLength(64);
            entity.HasIndex(e => e.PaymentId);
            entity.HasIndex(e => e.InvoiceId);
        });
    }
}
```

**Monitoring Saga State:**

```csharp
// File: Orchestration.API/Controllers/SagaController.cs
[ApiController]
[Route("api/v1/sagas")]
public class SagaController : ControllerBase
{
    private readonly OrchestrationDbContext _db;

    [HttpGet("payment/{paymentId}")]
    public async Task<IActionResult> GetPaymentSaga(Guid paymentId)
    {
        var saga = await _db.PaymentSagas
            .FirstOrDefaultAsync(s => s.PaymentId == paymentId);

        if (saga == null)
            return NotFound();

        return Ok(new
        {
            saga.CorrelationId,
            saga.CurrentState,
            saga.PaymentId,
            saga.InvoiceId,
            saga.Amount,
            saga.CreatedAt,
            saga.CompletedAt,
            saga.FailedAt,
            saga.RetryCount,
            saga.FailureReason
        });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var stats = await _db.PaymentSagas
            .GroupBy(s => s.CurrentState)
            .Select(g => new
            {
                State = g.Key,
                Count = g.Count()
            })
            .ToListAsync();

        return Ok(stats);
    }
}
```

**Lợi ích:**
- ✅ Saga state persisted in DB (survives restarts)
- ✅ Automatic timeout handling
- ✅ Compensation retry built-in
- ✅ Visual state tracking
- ✅ Production-ready orchestration

---

### 3.7. Transaction Boundary & Aggregate Design

**Vấn đề hiện tại:**
Design đang thiên về **service transaction** thay vì **aggregate transaction**

**DDD Production Best Practice:**
```
1 Transaction = 1 Aggregate Root
```

**Ví dụ:**

```csharp
// ✅ GOOD: Transaction boundary = Aggregate boundary
public async Task<Result> ProcessPayment(Guid paymentId)
{
    await using var tx = await _context.Database.BeginTransactionAsync();
    
    // Only modify Payment aggregate
    var payment = await _context.Payments.FindAsync(paymentId);
    payment.MarkAsCompleted(); // Domain method
    
    await _context.SaveChangesAsync();
    await tx.CommitAsync();
    
    // Publish event for other aggregates
    await _publishEndpoint.Publish(new PaymentCompletedEvent { ... });
}

// ❌ DANGEROUS: Transaction spans multiple aggregates
public async Task<Result> ProcessPayment(Guid paymentId)
{
    await using var tx = await _context.Database.BeginTransactionAsync();
    
    // Modifying multiple aggregates in same transaction
    var payment = await _context.Payments.FindAsync(paymentId);
    var invoice = await _context.Invoices.FindAsync(payment.InvoiceId);
    var user = await _context.Users.FindAsync(invoice.UserId);
    
    payment.MarkAsCompleted();
    invoice.MarkAsPaid();
    user.UpdateBalance(-invoice.Amount);
    
    await _context.SaveChangesAsync(); // Aggregate coupling!
    await tx.CommitAsync();
}
```

**Aggregate Boundaries trong Bizcore ERP:**

```
┌─────────────────────────────────────────────────────────────────┐
│                    Aggregate Boundaries                         │
└─────────────────────────────────────────────────────────────────┘

Payment Aggregate
├─ Payment (Root)
├─ PaymentMethod
└─ PaymentHistory

Invoice Aggregate
├─ Invoice (Root)
├─ InvoiceLineItem
└─ InvoiceHistory

User Aggregate
├─ User (Root)
├─ UserRole
└─ UserPermission

Audit Aggregate
├─ AuditEntry (Root)
└─ (No children - immutable)
```

**Transaction Rules:**

1. **Local Transaction**: Chỉ modify 1 aggregate
```csharp
// ✅ GOOD
await using var tx = await _context.Database.BeginTransactionAsync();
payment.MarkAsCompleted();
payment.AddHistory("Completed");
await _context.SaveChangesAsync();
await tx.CommitAsync();
```

2. **Saga**: Coordinate across aggregates
```csharp
// ✅ GOOD
// Service A: Update Payment aggregate
payment.MarkAsCompleted();
await _context.SaveChangesAsync();
await _publishEndpoint.Publish(new PaymentCompletedEvent { ... });

// Service B: Update Invoice aggregate (separate transaction)
invoice.MarkAsPaid();
await _context.SaveChangesAsync();
```

3. **Avoid**: Cross-aggregate transactions
```csharp
// ❌ BAD: Aggregate coupling
await using var tx = await _context.Database.BeginTransactionAsync();
payment.MarkAsCompleted();
invoice.MarkAsPaid(); // Different aggregate!
await _context.SaveChangesAsync();
await tx.CommitAsync();
```

**Refactoring Guide:**

```csharp
// BEFORE (Coupled)
public async Task ProcessPaymentAndInvoice(Guid paymentId)
{
    await using var tx = await _context.Database.BeginTransactionAsync();
    
    var payment = await _context.Payments.FindAsync(paymentId);
    var invoice = await _context.Invoices.FindAsync(payment.InvoiceId);
    
    payment.Status = PaymentStatus.Completed;
    invoice.Status = InvoiceStatus.Paid;
    
    await _context.SaveChangesAsync();
    await tx.CommitAsync();
}

// AFTER (Decoupled)
public async Task ProcessPayment(Guid paymentId)
{
    await using var tx = await _context.Database.BeginTransactionAsync();
    
    var payment = await _context.Payments.FindAsync(paymentId);
    payment.Status = PaymentStatus.Completed;
    
    await _publishEndpoint.Publish(new PaymentCompletedEvent
    {
        PaymentId = payment.Id,
        InvoiceId = payment.InvoiceId
    });
    
    await _context.SaveChangesAsync();
    await tx.CommitAsync();
}

// Separate consumer in Invoice Service
public class ApplyPaymentConsumer : IConsumer<IPaymentCompletedEvent>
{
    public async Task Consume(ConsumeContext<IPaymentCompletedEvent> context)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        
        var invoice = await _context.Invoices.FindAsync(context.Message.InvoiceId);
        invoice.Status = InvoiceStatus.Paid;
        
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
```

**Lợi ích:**
- ✅ Loose coupling giữa aggregates
- ✅ Better scalability (mỗi aggregate có thể scale độc lập)
- ✅ Easier testing (test từng aggregate riêng)
- ✅ Clear boundaries (dễ hiểu, dễ maintain)

---

## 📋 4. Checklist Áp dụng

### 4.1. Payment Service

- [ ] **Enable MassTransit Outbox + Inbox**
- [ ] **Add MediatR Transaction Pipeline**
- [ ] **InitiatePaymentAsync**: Publish event BÊN TRONG transaction
- [ ] **Enhanced Idempotency**: Add response caching
- [ ] **ConfirmPaymentConsumer**: Idempotent check
- [ ] **RejectPaymentConsumer**: Idempotent check
- [ ] **PaymentCompensationRequestedConsumer**: Idempotent check
- [ ] **Add optimistic concurrency** (RowVersion)
- [ ] Add migration cho Outbox/Inbox tables

### 4.2. Invoice Service

- [ ] **Enable MassTransit Outbox + Inbox**
- [ ] **Add MediatR Transaction Pipeline**
- [ ] **CreateInvoiceAsync**: Publish event BÊN TRONG transaction
- [ ] **UpdateStatusAsync**: Publish event BÊN TRONG transaction
- [ ] **ApplyPaymentToInvoiceConsumer**: Idempotent check
- [ ] **RestoreFieldAsync**: Transaction + Audit event
- [ ] **Add optimistic concurrency** (RowVersion already exists)
- [ ] Add migration cho Outbox/Inbox tables

### 4.3. Audit Service

- [ ] **AuditEventConsumer**: Partitioned Hash Chain với Read Committed
- [ ] **MarkAsReversedAsync**: Transaction
- [ ] **Add PartitionKey** to AuditEntry
- [ ] **Update HashChainService** for partitioning
- [ ] Add ExecutionStrategy cho retry logic
- [ ] Add migration cho PartitionKey

### 4.4. Orchestration Service

- [ ] **Add MassTransit State Machine** (PaymentSaga)
- [ ] **Add Quartz Scheduler** for timeouts
- [ ] **ProcessOrchestrationService**: Transaction
- [ ] **Add Saga monitoring endpoints**
- [ ] Add migration cho Saga state tables

### 4.5. Identity Service

- [ ] **DbSeeder**: Transaction
- [ ] **Add optimistic concurrency** (RowVersion)

### 4.6. Report Service

- [ ] **Consumers**: Transaction nếu cần

### 4.7. Cross-Cutting Concerns

- [ ] **Add MediatR** to all services
- [ ] **Add TransactionBehavior** pipeline
- [ ] **Add ValidationBehavior** pipeline
- [ ] **Add LoggingBehavior** pipeline
- [ ] **Configure OpenTelemetry** for distributed tracing
- [ ] **Add Prometheus metrics** for transactions
- [ ] **Add Grafana dashboards**
- [ ] **Configure Dead Letter Queue** (DLQ)

---

## 🧪 5. Testing Strategy

### 5.1. Unit Tests

```csharp
[Fact]
public async Task InitiatePayment_TransactionRollback_OnPublishFailure()
{
    // Arrange
    var mockPublisher = new Mock<IPublishEndpoint>();
    mockPublisher
        .Setup(x => x.Publish(It.IsAny<IPaymentInitiatedEvent>(), default))
        .ThrowsAsync(new Exception("RabbitMQ down"));
    
    var service = new PaymentService(_context, _idempotencyService, mockPublisher.Object);
    
    // Act & Assert
    await Assert.ThrowsAsync<Exception>(() => 
        service.InitiatePaymentAsync(payment, "key-123")
    );
    
    // Verify: Payment NOT saved
    var savedPayment = await _context.Payments.FindAsync(payment.Id);
    savedPayment.Should().BeNull();
    
    // Verify: IdempotencyRecord NOT saved
    var idempotencyRecord = await _context.IdempotencyRecords.FindAsync("key-123");
    idempotencyRecord.Should().BeNull();
}

[Fact]
public async Task InitiatePayment_DuplicateRequest_ReturnsC achedResponse()
{
    // Arrange
    var key = "test-key-001";
    
    // First request
    var result1 = await _service.InitiatePaymentAsync(payment, key);
    result1.Accepted.Should().BeTrue();
    
    // Second request (duplicate)
    var result2 = await _service.InitiatePaymentAsync(payment, key);
    
    // Assert: Same response
    result2.PaymentId.Should().Be(result1.PaymentId);
    result2.CachedResponse.Should().NotBeNull();
    
    // Verify: Only 1 payment created
    var payments = await _context.Payments.Where(p => p.IdempotencyKey == key).ToListAsync();
    payments.Should().HaveCount(1);
}
```

### 5.2. Integration Tests

```csharp
[Fact]
public async Task CreateInvoice_OutboxPattern_EventDeliveredAfterCommit()
{
    // Arrange
    var harness = _provider.GetRequiredService<ITestHarness>();
    await harness.Start();
    
    // Act
    var invoice = await _invoiceService.CreateInvoiceAsync("Alice", 1000m);
    
    // Assert: Invoice saved
    var savedInvoice = await _context.Invoices.FindAsync(invoice.Id);
    savedInvoice.Should().NotBeNull();
    
    // Assert: Event published (via Outbox)
    var published = await harness.Published.Any<IInvoiceCreatedEvent>(
        x => x.Context.Message.Id == invoice.Id
    );
    published.Should().BeTrue();
    
    await harness.Stop();
}

[Fact]
public async Task Consumer_InboxPattern_DuplicateMessageIgnored()
{
    // Arrange
    var harness = _provider.GetRequiredService<ITestHarness>();
    await harness.Start();
    
    var message = new PaymentCompletedEvent
    {
        PaymentId = Guid.NewGuid(),
        InvoiceId = Guid.NewGuid()
    };
    
    // Act: Send message twice
    await harness.Bus.Publish(message);
    await Task.Delay(100); // Wait for processing
    await harness.Bus.Publish(message); // Duplicate
    await Task.Delay(100);
    
    // Assert: Consumer processed only once
    var consumed = harness.Consumed.Select<IPaymentCompletedEvent>().Count();
    consumed.Should().Be(2); // Received twice
    
    // But business logic executed only once (check via side effects)
    var invoice = await _context.Invoices.FindAsync(message.InvoiceId);
    invoice.Status.Should().Be(InvoiceStatus.Paid);
    
    await harness.Stop();
}
```

### 5.3. Concurrency Tests

```csharp
[Fact]
public async Task AuditConsumer_ConcurrentRequests_HashChainIntact()
{
    // Arrange
    var events = Enumerable.Range(0, 100)
        .Select(i => new AuditEvent
        {
            EntityType = "Invoice",
            EntityId = Guid.NewGuid().ToString(),
            Action = "Create",
            Actor = $"User{i}"
        })
        .ToList();
    
    // Act: Consume concurrently
    var tasks = events.Select(e => _consumer.Consume(CreateContext(e)));
    await Task.WhenAll(tasks);
    
    // Assert: Hash chain valid per partition
    var partitions = await _db.AuditEntries
        .Select(e => e.PartitionKey)
        .Distinct()
        .ToListAsync();
    
    foreach (var partition in partitions)
    {
        var entries = await _db.AuditEntries
            .Where(e => e.PartitionKey == partition)
            .OrderBy(e => e.Sequence)
            .ToListAsync();
        
        for (int i = 1; i < entries.Count; i++)
        {
            var current = entries[i];
            var previous = entries[i - 1];
            
            // Verify: PreviousHash matches
            current.PreviousHash.Should().Be(previous.Hash);
        }
    }
}

[Fact]
public async Task Saga_Timeout_TriggersCompensation()
{
    // Arrange
    var harness = _provider.GetRequiredService<ITestHarness>();
    await harness.Start();
    
    var sagaHarness = harness.GetSagaStateMachineHarness<PaymentSagaStateMachine, PaymentSagaState>();
    
    // Act: Initiate payment but don't validate invoice
    await harness.Bus.Publish(new PaymentInitiatedEvent
    {
        PaymentId = Guid.NewGuid(),
        InvoiceId = Guid.NewGuid(),
        Amount = 1000m
    });
    
    // Wait for timeout (5 minutes in production, 5 seconds in test)
    await Task.Delay(TimeSpan.FromSeconds(6));
    
    // Assert: Saga transitioned to Compensating
    var saga = sagaHarness.Created.Select().First();
    saga.CurrentState.Should().Be("Compensating");
    
    // Assert: Compensation event published
    var compensationPublished = await harness.Published.Any<IPaymentCompensationRequestedEvent>();
    compensationPublished.Should().BeTrue();
    
    await harness.Stop();
}
```

---

## 📊 6. Monitoring & Observability

### 6.1. Prometheus Metrics

```csharp
// File: BuildingBlocks/Bizcore.BuildingBlocks/Metrics/TransactionMetrics.cs
using Prometheus;

public static class TransactionMetrics
{
    // Transaction duration
    public static readonly Histogram TransactionDuration = Metrics.CreateHistogram(
        "transaction_duration_seconds",
        "Duration of database transactions",
        new HistogramConfiguration
        {
            LabelNames = new[] { "service", "operation", "status" },
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 10)
        }
    );

    // Transaction counter
    public static readonly Counter TransactionTotal = Metrics.CreateCounter(
        "transaction_total",
        "Total number of transactions",
        new CounterConfiguration
        {
            LabelNames = new[] { "service", "operation", "status" }
        }
    );

    // Outbox metrics
    public static readonly Gauge OutboxPendingCount = Metrics.CreateGauge(
        "outbox_pending_count",
        "Number of pending messages in outbox",
        new GaugeConfiguration
        {
            LabelNames = new[] { "service" }
        }
    );

    public static readonly Counter OutboxDeliveredTotal = Metrics.CreateCounter(
        "outbox_delivered_total",
        "Total number of messages delivered from outbox",
        new CounterConfiguration
        {
            LabelNames = new[] { "service", "status" }
        }
    );

    // Inbox metrics
    public static readonly Counter InboxDuplicateCount = Metrics.CreateCounter(
        "inbox_duplicate_count",
        "Number of duplicate messages detected",
        new CounterConfiguration
        {
            LabelNames = new[] { "service", "consumer" }
        }
    );

    // Saga metrics
    public static readonly Gauge SagaActiveCount = Metrics.CreateGauge(
        "saga_active_count",
        "Number of active sagas",
        new GaugeConfiguration
        {
            LabelNames = new[] { "saga_type", "state" }
        }
    );

    public static readonly Counter SagaTimeoutCount = Metrics.CreateCounter(
        "saga_timeout_count",
        "Number of saga timeouts",
        new CounterConfiguration
        {
            LabelNames = new[] { "saga_type" }
        }
    );

    public static readonly Counter CompensationCount = Metrics.CreateCounter(
        "compensation_count",
        "Number of compensations triggered",
        new CounterConfiguration
        {
            LabelNames = new[] { "service", "reason" }
        }
    );

    // DLQ metrics
    public static readonly Counter DlqMessageCount = Metrics.CreateCounter(
        "dlq_message_count",
        "Number of messages sent to dead letter queue",
        new CounterConfiguration
        {
            LabelNames = new[] { "service", "consumer", "reason" }
        }
    );
}
```

**Usage in TransactionBehavior:**

```csharp
public async Task<TResponse> Handle(...)
{
    var stopwatch = Stopwatch.StartNew();
    var serviceName = "payment"; // From config
    var operationName = typeof(TRequest).Name;

    try
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        var response = await next();
        await tx.CommitAsync();

        // ✅ Record success metrics
        TransactionMetrics.TransactionTotal
            .WithLabels(serviceName, operationName, "committed")
            .Inc();

        TransactionMetrics.TransactionDuration
            .WithLabels(serviceName, operationName, "committed")
            .Observe(stopwatch.Elapsed.TotalSeconds);

        return response;
    }
    catch (Exception ex)
    {
        // ✅ Record failure metrics
        TransactionMetrics.TransactionTotal
            .WithLabels(serviceName, operationName, "rolled_back")
            .Inc();

        TransactionMetrics.TransactionDuration
            .WithLabels(serviceName, operationName, "rolled_back")
            .Observe(stopwatch.Elapsed.TotalSeconds);

        throw;
    }
}
```

### 6.2. OpenTelemetry Integration

```csharp
// File: Payment.API/Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.SetDbStatementForText = true;
                options.SetDbStatementForStoredProcedure = true;
            })
            .AddMassTransitInstrumentation()
            .AddSource("Payment.API")
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://tempo:4317");
            });
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter();
    });
```

### 6.3. Grafana Dashboards

**Transaction Dashboard:**

```promql
# Transaction success rate
rate(transaction_total{status="committed"}[5m]) 
/ 
rate(transaction_total[5m])

# Transaction duration p95
histogram_quantile(0.95, 
  rate(transaction_duration_seconds_bucket[5m])
)

# Transaction errors
rate(transaction_total{status="rolled_back"}[5m])

# Outbox pending messages
outbox_pending_count

# Inbox duplicate rate
rate(inbox_duplicate_count[5m])

# Saga timeout rate
rate(saga_timeout_count[5m])

# Compensation rate
rate(compensation_count[5m])

# DLQ message rate
rate(dlq_message_count[5m])
```

### 6.4. Alerts

```yaml
# File: prometheus-alerts.yml
groups:
  - name: transaction_alerts
    rules:
      - alert: HighTransactionFailureRate
        expr: rate(transaction_total{status="rolled_back"}[5m]) > 0.05
        for: 5m
        annotations:
          summary: "High transaction failure rate (> 5%)"
          
      - alert: OutboxBacklog
        expr: outbox_pending_count > 1000
        for: 10m
        annotations:
          summary: "Outbox has > 1000 pending messages"
          
      - alert: HighInboxDuplicateRate
        expr: rate(inbox_duplicate_count[5m]) > 0.1
        for: 5m
        annotations:
          summary: "High duplicate message rate (> 10%)"
          
      - alert: SagaTimeoutSpike
        expr: rate(saga_timeout_count[5m]) > 0.01
        for: 5m
        annotations:
          summary: "Saga timeout rate increased"
          
      - alert: DLQMessagesDetected
        expr: rate(dlq_message_count[5m]) > 0
        for: 1m
        annotations:
          summary: "Messages being sent to DLQ"
```

---

## 📚 7. Best Practices Summary

### 5.1. Unit Tests

```csharp
[Fact]
public async Task InitiatePayment_TransactionRollback_OnPublishFailure()
{
    // Arrange
    var mockPublisher = new Mock<IPublishEndpoint>();
    mockPublisher
        .Setup(x => x.Publish(It.IsAny<IPaymentInitiatedEvent>(), default))
        .ThrowsAsync(new Exception("RabbitMQ down"));
    
    var service = new PaymentService(_context, _idempotencyService, mockPublisher.Object);
    
    // Act & Assert
    await Assert.ThrowsAsync<Exception>(() => 
        service.InitiatePaymentAsync(payment, "key-123")
    );
    
    // Verify: Payment NOT saved
    var savedPayment = await _context.Payments.FindAsync(payment.Id);
    savedPayment.Should().BeNull();
    
    // Verify: IdempotencyRecord NOT saved
    var idempotencyRecord = await _context.IdempotencyRecords.FindAsync("key-123");
    idempotencyRecord.Should().BeNull();
}
```

### 5.2. Integration Tests

```csharp
[Fact]
public async Task CreateInvoice_OutboxPattern_EventDeliveredAfterCommit()
{
    // Arrange
    var harness = _provider.GetRequiredService<ITestHarness>();
    await harness.Start();
    
    // Act
    var invoice = await _invoiceService.CreateInvoiceAsync("Alice", 1000m);
    
    // Assert: Invoice saved
    var savedInvoice = await _context.Invoices.FindAsync(invoice.Id);
    savedInvoice.Should().NotBeNull();
    
    // Assert: Event published (via Outbox)
    var published = await harness.Published.Any<IInvoiceCreatedEvent>(
        x => x.Context.Message.Id == invoice.Id
    );
    published.Should().BeTrue();
    
    await harness.Stop();
}
```

### 5.3. Concurrency Tests

```csharp
[Fact]
public async Task AuditConsumer_ConcurrentRequests_HashChainIntact()
{
    // Arrange
    var events = Enumerable.Range(0, 10)
        .Select(i => new AuditEvent
        {
            EntityType = "Invoice",
            EntityId = Guid.NewGuid().ToString(),
            Action = "Create",
            Actor = $"User{i}"
        })
        .ToList();
    
    // Act: Consume concurrently
    var tasks = events.Select(e => _consumer.Consume(CreateContext(e)));
    await Task.WhenAll(tasks);
    
    // Assert: Hash chain valid
    var entries = await _db.AuditEntries
        .OrderBy(x => x.PartitionKey)
        .ThenBy(x => x.Sequence)
        .ToListAsync();
    
    for (int i = 1; i < entries.Count; i++)
    {
        var current = entries[i];
        var previous = entries[i - 1];
        
        // Verify: PreviousHash matches
        current.PreviousHash.Should().Be(previous.Hash);
        
        // Verify: Hash computed correctly
        var expectedHash = ComputeHash(current, previous.Hash);
        current.Hash.Should().Be(expectedHash);
    }
}
```

---

## 📊 6. Performance Considerations

### 6.1. Transaction Overhead

| Pattern | Latency | Throughput | Use Case |
|---------|---------|------------|----------|
| No Transaction | ~5ms | High | ❌ Unsafe |
| Read Committed | ~10ms | High | ✅ Most cases |
| Partitioned audit append | ~15-30ms | Medium | ✅ Audit hash chain |
| Serializable | ~50ms+ | Low | ⚠️ Temporary fallback per partition |
| Outbox | ~15ms | Medium | ✅ Event publishing |

### 6.2. Optimization Tips

**1. Keep transactions short:**
```csharp
// ❌ BAD: Long transaction
await using var tx = await _context.Database.BeginTransactionAsync();
await ExpensiveExternalApiCall();  // Network I/O trong transaction
await _context.SaveChangesAsync();
await tx.CommitAsync();

// ✅ GOOD: Short transaction
var data = await ExpensiveExternalApiCall();  // Gọi trước
await using var tx = await _context.Database.BeginTransactionAsync();
_context.Add(data);
await _context.SaveChangesAsync();
await tx.CommitAsync();
```

**2. Batch operations:**
```csharp
// ✅ GOOD: Batch insert trong 1 transaction
await using var tx = await _context.Database.BeginTransactionAsync();
_context.AuditEntries.AddRange(entries);  // Bulk insert
await _context.SaveChangesAsync();
await tx.CommitAsync();
```

**3. Use appropriate isolation/concurrency control:**
```csharp
// Most cases: Read Committed (default)
await using var tx = await _context.Database.BeginTransactionAsync();

// Audit hash chain: serialize append per partition.
// Use a ChainHead row lock, application lock, or optimistic retry on (PartitionKey, Sequence).
```

---

## 🚨 7. Error Handling & Monitoring

### 7.1. Retry Strategy

```csharp
// ✅ Sử dụng ExecutionStrategy cho transient errors
var strategy = _context.Database.CreateExecutionStrategy();

await strategy.ExecuteAsync(async () =>
{
    await using var tx = await _context.Database.BeginTransactionAsync();
    
    try
    {
        // Business logic
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
    }
    catch
    {
        await tx.RollbackAsync();
        throw;
    }
});
```

**ExecutionStrategy tự động retry cho:**
- Deadlock
- Connection timeout
- Transient network errors

### 7.2. Logging

```csharp
try
{
    await using var tx = await _context.Database.BeginTransactionAsync();
    
    _logger.LogDebug("Transaction started: {TransactionId}", tx.TransactionId);
    
    // Business logic
    
    await tx.CommitAsync();
    _logger.LogInformation("Transaction committed: {TransactionId}", tx.TransactionId);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Transaction failed and rolled back: {TransactionId}", tx.TransactionId);
    throw;
}
```

### 7.3. Metrics

```csharp
// Prometheus metrics
transaction_duration_seconds{service="payment",status="committed"}
transaction_duration_seconds{service="payment",status="rolled_back"}
transaction_errors_total{service="payment",error_type="deadlock"}
```

---

## 📚 8. Best Practices Summary

### ✅ DO

1. **Wrap multiple DB operations trong transaction**
2. **Sử dụng Outbox Pattern cho event publishing**
3. **Publish events SAU KHI transaction commit**
4. **Sử dụng partitioned append có khóa/sequence cho Audit hash chain**
5. **Keep transactions short và focused**
6. **Sử dụng ExecutionStrategy cho retry**
7. **Log transaction lifecycle**
8. **Test concurrency scenarios**

### ❌ DON'T

1. **Publish events TRƯỚC KHI transaction commit**
2. **Gọi external APIs trong transaction**
3. **Giữ transaction open quá lâu**
4. **Ignore transaction errors**
5. **Sử dụng Serializable rộng cho mọi transaction hoặc global Audit chain**
6. **Nest transactions (SQL Server không support)**
7. **Forget to rollback on error**

---

## 🎯 9. Migration Plan

### Phase 1: Critical Services (Week 1)
1. ✅ Payment Service - InitiatePaymentAsync
2. ✅ Audit Service - AuditEventConsumer
3. ✅ Enable Outbox cho Payment và Invoice

### Phase 2: Core Services (Week 2)
4. ✅ Invoice Service - CreateInvoiceAsync
5. ✅ Invoice Service - RestoreFieldAsync
6. ✅ Payment Consumers (Confirm, Reject, Compensation)

### Phase 3: Supporting Services (Week 3)
7. ✅ Identity Service - DbSeeder
8. ✅ Orchestration Service - ProcessOrchestrationService
9. ✅ Report Service - Consumers

### Phase 4: Testing & Monitoring (Week 4)
10. ✅ Integration tests
11. ✅ Concurrency tests
12. ✅ Performance benchmarks
13. ✅ Monitoring dashboards

---

## 📖 10. References

- [EF Core Transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions)
- [MassTransit Outbox](https://masstransit.io/documentation/patterns/outbox)
- [SQL Server Isolation Levels](https://learn.microsoft.com/en-us/sql/t-sql/statements/set-transaction-isolation-level-transact-sql)
- [Saga Pattern](https://microservices.io/patterns/data/saga.html)

---

*Document này là Single Source of Truth cho Transaction Management trong Bizcore ERP.*
*Cập nhật lần cuối: 07/05/2026*


---

## 🎓 11. Key Takeaways (Production Lessons)

### 1. Outbox Pattern - Publish BÊN TRONG Transaction
**❌ Sai lầm phổ biến:**
```csharp
await tx.CommitAsync();
await _publishEndpoint.Publish(event); // Process crash → event lost!
```

**✅ Đúng:**
```csharp
await _publishEndpoint.Publish(event); // Saved to Outbox
await _context.SaveChangesAsync();
await tx.CommitAsync();
// MassTransit delivers from Outbox async
```

### 2. Hash Chain - Partition thay vì Serializable
**❌ Bottleneck:**
- Global chain + Serializable = ~100 req/s

**✅ Scalable:**
- Partitioned chain + Read Committed = ~1000+ req/s

### 3. Transaction Boundary = Aggregate Boundary
**❌ Coupling:**
```csharp
payment.MarkAsCompleted();
invoice.MarkAsPaid(); // Different aggregate!
```

**✅ Decoupled:**
```csharp
payment.MarkAsCompleted();
await _publishEndpoint.Publish(new PaymentCompletedEvent { ... });
// Invoice Service handles in separate transaction
```

### 4. Idempotency - Cache Response
**❌ Basic:**
- Check duplicate → Return 409 Conflict

**✅ Production:**
- Check duplicate → Return cached response (same status code)

### 5. Consumers - Always Idempotent
**❌ Dangerous:**
```csharp
invoice.Status = InvoiceStatus.Paid; // Blindly update
```

**✅ Safe:**
```csharp
if (invoice.Status == InvoiceStatus.Paid) return; // Already processed
invoice.Status = InvoiceStatus.Paid;
```

### 6. Saga - State Machine + Timeout
**❌ Basic:**
- Event-driven compensation

**✅ Production:**
- State Machine + Timeout + Retry + DLQ

### 7. Monitoring - Everything
**❌ Blind:**
- No metrics, no alerts

**✅ Observable:**
- Transaction metrics
- Outbox/Inbox metrics
- Saga metrics
- Distributed tracing
- Alerts

---

*Cập nhật với Production Best Practices từ feedback - 07/05/2026*
