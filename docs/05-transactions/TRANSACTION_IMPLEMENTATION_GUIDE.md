# Transaction Implementation Guide - Step by Step

## 🎯 Mục đích

Hướng dẫn chi tiết từng bước để implement Transaction Management vào các services của Bizcore ERP.

---

## 📦 1. Payment Service Implementation

### 1.1. Enable MassTransit Outbox

#### Step 1: Update DbContext

```csharp
// File: src/Services/Payment/Payment.API/Infrastructure/Data/PaymentDbContext.cs

using MassTransit;
using Microsoft.EntityFrameworkCore;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<Payment> Payments { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Payment entity configuration
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.IdempotencyKey).HasMaxLength(256);
            entity.HasIndex(e => e.IdempotencyKey).IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL");
            entity.HasIndex(e => e.InvoiceId);
        });

        // IdempotencyRecord configuration
        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(256);
            entity.Property(e => e.RequestHash).HasMaxLength(64);
            entity.Property(e => e.Status).HasMaxLength(32).IsRequired();
            entity.Property(e => e.ResponseJson).HasColumnType("nvarchar(max)");
            entity.HasIndex(e => e.ExpiresAt);
            entity.HasIndex(e => e.PaymentId);
        });

        // ✅ ADD: MassTransit Outbox tables
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
    }
}
```

#### Step 2: Create Migration

```bash
cd src/Services/Payment/Payment.API
dotnet ef migrations add AddMassTransitOutbox
dotnet ef database update
```

#### Step 3: Configure Outbox in Program.cs

```csharp
// File: src/Services/Payment/Payment.API/Program.cs

// Find the MassTransit configuration section and update:

builder.Services.AddMassTransit(x =>
{
    // Register consumers
    x.AddConsumer<ConfirmPaymentConsumer>();
    x.AddConsumer<RejectPaymentConsumer>();
    x.AddConsumer<PaymentCompensationRequestedConsumer>();
    x.AddConsumer<InvoiceCreatedConsumer>();

    // ✅ ADD: Configure Entity Framework Outbox
    x.AddEntityFrameworkOutbox<PaymentDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();

        // Outbox delivery settings
        o.QueryDelay = TimeSpan.FromSeconds(1);
        o.MessageDeliveryLimit = 3;
        o.MessageDeliveryTimeout = TimeSpan.FromMinutes(5);
        
        // Cleanup old messages
        o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.ConfigureEndpoints(context);
    });
});
```

### 1.2. Update PaymentService with Transaction

```csharp
// File: src/Services/Payment/Payment.API/Application/Services/PaymentService.cs

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

public class PaymentService : IPaymentService
{
    private readonly PaymentDbContext _context;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        PaymentDbContext context,
        IIdempotencyService idempotencyService,
        IPublishEndpoint publishEndpoint,
        ILogger<PaymentService> logger)
    {
        _context = context;
        _idempotencyService = idempotencyService;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<PaymentInitiationResult> InitiatePaymentAsync(
        Payment payment,
        string idempotencyKey)
    {
        // ✅ Use ExecutionStrategy for retry on transient errors
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            // ✅ Begin transaction
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogDebug(
                    "Transaction started for payment initiation. TransactionId: {TransactionId}, IdempotencyKey: {IdempotencyKey}",
                    transaction.TransactionId,
                    idempotencyKey
                );

                // 1. Check/Create Idempotency Record
                var idempotencyResult = await _idempotencyService.CheckOrCreateAsync(
                    idempotencyKey,
                    new { payment.InvoiceId, payment.Amount },
                    payment.Id,
                    TimeSpan.FromMinutes(30)
                );

                if (!idempotencyResult.IsNew)
                {
                    // Duplicate request detected
                    await transaction.RollbackAsync();

                    _logger.LogInformation(
                        "Duplicate payment request detected. IdempotencyKey: {IdempotencyKey}, ExistingPaymentId: {PaymentId}",
                        idempotencyKey,
                        idempotencyResult.PaymentId
                    );

                    return new PaymentInitiationResult(
                        Accepted: false,
                        PaymentId: idempotencyResult.PaymentId,
                        ErrorReason: idempotencyResult.ConflictReason
                    );
                }

                // 2. Create Payment entity
                payment.Status = PaymentStatus.Processing;
                payment.IdempotencyKey = idempotencyKey;
                _context.Payments.Add(payment);

                // 3. Publish event (will be saved to Outbox table)
                await _publishEndpoint.Publish(new PaymentInitiatedEvent
                {
                    PaymentId = payment.Id,
                    InvoiceId = payment.InvoiceId,
                    Amount = payment.Amount,
                    InitiatedAt = DateTime.UtcNow
                });

                // 4. Save changes (commits Payment + IdempotencyRecord + OutboxMessage)
                await _context.SaveChangesAsync();

                // 5. Commit transaction
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Payment initiated successfully. PaymentId: {PaymentId}, TransactionId: {TransactionId}",
                    payment.Id,
                    transaction.TransactionId
                );

                return new PaymentInitiationResult(
                    Accepted: true,
                    PaymentId: payment.Id,
                    ErrorReason: null
                );
            }
            catch (Exception ex)
            {
                // Rollback on any error
                await transaction.RollbackAsync();

                _logger.LogError(
                    ex,
                    "Failed to initiate payment. Transaction rolled back. IdempotencyKey: {IdempotencyKey}",
                    idempotencyKey
                );

                throw;
            }
        });
    }
}
```

