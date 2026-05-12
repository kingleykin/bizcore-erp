# Saga Orchestrator Guardrails - Production Readiness

## Tổng quan

Document này mô tả các guardrails đã được implement để đảm bảo Saga Orchestrator pattern hoạt động ổn định trong production, xử lý các edge cases:

- ❌ Client retry/polling quá nhiều → quá tải
- ❌ Event thất lạc/consumer lỗi → payment "Processing" mãi
- ❌ Network timeout → saga stuck
- ❌ Message loss → data inconsistency

---

## 1. Idempotency Protection

### Vấn đề

Client retry request nhiều lần (network timeout, user click nhiều lần) → tạo duplicate payments.

### Giải pháp

✅ **Idempotency Key** (đã implement)

```csharp
// PaymentService.cs
if (_cache.TryGetValue(idempotencyKey, out Guid existingPaymentId))
{
    _logger.LogInformation("Duplicate request detected IdempotencyKey={Key}", idempotencyKey);
    return new InitiatePaymentResult(true, existingPaymentId, null);
}
```

**Cách dùng:**

```http
POST /api/v1/payment/pay
Headers:
  X-Idempotency-Key: unique-key-123
```

- Key phải unique per request (UUID recommended)
- Retry với cùng key → trả về cùng `paymentId`
- Cache TTL: 30 phút

---

## 2. Message Retry + Dead Letter Queue

### Vấn đề

Consumer crash, network issue → message bị mất → saga stuck.

### Giải pháp

✅ **Message Retry Policy**

```csharp
// Program.cs (tất cả services)
cfg.UseMessageRetry(r => r.Intervals(
    TimeSpan.FromSeconds(5),   // Retry 1
    TimeSpan.FromSeconds(10),  // Retry 2
    TimeSpan.FromSeconds(30)   // Retry 3
));
```

✅ **Dead Letter Queue**

```csharp
e.SetQueueArgument("x-dead-letter-exchange", $"{e.InputAddress.AbsolutePath}_error");
e.SetQueueArgument("x-message-ttl", (int)TimeSpan.FromDays(7).TotalMilliseconds);
```

**Flow:**

```
Message → Consumer fail → Retry 1 (5s) → Retry 2 (10s) → Retry 3 (30s) → Dead Letter Queue
```

**Dead Letter Queue:**

- `payment-confirm_error`
- `payment-reject_error`
- `invoice-validate_error`
- `orchestration-payment-saga_error`

**Monitoring:**

```bash
# Check DLQ trong RabbitMQ Management UI
http://localhost:15672/#/queues/%2F/payment-confirm_error
```

---

## 3. Queue Durability

### Vấn đề

RabbitMQ restart → messages bị mất.

### Giải pháp

✅ **Durable Queues**

```csharp
e.Durable = true;
e.AutoDelete = false;
```

- Queue persist vào disk
- Survive RabbitMQ restart
- Messages không bị mất khi broker down

---

## 4. Outbox Pattern

### Vấn đề

Publish event fail sau khi commit DB → data inconsistency.

### Giải pháp

✅ **Entity Framework Outbox** (Sản xuất thực tế)

Để đạt được độ tin cậy cấp độ production, chúng ta tách biệt 2 giai đoạn:

**1. Giai đoạn đăng ký (Registration):**
Trong `AddMassTransit(...)`:

```csharp
x.AddBusinessOutbox<AppDbContext>(); 
```

*Đăng ký: Inbox, Outbox, Delivery services và Background Dispatcher.*

**2. Giai đoạn Endpoint (Middleware):**
Trong `ReceiveEndpoint(...)`:

```csharp
e.UseEntityFrameworkOutbox<AppDbContext>(context);
```

*Đánh chặn message bên trong Consumer để đảm bảo tính Idempotency và Atomicity.*

**Cấu hình tối ưu:**

```csharp
x.AddEntityFrameworkOutbox<TDbContext>(o =>
{
    o.UseSqlServer();
    o.UseBusOutbox(); // Atomicity cho Publish/Send từ Controller/Service
    o.QueryDelay = TimeSpan.FromSeconds(1); // Tăng tốc độ đẩy message (ERP standard)
});
```

