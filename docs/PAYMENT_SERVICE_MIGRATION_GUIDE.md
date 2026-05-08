# Payment Service - Transaction Management Migration Guide

## 📋 Overview

This guide provides step-by-step instructions for applying the transaction management changes to the Payment service.

## ✅ What Has Been Implemented

### 1. Core Infrastructure
- ✅ `PaymentUnitOfWork` - Transaction management
- ✅ `InitiatePaymentCommand` and Handler - MediatR command pattern
- ✅ Enhanced `IdempotencyService` - Response caching
- ✅ Updated `IdempotencyRecord` entity - New fields for response replay
- ✅ MediatR pipeline with behaviors (Logging, Validation, Transaction)
- ✅ Updated `PaymentsController` - Uses MediatR

### 2. Key Changes

#### Before (Direct Service Call)
```csharp
// Controller
var result = await _paymentService.ProcessPaymentAsync(payment, idempotencyKey);

// Service (no transaction)
_context.Payments.Add(payment);
await _context.SaveChangesAsync();
await _publishEndpoint.Publish(event); // ❌ After commit
```

#### After (MediatR with Transaction Pipeline)
```csharp
// Controller
var command = new InitiatePaymentCommand(...);
var result = await _mediator.Send(command);

// Handler (automatic transaction via TransactionBehavior)
_context.Payments.Add(payment);
await _publishEndpoint.Publish(event); // ✅ Inside transaction (Outbox)
// No SaveChangesAsync - UnitOfWork.CommitAsync handles it
```

## 🔧 Migration Steps

### Step 1: Database Migration

Run the following SQL script to add new fields to `IdempotencyRecords`:

```sql
-- Add response caching fields
ALTER TABLE IdempotencyRecords
ADD Status NVARCHAR(50) NOT NULL DEFAULT 'InProgress',
    ResponseJson NVARCHAR(MAX) NULL,
    StatusCode INT NULL;

-- Verify the changes
SELECT TOP 1 * FROM IdempotencyRecords;
```

Or use Entity Framework migrations:

```bash
cd src/Services/Payment/Payment.API

# Create migration
dotnet ef migrations add AddIdempotencyResponseCaching

# Apply migration
dotnet ef database update
```

### Step 2: Update NuGet Packages

Ensure the following packages are installed:

```xml
<PackageReference Include="MediatR" Version="12.2.0" />
<PackageReference Include="FluentValidation" Version="11.9.0" />
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.9.0" />
<PackageReference Include="prometheus-net" Version="8.2.1" />
<PackageReference Include="prometheus-net.AspNetCore" Version="8.2.1" />
```

Add to `Payment.API.csproj`:

```bash
cd src/Services/Payment/Payment.API
dotnet add package MediatR
dotnet add package FluentValidation
dotnet add package FluentValidation.DependencyInjectionExtensions
```

### Step 3: Verify BuildingBlocks Reference

Ensure `Payment.API.csproj` references the BuildingBlocks project:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\..\BuildingBlocks\Bizcore.BuildingBlocks\Bizcore.BuildingBlocks.csproj" />
</ItemGroup>
```

### Step 4: Test the Changes

#### 4.1 Start the Services

```bash
# Start dependencies (RabbitMQ, SQL Server, etc.)
docker-compose up -d

# Start Payment API
cd src/Services/Payment/Payment.API
dotnet run
```

#### 4.2 Test Payment Initiation

```bash
# First request
curl -X POST http://localhost:5001/api/v1/payment/pay \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: test-key-001" \
  -d '{
    "invoiceId": "f1d2c3b4-a5e6-4d7f-8e9a-0b1c2d3e4f5a",
    "amount": 1000.00,
    "paymentMethod": "CreditCard"
  }'

# Expected: 202 Accepted with PaymentId