### 1.3. Update Payment Consumers with Transaction

```csharp
// File: src/Services/Payment/Payment.API/Application/Consumers/ConfirmPaymentConsumer.cs

public class ConfirmPaymentConsumer : IConsumer<IConfirmPaymentCommand>
{
    private readonly PaymentDbContext _context;
    private readonly ILogger<ConfirmPaymentConsumer> _logger;

    public ConfirmPaymentConsumer(
        PaymentDbContext context,
        ILogger<ConfirmPaymentConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IConfirmPaymentCommand> context)
    {
        var cmd = context.Message;

        // ✅ Use ExecutionStrategy
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            // ✅ Begin transaction
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var payment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.Id == cmd.PaymentId);

                if (payment is null)
                {
                    _logger.LogWarning(
                        "Payment not found for confirmation. PaymentId: {PaymentId}",
                        cmd.PaymentId
                    );
                    return;
                }

                // Idempotency check
                if (payment.Status == PaymentStatus.Completed)
                {
                    _logger.LogInformation(
                        "Payment already completed. PaymentId: {PaymentId}",
                        cmd.PaymentId
                    );
                    return;
                }

                // Update status
                payment.Status = PaymentStatus.Completed;
                await _context.SaveChangesAsync();

                // Commit transaction
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Payment confirmed successfully. PaymentId: {PaymentId}",
                    cmd.PaymentId
                );
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(
                    ex,
                    "Failed to confirm payment. PaymentId: {PaymentId}",
                    cmd.PaymentId
                );
                throw;
            }
        });
    }
}
```

---

## 📦 2. Invoice Service Implementation

### 2.1. Enable MassTransit Outbox

#### Step 1: Update DbContext

```csharp
// File: src/Services/Invoice/Invoice.API/Infrastructure/Data/InvoiceDbContext.cs

using MassTransit;

public class InvoiceDbContext : DbContext
{
    public InvoiceDbContext(DbContextOptions<InvoiceDbContext> options) : base(options) { }

    public DbSet<InvoiceEntity> Invoices { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<InvoiceEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CustomerName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => e.Status);
        });

        // ✅ ADD: MassTransit Outbox tables
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
    }
}
```

#### Step 2: Create Migration

```bash
cd src/Services/Invoice/Invoice.API
dotnet ef migrations add AddMassTransitOutbox
dotnet ef database update
```

#### Step 3: Configure Outbox in Program.cs

```csharp
// File: src/Services/Invoice/Invoice.API/Program.cs

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ApplyPaymentToInvoiceConsumer>();
    x.AddConsumer<ValidateInvoiceCommandConsumer>();

    // ✅ ADD: Configure Entity Framework Outbox
    x.AddEntityFrameworkOutbox<InvoiceDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
        o.QueryDelay = TimeSpan.FromSeconds(1);
        o.MessageDeliveryLimit = 3;
        o.MessageDeliveryTimeout = TimeSpan.FromMinutes(5);
        o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.ConfigureEndpoints(context);
    });
});
```

### 2.2. Update InvoiceService with Transaction