**Quan trọng:** `AppDbContext` phải chứa các thực thể của MassTransit:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder) {
    modelBuilder.AddInboxStateEntity();
    modelBuilder.AddOutboxMessageEntity();
    modelBuilder.AddOutboxStateEntity();
}
```

**Flow:**

```
1. Begin transaction
2. Save entity to DB
3. Save outbox message to DB
4. Commit transaction
5. Background worker publish message từ outbox
```

**Đảm bảo:**

- Atomicity: DB + message cùng transaction
- Reliability: Message không bị mất khi publish fail
- Eventual consistency: Message sẽ được gửi eventually

---

## 5. Saga Timeout

### Vấn đề

Invoice service down → saga stuck ở `Validating` state mãi mãi.

### Giải pháp

✅ **Saga Timeout Schedule** (60 giây)

```csharp
// PaymentSaga.cs
Schedule(() => ValidationTimeout, x => x.ValidationTimeoutTokenId, s =>
{
    s.Delay = TimeSpan.FromSeconds(60);
    s.Received = r => r.CorrelateById(ctx => ctx.Message.PaymentId);
});
```

✅ **Reliable Persistence**
Saga repository sử dụng `ExistingDbContext` để dùng chung transaction với business logic:

```csharp
r.ExistingDbContext<AppDbContext>();
```

*(Tránh sử dụng `AddDbContext<DbContext, AppDbContext>` để không gặp lỗi khởi tạo abstract class).*

**Flow:**

```
Payment Initiated → Schedule timeout (60s)
                 ↓
         Invoice validates
                 ↓
    ✅ OK → Unschedule timeout → Confirm
    ❌ FAIL → Unschedule timeout → Reject
    ⏱️ TIMEOUT → Auto reject với reason "timeout"
```

**States:**

- `Validating` → `Confirmed` (happy path)
- `Validating` → `Rejected` (validation failed)
- `Validating` → `TimedOut` (timeout after 60s)

---

## 6. Client Polling Guidance

### Vấn đề

Client poll quá nhanh (mỗi 1s) → API overload.

### Giải pháp

✅ **TTL + Exponential Backoff**

```json
GET /api/v1/payment/{id}
Response:
{
  "paymentId": "...",
  "status": "Processing",
  "expiresIn": 45,      // Còn 45 giây trước timeout
  "retryAfter": 5       // Nên đợi 5 giây trước khi poll lại
}
```

**Exponential Backoff:**

- 0-10s: `retryAfter = 2s`
- 10-30s: `retryAfter = 5s`
- 30-60s: `retryAfter = 10s`
- `expiresIn <= 0`: Stop polling

**Client implementation:**

```typescript
async function pollPaymentStatus(paymentId: string): Promise<PaymentResult> {
  while (true) {
    const response = await fetch(`/api/v1/payment/${paymentId}`);
    const payment = await response.json();
    
    if (payment.status !== 'Processing') {
      return payment; // Completed or Failed
    }
    
    if (payment.expiresIn <= 0) {
      throw new Error('Payment timeout');
    }
    
    // Exponential backoff
    await sleep(payment.retryAfter * 1000);
  }
}
```

---

## 7. Reconciliation Job

### Vấn đề

Saga timeout không fire, event bị mất → payment stuck ở `Processing` mãi.

### Giải pháp

✅ **Background Reconciliation Service**

```csharp
// PaymentReconciliationService.cs
// Chạy mỗi 5 phút
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        await ReconcileStuckPaymentsAsync(stoppingToken);
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
    }
}
```

**Logic:**

```sql
SELECT * FROM Payments
WHERE Status = 'Processing'
  AND PaymentDate < DATEADD(MINUTE, -5, GETUTCDATE())