# Duplicate request (same idempotency key)
curl -X POST http://localhost:5001/api/v1/payment/pay \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: test-key-001" \
  -d '{
    "invoiceId": "f1d2c3b4-a5e6-4d7f-8e9a-0b1c2d3e4f5a",
    "amount": 1000.00,
    "paymentMethod": "CreditCard"
  }'

# Expected: 202 Accepted with SAME PaymentId (cached response)
```

#### 4.3 Verify Database

```sql
-- Check payment was created
SELECT * FROM Payments ORDER BY PaymentDate DESC;

-- Check idempotency record with cached response
SELECT 
    [Key],
    PaymentId,
    Status,
    StatusCode,
    LEFT(ResponseJson, 100) as ResponsePreview,
    CreatedAt,
    ExpiresAt
FROM IdempotencyRecords
WHERE [Key] = 'test-key-001';

-- Check outbox messages
SELECT 
    MessageId,
    MessageType,
    SentTime,
    InboxMessageId,
    InboxConsumerId
FROM OutboxMessage
ORDER BY SentTime DESC;
```

#### 4.4 Check Logs

Look for transaction logs:

```
[INFO] Begin transaction for InitiatePaymentCommand
[INFO] Payment initiated. PaymentId: xxx, InvoiceId: xxx
[INFO] Committed transaction for InitiatePaymentCommand. TransactionId: xxx
```

#### 4.5 Check Prometheus Metrics

```bash
# Access metrics endpoint
curl http://localhost:5001/metrics | grep transaction

# Expected metrics:
# transaction_total{service="payment",operation="InitiatePaymentCommand",status="committed"} 1
# transaction_duration_seconds_sum{service="payment",operation="InitiatePaymentCommand",status="committed"} 0.123
```

### Step 5: Test Error Scenarios

#### 5.1 Test Transaction Rollback

Simulate a failure by stopping RabbitMQ:

```bash
docker stop rabbitmq
```

Then try to create a payment:

```bash
curl -X POST http://localhost:5001/api/v1/payment/pay \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: test-key-rollback" \
  -d '{
    "invoiceId": "f1d2c3b4-a5e6-4d7f-8e9a-0b1c2d3e4f5a",
    "amount": 1000.00,
    "paymentMethod": "CreditCard"
  }'

# Expected: 500 Internal Server Error
```

Verify rollback:

```sql
-- Payment should NOT exist
SELECT * FROM Payments WHERE IdempotencyKey = 'test-key-rollback';

-- Idempotency record should NOT exist
SELECT * FROM IdempotencyRecords WHERE [Key] = 'test-key-rollback';
```

Check logs:

```
[ERROR] Rolled back transaction for InitiatePaymentCommand. TransactionId: xxx
```

Restart RabbitMQ:

```bash
docker start rabbitmq
```

#### 5.2 Test Idempotency Conflict

Try to use the same key with different payload:

```bash
# First request
curl -X POST http://localhost:5001/api/v1/payment/pay \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: test-key-conflict" \
  -d '{
    "invoiceId": "f1d2c3b4-a5e6-4d7f-8e9a-0b1c2d3e4f5a",
    "amount": 1000.00,
    "paymentMethod": "CreditCard"
  }'

# Second request with DIFFERENT amount
curl -X POST http://localhost:5001/api/v1/payment/pay \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: test-key-conflict" \
  -d '{
    "invoiceId": "f1d2c3b4-a5e6-4d7f-8e9a-0b1c2d3e4f5a",
    "amount": 2000.00,
    "paymentMethod": "CreditCard"
  }'

# Expected: 400 Bad Request
# Error: "Idempotency key already used with different request payload"
```

## 🔍 Troubleshooting

### Issue 1: MediatR Not Found

**Error:**
```
The type or namespace name 'MediatR' could not be found
```

**Solution:**
```bash
cd src/Services/Payment/Payment.API
dotnet add package MediatR
dotnet restore
```

### Issue 2: IUnitOfWork Not Found

**Error:**
```
The type or namespace name 'IUnitOfWork' could not be found
```

**Solution:**
Ensure BuildingBlocks project reference is added and the project is built:

```bash
cd src/BuildingBlocks/Bizcore.BuildingBlocks
dotnet build