```csharp
// File: src/Services/Invoice/Invoice.API/Application/Services/InvoiceService.cs

public class InvoiceService : IInvoiceService
{
    private readonly InvoiceDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<InvoiceService> _logger;

    public InvoiceService(
        InvoiceDbContext context,
        IPublishEndpoint publishEndpoint,
        ILogger<InvoiceService> logger)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<InvoiceEntity> CreateInvoiceAsync(
        string customerName,
        decimal amount)
    {
        // ✅ Use ExecutionStrategy
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            // ✅ Begin transaction
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogDebug(
                    "Transaction started for invoice creation. TransactionId: {TransactionId}",
                    transaction.TransactionId
                );

                // 1. Create Invoice
                var invoice = InvoiceEntity.Create(customerName, amount);
                _context.Invoices.Add(invoice);

                // 2. Publish event (will be saved to Outbox)
                await _publishEndpoint.Publish(new InvoiceCreatedEvent
                {
                    Id = invoice.Id,
                    CustomerName = invoice.CustomerName,
                    Amount = invoice.Amount,
                    CreatedAt = invoice.CreatedAt
                });

                // 3. Publish Audit event
                await _publishEndpoint.Publish(new AuditEvent
                {
                    EntityType = "Invoice",
                    EntityId = invoice.Id.ToString(),
                    Action = "Create",
                    Actor = "System", // TODO: Get from ClaimsPrincipal
                    BeforeJson = null,
                    AfterJson = JsonSerializer.Serialize(new
                    {
                        invoice.Id,
                        invoice.CustomerName,
                        invoice.Amount,
                        invoice.Status
                    }),
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = Activity.Current?.Id ?? Guid.NewGuid().ToString()
                });

                // 4. Save changes (commits Invoice + OutboxMessages)
                await _context.SaveChangesAsync();

                // 5. Commit transaction
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Invoice created successfully. InvoiceId: {InvoiceId}, TransactionId: {TransactionId}",
                    invoice.Id,
                    transaction.TransactionId
                );

                return invoice;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(
                    ex,
                    "Failed to create invoice. Transaction rolled back."
                );
                throw;
            }
        });
    }

    public async Task<bool> UpdateStatusAsync(
        Guid id,
        InvoiceStatus status)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var invoice = await _context.Invoices.FindAsync(id);
                if (invoice is null)
                {
                    return false;
                }

                var oldStatus = invoice.Status;
                invoice.Status = status;

                // Publish Audit event
                await _publishEndpoint.Publish(new AuditEvent
                {
                    EntityType = "Invoice",
                    EntityId = invoice.Id.ToString(),
                    Action = "UpdateStatus",
                    Actor = "System",
                    BeforeJson = JsonSerializer.Serialize(new { Status = oldStatus }),
                    AfterJson = JsonSerializer.Serialize(new { Status = status }),
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = Activity.Current?.Id ?? Guid.NewGuid().ToString()
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Invoice status updated. InvoiceId: {InvoiceId}, OldStatus: {OldStatus}, NewStatus: {NewStatus}",
                    id,
                    oldStatus,
                    status
                );

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(
                    ex,
                    "Failed to update invoice status. InvoiceId: {InvoiceId}",
                    id
                );
                throw;
            }
        });
    }
}
```

---

## 📦 3. Audit Service Implementation

### 3.1. Update AuditEventConsumer with Partitioned Hash Chain

```csharp
// File: src/Services/Audit/Audit.API/Application/Consumers/AuditEventConsumer.cs

public class AuditEventConsumer : IConsumer<IAuditEvent>
{
    private readonly AuditDbContext _db;
    private readonly IHashChainService _hashChainService;
    private readonly ILogger<AuditEventConsumer> _logger;

    public AuditEventConsumer(
        AuditDbContext db,
        IHashChainService hashChainService,
        ILogger<AuditEventConsumer> logger)
    {
        _db = db;
        _hashChainService = hashChainService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IAuditEvent> context)
    {
        var message = context.Message;

        // ✅ Use ExecutionStrategy for retry on deadlock
        var strategy = _db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            // Use Read Committed with serialized append per partition.
            // HashChainService must lock/update a ChainHead row or use an application lock
            // keyed by PartitionKey before assigning Sequence and PreviousHash.
            await using var transaction = await _db.Database.BeginTransactionAsync(
                context.CancellationToken
            );

            try
            {
                _logger.LogDebug(
                    "Transaction started for audit entry. TransactionId: {TransactionId}",
                    transaction.TransactionId
                );

                // 1. Create AuditEntry
                var entry = new AuditEntry
                {
                    Id = Guid.NewGuid(),
                    EntityType = message.EntityType,
                    EntityId = message.EntityId,
                    Action = message.Action,
                    Actor = message.Actor,
                    BeforeJson = message.BeforeJson,
                    AfterJson = message.AfterJson,
                    Timestamp = message.Timestamp,
                    CorrelationId = message.CorrelationId,
                    PartitionKey = message.EntityType
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
                    "AuditEntry persisted successfully. Id: {Id}, Partition: {PartitionKey}, Sequence: {Sequence}, Hash: {Hash}, TransactionId: {TransactionId}",
                    entry.Id,
                    entry.PartitionKey,
                    entry.Sequence,
                    entry.Hash,
                    transaction.TransactionId
                );
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(context.CancellationToken);

                _logger.LogError(
                    ex,
                    "Failed to persist AuditEntry. Transaction rolled back. EntityType: {EntityType}, EntityId: {EntityId}",
                    message.EntityType,
                    message.EntityId
                );

                throw;
            }
        });
    }
}
```