```

**Action:**

- Mark payment as `Failed`
- Set `FailureReason = "Payment stuck in Processing state. Auto-failed by reconciliation job."`
- Log warning với PaymentId, InvoiceId, Age

**Đây là safety net cuối cùng** khi tất cả mechanisms khác fail.

---

## 8. Monitoring & Alerting

### Metrics cần track

**Payment Service:**

- `payment_processing_duration_seconds` - histogram
- `payment_status_total{status="Processing|Completed|Failed"}` - counter
- `payment_reconciliation_auto_failed_total` - counter

**Saga Orchestrator:**

- `saga_state_total{state="Validating|Confirmed|Rejected|TimedOut"}` - counter
- `saga_timeout_total` - counter
- `saga_duration_seconds` - histogram

**RabbitMQ:**

- Queue depth: `payment-confirm`, `invoice-validate`, `orchestration-payment-saga`
- Dead letter queue depth: `*_error` queues
- Message rate: publish/consume per second

### Alerts

**Critical:**

```yaml
- alert: PaymentStuckInProcessing
  expr: count(payment_status{status="Processing"}) > 100
  for: 5m
  annotations:
    summary: "Too many payments stuck in Processing state"

- alert: SagaTimeoutRateHigh
  expr: rate(saga_timeout_total[5m]) > 0.1
  for: 5m
  annotations:
    summary: "Saga timeout rate > 10%"

- alert: DeadLetterQueueNotEmpty
  expr: rabbitmq_queue_messages{queue=~".*_error"} > 0
  for: 10m
  annotations:
    summary: "Messages in dead letter queue"
```

**Warning:**

```yaml
- alert: ReconciliationJobAutoFailedPayments
  expr: rate(payment_reconciliation_auto_failed_total[1h]) > 0
  annotations:
    summary: "Reconciliation job is auto-failing payments"
```

---

## 9. Troubleshooting Playbook

### Scenario 1: Payment stuck ở Processing

**Symptoms:**

- Client poll mãi không thấy Completed/Failed
- `expiresIn` đã về 0

**Debug steps:**

1. Check payment trong DB:

   ```sql
   SELECT * FROM Payments WHERE Id = '{paymentId}'
   ```

2. Check saga state:

   ```sql
   SELECT * FROM PaymentSagaStates WHERE PaymentId = '{paymentId}'
   ```

3. Check RabbitMQ queues:
   - `invoice-validate`: có message pending không?
   - `payment-confirm`: có message pending không?
4. Check logs với CorrelationId:

   ```logql
   {service=~"payment-api|invoice-api|orchestration-api"} | json | CorrelationId="{id}"
   ```

**Resolution:**

- Nếu saga ở `Validating` > 60s → saga timeout sẽ auto reject
- Nếu saga không tồn tại → manually publish `IPaymentInitiatedEvent`
- Nếu reconciliation job chưa chạy → đợi 5 phút hoặc manually mark Failed

### Scenario 2: Dead Letter Queue có messages

**Symptoms:**

- Alert: `DeadLetterQueueNotEmpty`
- Messages trong `*_error` queues

**Debug steps:**

1. Check message content trong RabbitMQ Management UI
2. Check consumer logs để tìm exception
3. Identify root cause: bug, data issue, external service down

**Resolution:**

- Fix bug/data issue
- Redeploy consumer
- Manually reprocess messages từ DLQ:

  ```bash
  # Move messages từ DLQ về main queue
  rabbitmqadmin get queue=payment-confirm_error count=100 requeue=true
  ```

### Scenario 3: Saga timeout rate cao

**Symptoms:**

- Alert: `SagaTimeoutRateHigh`
- Nhiều payments Failed với reason "timeout"

**Debug steps:**

1. Check Invoice service health: `/health`
2. Check Invoice service logs: có errors không?
3. Check RabbitMQ: `invoice-validate` queue có backlog không?
4. Check network latency giữa services

**Resolution:**

- Scale Invoice service nếu overload
- Fix bugs trong Invoice consumer
- Tăng saga timeout nếu cần (hiện tại 60s)

---

## 10. Configuration

### Environment Variables

```yaml
# appsettings.json
{
  "RabbitMQ": {
    "Host": "rabbitmq",
    "Username": "guest",
    "Password": "guest"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=sqlserver;Database=PaymentDb;..."
  },
  "Saga": {
    "TimeoutSeconds": 60,
    "ReconciliationIntervalMinutes": 5,
    "ReconciliationThresholdMinutes": 5
  }
}
```

### Tuning Parameters

| Parameter | Default | Recommended | Notes |
|-----------|---------|-------------|-------|
| Saga Timeout | 60s | 30-120s | Tùy Invoice service latency |
| Message Retry Intervals | 5s, 10s, 30s | - | Exponential backoff |
| Reconciliation Interval | 5 min | 5-10 min | Không nên quá thường xuyên |
| Reconciliation Threshold | 5 min | 5-10 min | Phải > Saga Timeout |
| Idempotency Cache TTL | 30 min | 30-60 min | Đủ lâu cho client retry |
| DLQ Message TTL | 7 days | 7-30 days | Đủ thời gian để investigate |

---

## 11. Testing Guardrails

### Test Idempotency

```bash
# Gửi cùng request 2 lần với cùng idempotency key
curl -X POST http://localhost:5001/api/v1/payment/pay \
  -H "X-Idempotency-Key: test-001" \
  -d '{"invoiceId": "...", "amount": 1500}'