cd ../../Services/Payment/Payment.API
dotnet restore
```

### Issue 3: Migration Fails

**Error:**
```
Cannot add column 'Status' with DEFAULT constraint
```

**Solution:**
The table might have existing data. Use a two-step migration:

```sql
-- Step 1: Add nullable column
ALTER TABLE IdempotencyRecords
ADD Status NVARCHAR(50) NULL;

-- Step 2: Update existing rows
UPDATE IdempotencyRecords
SET Status = 'Completed'
WHERE Status IS NULL;

-- Step 3: Make it NOT NULL
ALTER TABLE IdempotencyRecords
ALTER COLUMN Status NVARCHAR(50) NOT NULL;
```

### Issue 4: Outbox Messages Not Delivered

**Symptoms:**
- Payment created successfully
- OutboxMessage table has pending messages
- Events not received by consumers

**Solution:**

1. Check MassTransit Outbox configuration in `Program.cs`:

```csharp
x.AddEntityFrameworkOutbox<AppDbContext>(o =>
{
    o.UseSqlServer();
    o.UseBusOutbox(); // ✅ Must be present
    o.QueryDelay = TimeSpan.FromSeconds(1);
});
```

2. Check RabbitMQ connection:

```bash
docker logs rabbitmq
```

3. Check Outbox delivery logs:

```
[INFO] Outbox message delivered. MessageId: xxx
```

### Issue 5: Transaction Timeout

**Error:**
```
Transaction timeout (default: 30 seconds)
```

**Solution:**
Increase transaction timeout in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=...;Command Timeout=60;"
  }
}
```

## 📊 Performance Considerations

### Expected Latency

| Operation | Before | After | Notes |
|-----------|--------|-------|-------|
| Payment Initiation | ~50ms | ~80ms | +30ms for transaction overhead |
| Duplicate Request | ~50ms | ~20ms | Faster due to cached response |
| Outbox Delivery | N/A | ~1-5s | Async, doesn't block request |

### Throughput

- **Before:** ~500 req/s (no transaction safety)
- **After:** ~300 req/s (with transaction safety)
- **Bottleneck:** Database transaction commit

### Optimization Tips

1. **Use connection pooling:**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Min Pool Size=10;Max Pool Size=100;"
  }
}
```

2. **Monitor transaction duration:**

```promql
histogram_quantile(0.95, 
  rate(transaction_duration_seconds_bucket[5m])
)
```

3. **Tune Outbox delivery:**

```csharp
o.QueryDelay = TimeSpan.FromMilliseconds(500); // Faster polling
o.MessageDeliveryLimit = 5; // More retries
```

## ✅ Verification Checklist

- [ ] Database migration applied successfully
- [ ] NuGet packages installed
- [ ] BuildingBlocks reference added
- [ ] Payment initiation works (202 Accepted)
- [ ] Duplicate request returns cached response
- [ ] Idempotency conflict detected (400 Bad Request)
- [ ] Transaction rollback works (no orphan records)
- [ ] Outbox messages delivered to RabbitMQ
- [ ] Prometheus metrics exposed
- [ ] Logs show transaction lifecycle
- [ ] Performance acceptable (< 100ms p95)

## 🎯 Next Steps

After Payment service is stable:

1. **Update Payment Consumers** - Add idempotency checks
2. **Implement Invoice Service** - Similar transaction management
3. **Implement Audit Service** - Partitioned hash chain
4. **Implement Orchestration Service** - Saga State Machine
5. **Add Integration Tests** - Test transaction scenarios
6. **Setup Monitoring** - Grafana dashboards and alerts

---

*Last Updated: 2026-05-08*
*Version: 1.0*