### 3.2. Update AuditController with Transaction

```csharp
// File: src/Services/Audit/Audit.API/Controllers/AuditController.cs

[HttpPatch("{id:guid}/mark-reversed")]
public async Task<IActionResult> MarkAsReversed(
    Guid id,
    [FromBody] MarkAsReversedRequest request,
    CancellationToken ct)
{
    var strategy = _db.Database.CreateExecutionStrategy();

    return await strategy.ExecuteAsync(async () =>
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            var entry = await _db.AuditEntries.FindAsync(new object[] { id }, ct);

            if (entry is null)
            {
                return NotFound(new { message = "AuditEntry not found." });
            }

            entry.MarkAsReversed(request.ReversalEntryId, request.Reason);
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "AuditEntry marked as reversed. Id: {Id}, ReversalEntryId: {ReversalEntryId}",
                id,
                request.ReversalEntryId
            );

            return Ok(new { message = "AuditEntry đã được đánh dấu reversed." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(
                ex,
                "Failed to mark AuditEntry as reversed. Id: {Id}",
                id
            );
            throw;
        }
    });
}
```

---

## 📦 4. Identity Service Implementation

### 4.1. Update DbSeeder with Transaction

```csharp
// File: src/Services/Identity/Identity.API/Infrastructure/Data/DbSeeder.cs

public static async Task SeedAsync(
    IdentityDbContext context,
    ILogger logger)
{
    // ✅ Use ExecutionStrategy
    var strategy = context.Database.CreateExecutionStrategy();

    await strategy.ExecuteAsync(async () =>
    {
        // ✅ Begin transaction for entire seed operation
        await using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            logger.LogInformation("Starting database seeding...");

            // 1. Seed Permissions
            await SeedPermissionsAsync(context, logger);

            // 2. Seed Roles
            await SeedRolesAsync(context, logger);

            // 3. Seed Users
            await SeedUsersAsync(context, logger);

            // Commit all seeds
            await transaction.CommitAsync();

            logger.LogInformation("Database seeding completed successfully.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Database seeding failed. Transaction rolled back.");
            throw;
        }
    });
}

private static async Task SeedPermissionsAsync(
    IdentityDbContext context,
    ILogger logger)
{
    if (await context.Permissions.AnyAsync())
    {
        logger.LogInformation("Permissions already seeded. Skipping.");
        return;
    }

    var permissions = Permissions.GetAll()
        .Select(p => new Permission
        {
            Id = Guid.NewGuid(),
            Name = p,
            Description = $"Permission for {p}"
        })
        .ToList();

    context.Permissions.AddRange(permissions);
    await context.SaveChangesAsync();

    logger.LogInformation("Seeded {Count} permissions.", permissions.Count);
}

// Similar updates for SeedRolesAsync and SeedUsersAsync...
```

---

## 📦 5. Orchestration Service Implementation

### 5.1. Update ProcessOrchestrationService with Transaction

```csharp
// File: src/Services/Orchestration/Orchestration.API/Application/Services/ProcessOrchestrationService.cs

public async Task HandlePaymentInitiatedAsync(
    IPaymentInitiatedEvent evt,
    CancellationToken cancellationToken)
{
    var strategy = _db.Database.CreateExecutionStrategy();

    await strategy.ExecuteAsync(async () =>
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // 1. Create ProcessFlow
            var flow = new ProcessFlow
            {
                Id = Guid.NewGuid(),
                InvoiceId = evt.InvoiceId,
                PaymentId = evt.PaymentId,
                Status = "Initiated",
                StartedAt = DateTime.UtcNow
            };
            _db.ProcessFlows.Add(flow);

            // 2. Create FlowStep
            _db.FlowSteps.Add(new FlowStep
            {
                Id = Guid.NewGuid(),
                FlowId = flow.Id,
                StepName = "PaymentInitiated",
                Status = "Completed",
                Timestamp = evt.InitiatedAt,
                Metadata = JsonSerializer.Serialize(new
                {
                    evt.PaymentId,
                    evt.InvoiceId,
                    evt.Amount
                })
            });

            // 3. Save changes
            await _db.SaveChangesAsync(cancellationToken);

            // 4. Commit transaction
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "ProcessFlow created. FlowId: {FlowId}, InvoiceId: {InvoiceId}",
                flow.Id,
                evt.InvoiceId
            );
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(
                ex,
                "Failed to create ProcessFlow. InvoiceId: {InvoiceId}",
                evt.InvoiceId
            );
            throw;
        }
    });
}
```