# Response 1: 202 Accepted, paymentId = abc
# Response 2: 202 Accepted, paymentId = abc (same)

# Verify: chỉ có 1 payment trong DB
```

### Test Message Retry

```bash
# 1. Stop Invoice service
docker-compose stop invoice-api

# 2. Initiate payment
curl -X POST http://localhost:5001/api/v1/payment/pay ...

# 3. Check RabbitMQ: invoice-validate queue có message
# 4. Check logs: thấy retry attempts (5s, 10s, 30s)
# 5. Start Invoice service
docker-compose start invoice-api

# 6. Message được consume thành công
```

### Test Saga Timeout

```bash
# 1. Stop Invoice service
docker-compose stop invoice-api

# 2. Initiate payment
curl -X POST http://localhost:5001/api/v1/payment/pay ...

# 3. Đợi 60 giây
sleep 60

# 4. Poll payment status
curl http://localhost:5001/api/v1/payment/{id}

# Response: status = "Failed", failureReason = "Invoice validation timeout after 60 seconds."
```

### Test Reconciliation Job

```bash
# 1. Manually insert stuck payment vào DB
INSERT INTO Payments (Id, InvoiceId, Amount, Status, PaymentDate)
VALUES (NEWID(), '...', 1500, 0, DATEADD(MINUTE, -10, GETUTCDATE()))

# 2. Đợi reconciliation job chạy (5 phút)
# Hoặc restart Payment service để trigger ngay

# 3. Check payment status → Failed
# 4. Check logs: "Auto-failed stuck payment"
```

### Test Dead Letter Queue

```bash
# 1. Inject bug vào consumer (throw exception)
# 2. Send message
# 3. Check logs: retry 3 lần
# 4. Check RabbitMQ: message vào DLQ
# 5. Fix bug, redeploy
# 6. Reprocess message từ DLQ
```

---

## 12. Summary

| Guardrail | Status | Purpose |
|-----------|--------|---------|
| ✅ Idempotency | Implemented | Prevent duplicate payments |
| ✅ Message Retry | Implemented | Handle transient failures |
| ✅ Dead Letter Queue | Implemented | Capture failed messages |
| ✅ Queue Durability | Implemented | Survive broker restart |
| ✅ Outbox Pattern | Implemented | Ensure message delivery |
| ✅ Saga Timeout | Implemented | Prevent stuck sagas |
| ✅ TTL + Backoff | Implemented | Guide client polling |
| ✅ Reconciliation Job | Implemented | Safety net for stuck payments |

**Hệ thống giờ đã production-ready với:**

- ✅ No message loss
- ✅ No stuck payments
- ✅ No duplicate payments
- ✅ Graceful degradation
- ✅ Observable & debuggable
