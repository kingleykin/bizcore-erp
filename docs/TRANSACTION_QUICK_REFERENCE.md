# Transaction Management - Quick Reference

## 🎯 Khi nào cần Transaction?

| Tình huống | Cần Transaction? | Pattern |
|------------|------------------|---------|
| Ghi 1 bảng, không publish event | ❌ Không cần | `SaveChangesAsync()` |
| Ghi nhiều bảng, cùng DB | ✅ Cần | Local Transaction |
| Ghi DB + Publish event | ✅ Cần | Outbox Pattern |
| Audit với Hash Chain | ✅ Cần | Partitioned Append + per-partition lock/sequence |
| Cross-service coordination | ❌ Không dùng transaction | Saga Pattern |

---

## 📝 Code Templates

### 1. Local Transaction (Nhiều bảng, cùng DB)

```csharp
var strategy = _context.Database.CreateExecutionStrategy();

return await strategy.ExecuteAsync(async () =>
{
    await using var transaction = await _context.Database.BeginTransactionAsync();
    
    try
    {
        // 1. Business logic
        _context.Table1.Add(entity1);
        _context.Table2.Add(entity2);
        
        // 2. Save changes
        await _context.SaveChangesAsync();
        
        // 3. Commit
        await transaction.CommitAsync();
        
        return result;
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        _logger.LogError(ex, "Transaction failed");
        throw;
    }
});
```

### 2. Outbox Pattern (DB + Event)

```csharp
var strategy = _context.Database.CreateExecutionStrategy();

return await strategy.ExecuteAsync(async () =>
{
    await using var transaction = await _context.Database.BeginTransactionAsync();
    
    try
    {
        // 1. Create entity
        _context.Entities.Add(entity);
        
        // 2. Publish event (saved to Outbox table)
        await _publishEndpoint.Publish(new EntityCreatedEvent { ... });
        
        // 3. Save changes (commits entity + outbox message)
        await _context.SaveChangesAsync();
        
        // 4. Commit transaction
        await transaction.CommitAsync();
        
        // MassTransit will deliver message from Outbox
        
        return entity;
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        _logger.LogError(ex, "Transaction failed");
        throw;
    }
});
```

### 3. Partitioned Audit Hash Chain

```csharp
var strategy = _db.Database.CreateExecutionStrategy();

await strategy.ExecuteAsync(async () =>
{
    await using var transaction = await _db.Database.BeginTransactionAsync();
    
    try
    {
        // 1. Serialize append for this partition only
        // Implementation options: ChainHead row lock, application lock, or optimistic retry.
        entry.PartitionKey = entry.EntityType;
        entry.Sequence = await _hashChainService.NextSequenceAsync(entry.PartitionKey);

        // 2. Read previous hash by (PartitionKey, Sequence - 1)
        await _hashChainService.ComputeAndSetHashAsync(entry);
        
        // 3. Save audit entry
        _db.AuditEntries.Add(entry);
        await _db.SaveChangesAsync();
        
        // 4. Commit
        await transaction.CommitAsync();
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        _logger.LogError(ex, "Audit transaction failed");
        throw;
    }
});
```

---

## ⚙️ MassTransit Outbox Setup

### 1. DbContext Configuration

```csharp
using MassTransit;

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    // ✅ Add Outbox tables
    modelBuilder.AddInboxStateEntity();
    modelBuilder.AddOutboxStateEntity();
    modelBuilder.AddOutboxMessageEntity();
}
```

### 2. Program.cs Configuration