---

## ✅ 6. Testing Checklist

### 6.1. Unit Tests

- [ ] Test transaction rollback on SaveChanges failure
- [ ] Test transaction rollback on Publish failure (before Outbox)
- [ ] Test idempotency with transaction
- [ ] Test concurrent audit append with partitioned sequence/lock

### 6.2. Integration Tests

- [ ] Test Outbox message delivery after commit
- [ ] Test Outbox retry on RabbitMQ failure
- [ ] Test hash chain integrity with concurrent consumers
- [ ] Test cross-service flow with compensation

### 6.3. Performance Tests

- [ ] Benchmark transaction overhead
- [ ] Benchmark partitioned audit append vs Serializable fallback
- [ ] Benchmark Outbox delivery latency
- [ ] Load test with concurrent requests

---

## 📊 7. Monitoring Setup

### 7.1. Add Transaction Metrics

```csharp
// Add to each service's Program.cs

using Prometheus;

// Transaction duration histogram
var transactionDuration = Metrics.CreateHistogram(
    "transaction_duration_seconds",
    "Duration of database transactions",
    new HistogramConfiguration
    {
        LabelNames = new[] { "service", "operation", "status" },
        Buckets = Histogram.ExponentialBuckets(0.001, 2, 10)
    }
);

// Transaction counter
var transactionTotal = Metrics.CreateCounter(
    "transaction_total",
    "Total number of transactions",
    new CounterConfiguration
    {
        LabelNames = new[] { "service", "operation", "status" }
    }
);

// Usage in service:
var stopwatch = Stopwatch.StartNew();
try
{
    await using var tx = await _context.Database.BeginTransactionAsync();
    // ... business logic ...
    await tx.CommitAsync();
    
    transactionTotal.WithLabels("payment", "initiate", "committed").Inc();
    transactionDuration.WithLabels("payment", "initiate", "committed")
        .Observe(stopwatch.Elapsed.TotalSeconds);
}
catch
{
    transactionTotal.WithLabels("payment", "initiate", "rolled_back").Inc();
    transactionDuration.WithLabels("payment", "initiate", "rolled_back")
        .Observe(stopwatch.Elapsed.TotalSeconds);
    throw;
}
```

### 7.2. Grafana Dashboard Queries

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
```

---

## 🚀 8. Deployment Plan

### Phase 1: Enable Outbox (Low Risk)
1. Deploy Payment Service with Outbox
2. Deploy Invoice Service with Outbox
3. Monitor Outbox delivery metrics
4. Verify no message loss

### Phase 2: Add Transactions (Medium Risk)
1. Deploy Payment Service with transactions
2. Deploy Invoice Service with transactions
3. Monitor transaction metrics
4. Verify data consistency

### Phase 3: Audit Partitioned Hash Chain (High Risk)
1. Deploy Audit Service with `PartitionKey`, `Sequence`, and chain-head/app-lock support
2. Monitor append conflict/deadlock rate
3. Tune retry policy and partition strategy if needed
4. Verify hash chain integrity per partition

### Phase 4: Remaining Services (Low Risk)
1. Deploy Identity Service updates
2. Deploy Orchestration Service updates
3. Deploy Report Service updates
4. Final verification

---

## 📚 9. Rollback Plan

If issues occur:

1. **Outbox issues**: Disable Outbox, revert to direct publish
   ```csharp
   // Comment out in Program.cs:
   // x.AddEntityFrameworkOutbox<PaymentDbContext>(o => { ... });
   ```

2. **Transaction issues**: Remove transaction wrapper
   ```csharp
   // Revert to:
   await _context.SaveChangesAsync();
   await _publishEndpoint.Publish(event);
   ```

3. **Audit append issues**: Temporarily serialize per partition more strictly
   ```csharp
   // Preferred: keep partitioning, but strengthen append serialization
   // by using a ChainHead row lock or application lock per PartitionKey.
   ```

---

*Implementation guide này cung cấp code cụ thể để áp dụng Transaction Management vào dự án.*
*Cập nhật lần cuối: 07/05/2026*