```csharp
builder.Services.AddMassTransit(x =>
{
    x.AddConsumers(typeof(Program).Assembly);
    
    // ✅ Configure Outbox
    x.AddEntityFrameworkOutbox<YourDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
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

### 3. Create Migration

```bash
dotnet ef migrations add AddMassTransitOutbox --project YourProject
dotnet ef database update --project YourProject
```

---

## 🔍 Isolation Levels

| Level | Dirty Read | Non-Repeatable Read | Phantom Read | Use Case |
|-------|------------|---------------------|--------------|----------|
| **Read Uncommitted** | ✅ Yes | ✅ Yes | ✅ Yes | ❌ Never use |
| **Read Committed** (Default) | ❌ No | ✅ Yes | ✅ Yes | ✅ Most cases |
| **Repeatable Read** | ❌ No | ❌ No | ✅ Yes | ⚠️ Rarely needed |
| **Serializable** | ❌ No | ❌ No | ❌ No | ⚠️ Fallback per partition only |

**Recommendation:**
- 95% cases: Use default (Read Committed)
- Audit Service: Use partitioned append with per-partition lock/sequence
- Never use Read Uncommitted

---

## 🚨 Common Mistakes

### ❌ BAD: Publish before commit

```csharp
await _context.SaveChangesAsync();
await _publishEndpoint.Publish(event);  // ❌ Event sent, but transaction might rollback
```

### ✅ GOOD: Publish inside transaction (Outbox)

```csharp
await using var tx = await _context.Database.BeginTransactionAsync();
await _publishEndpoint.Publish(event);  // ✅ Saved to Outbox
await _context.SaveChangesAsync();
await tx.CommitAsync();
```

### ❌ BAD: Long transaction

```csharp
await using var tx = await _context.Database.BeginTransactionAsync();
await ExpensiveApiCall();  // ❌ Network I/O in transaction
await _context.SaveChangesAsync();
await tx.CommitAsync();
```

### ✅ GOOD: Short transaction

```csharp
var data = await ExpensiveApiCall();  // ✅ Call before transaction
await using var tx = await _context.Database.BeginTransactionAsync();
_context.Add(data);
await _context.SaveChangesAsync();
await tx.CommitAsync();
```

### ❌ BAD: Forget rollback

```csharp
await using var tx = await _context.Database.BeginTransactionAsync();
try
{
    await _context.SaveChangesAsync();
    await tx.CommitAsync();
}
catch (Exception ex)
{
    // ❌ No rollback
    throw;
}
```

### ✅ GOOD: Always rollback on error

```csharp
await using var tx = await _context.Database.BeginTransactionAsync();
try
{
    await _context.SaveChangesAsync();
    await tx.CommitAsync();
}
catch (Exception ex)
{
    await tx.RollbackAsync();  // ✅ Explicit rollback
    throw;
}
```

---

## 📊 Performance Impact

| Pattern | Latency | Throughput | Reliability |
|---------|---------|------------|-------------|
| No Transaction | ~5ms | ⚡⚡⚡ High | ❌ Unsafe |
| Read Committed | ~10ms | ⚡⚡ Medium | ✅ Safe |
| Partitioned Audit Append | ~15-30ms | ⚡⚡ Medium | ✅ Safe when append is serialized per partition |
| Serializable | ~50ms+ | ⚡ Low | ⚠️ Fallback only |
| Outbox | ~15ms | ⚡⚡ Medium | ✅ Safe + Reliable |

**Trade-offs:**
- No Transaction: Fast but unsafe (data corruption risk)
- Read Committed: Good balance (recommended default)
- Serializable: Slow; use only as a temporary fallback for Audit partitions that do not yet have append locking
- Outbox: Slight overhead but prevents message loss

---

## ✅ Checklist

### Before Deployment

- [ ] All multi-table operations wrapped in transaction
- [ ] Outbox enabled for services that publish events
- [ ] Audit Service uses partitioned hash chain with per-partition lock/sequence
- [ ] ExecutionStrategy added for retry logic
- [ ] Migrations created for Outbox tables
- [ ] Transaction logging added
- [ ] Metrics configured (Prometheus)
- [ ] Integration tests written
- [ ] Concurrency tests written
- [ ] Performance benchmarks done

### After Deployment

- [ ] Monitor transaction success rate
- [ ] Monitor transaction duration (p95, p99)
- [ ] Monitor Outbox delivery latency
- [ ] Monitor deadlock rate (Audit Service)
- [ ] Verify no data inconsistencies
- [ ] Verify no message loss
- [ ] Check Grafana dashboards
- [ ] Review error logs

---

## 📚 Related Documents

- [TRANSACTION_MANAGEMENT_DESIGN.md](TRANSACTION_MANAGEMENT_DESIGN.md) - Thiết kế chi tiết
- [TRANSACTION_IMPLEMENTATION_GUIDE.md](TRANSACTION_IMPLEMENTATION_GUIDE.md) - Hướng dẫn implementation
- [IDEMPOTENCY_DESIGN.md](IDEMPOTENCY_DESIGN.md) - Idempotency pattern
- [PROJECT_INDEX.md](PROJECT_INDEX.md) - Tổng quan dự án

---

## 🆘 Troubleshooting

### Problem: Audit hash chain conflict/deadlock

**Solution:**
```csharp
// Serialize append per partition and retry transient conflicts.
// Prefer a ChainHead row per PartitionKey or an application lock keyed by PartitionKey.
var strategy = _db.Database.CreateExecutionStrategy();
await strategy.ExecuteAsync(() => AppendAuditEntryAsync(entry, ct));
```

### Problem: Outbox messages not delivered

**Check:**
1. Verify Outbox tables exist: `InboxState`, `OutboxState`, `OutboxMessage`
2. Check RabbitMQ connection
3. Check `OutboxMessage` table for pending messages
4. Review MassTransit logs

**Query:**
```sql
SELECT * FROM OutboxMessage WHERE LockId IS NOT NULL;  -- Stuck messages
SELECT * FROM OutboxState WHERE Delivered IS NULL;     -- Pending deliveries
```

### Problem: Transaction timeout

**Solution:**
```csharp
// Increase command timeout
builder.Services.AddDbContext<YourDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.CommandTimeout(60);  // 60 seconds
    });
});
```

---

*Quick reference này giúp developers nhanh chóng áp dụng Transaction Management đúng cách.*
*Cập nhật lần cuối: 07/05/2026*
