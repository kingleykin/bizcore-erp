# Saga Orchestrator Pattern - Payment Flow

## Tổng quan

Hệ thống đã được chuyển từ **Request-Reply pattern** (synchronous, blocking) sang **Saga Orchestrator pattern** (asynchronous, non-blocking) để xử lý payment flow.

### Kiến trúc cũ (Request-Reply)

```
Client → POST /payment/pay
           ↓ [BLOCKING]
    Payment service → IRequestClient → Invoice service
           ↓ [WAIT 30s]
    Invoice validates + marks Paid
           ↓
    Payment commits
           ↓
    Client nhận: 200 OK / 400 Bad Request
```

**Vấn đề:**
- Tight coupling: Payment phụ thuộc trực tiếp vào Invoice
- Blocking: Client chờ đến khi Invoice xử lý xong
- Single point of failure: Invoice down → Payment API bị block
- Không có compensation tự động

### Kiến trúc mới (Saga Orchestrator)

```
Client → POST /payment/pay
           ↓
    Payment: tạo Payment{Status=Processing}, publish IPaymentInitiatedEvent
    → trả ngay: 202 Accepted { paymentId, status: "Processing" }
           ↓ [ASYNC]
    Saga Orchestrator nhận IPaymentInitiatedEvent
           ↓
    Saga → send IValidateInvoiceCommand → Invoice service
           ↓
    Invoice validates → publish IInvoiceValidated / IInvoiceValidationFailed
           ↓
    Saga nhận kết quả:
      ✅ OK  → send IConfirmPaymentCommand → Payment{Status=Completed}
      ❌ FAIL → send IRejectPaymentCommand → Payment{Status=Failed}
           ↓
    Client poll: GET /payment/{id} → lấy trạng thái cuối
```

**Ưu điểm:**
- Loose coupling: Services giao tiếp qua events/commands
- Non-blocking: Client nhận response ngay, không chờ validation
- Resilient: Invoice down không ảnh hưởng đến Payment API
- Saga quản lý compensation tự động
- Dễ mở rộng: thêm steps mới vào saga không ảnh hưởng services khác

---

## Flow chi tiết

### 1. Client gửi payment request

```http
POST /api/v1/payment/pay
Headers:
  X-Idempotency-Key: unique-key-123
  X-Correlation-ID: trace-abc-456 (optional, auto-generated nếu không có)
Body:
{
  "invoiceId": "f1d2c3b4-a5e6-4d7f-8e9a-0b1c2d3e4f5a",
  "amount": 1500
}
```

**Response: 202 Accepted**
```json
{
  "paymentId": "9e8d7c6b-5a4b-3c2d-1e0f-9a8b7c6d5e4f",
  "status": "Processing",
  "message": "Payment is being processed. Poll this endpoint to get the final status."
}
```

### 2. Payment service

- Tạo `Payment` record với `Status = Processing`
- Publish `IPaymentInitiatedEvent` lên RabbitMQ
- Trả 202 Accepted cho client ngay lập tức

### 3. Saga Orchestrator

**State Machine:**
```
Initial → Validating → Confirmed / Rejected → Final
```

**Saga nhận `IPaymentInitiatedEvent`:**
- Tạo saga instance với `CorrelationId = PaymentId`
- Chuyển state → `Validating`
- Send `IValidateInvoiceCommand` → `invoice-validate` queue

### 4. Invoice service

**Consumer nhận `IValidateInvoiceCommand`:**
- Validate invoice (tồn tại, status, amount match)
- Nếu OK:
  - Cập nhật `Invoice.Status = Paid`
  - Publish `IInvoiceValidatedEvent`
- Nếu FAIL:
  - Publish `IInvoiceValidationFailedEvent` (kèm reason)

### 5. Saga Orchestrator (tiếp)

**Happy path - nhận `IInvoiceValidatedEvent`:**
- Chuyển state → `Confirmed`
- Send `IConfirmPaymentCommand` → `payment-confirm` queue

**Failure path - nhận `IInvoiceValidationFailedEvent`:**
- Chuyển state → `Rejected`
- Send `IRejectPaymentCommand` → `payment-reject` queue

### 6. Payment service (finalize)
Có — **nếu không thiết kế guardrail**, bạn sẽ dính đúng 2 vấn đề đó:

1. client retry/polling quá nhiều → quá tải
2. event thất lạc/consumer lỗi → payment “Processing” mãi

Nhưng đây là bài toán quen thuộc của distributed system, có cách khóa lại rất rõ ràng.

---

# 🎯 1. Vấn đề Polly / client retry quá nhiều

## ❗ Nguy cơ

* Client retry liên tục (timeout → retry)
* API bị spam
* Có thể tạo duplicate request

---

## 🚀 Cách xử lý chuẩn

### 🔥 A. Idempotency (bạn đã làm — rất tốt)

```csharp id="4e4wqv"
if (_cache.TryGetValue(idempotencyKey, out _))
```

👉 Đây là lớp bảo vệ số 1

---

### 🔥 B. Retry phải có giới hạn + backoff

Dùng Polly:

```csharp id="0k3y6k"
WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
```

👉 Không retry vô hạn

---

### 🔥 C. Client KHÔNG poll quá nhanh

❌ Sai:

```text
GET /payment/{id} mỗi 1s
```

✔ Đúng:

```text
2s → 5s → 10s → stop
```

---

### 🔥 D. Trả thêm TTL cho client

```json id="q8z1d3"
{
  "paymentId": "...",
  "status": "Processing",
  "expiresIn": 30
}
```

👉 Client biết khi nào nên dừng

---

# 🚨 2. Event bị mất → Processing mãi

## ❗ Đây là vấn đề NGHIÊM TRỌNG nhất

Nguyên nhân có thể:

* Consumer crash
* Message không được ack
* Queue misconfig
* Network issue

---

# 🚀 Cách xử lý chuẩn (bắt buộc)

## 🔥 A. Outbox Pattern (QUAN TRỌNG NHẤT)

👉 Khi publish event:

* Lưu DB
* Sau đó mới gửi message

---

👉 MassTransit hỗ trợ:

```csharp id="f8dcsx"
cfg.UseEntityFrameworkOutbox<AppDbContext>();
```

---

## 🔥 B. Message durability

Trong RabbitMQ:

* Queue durable
* Message persistent

---

## 🔥 C. Retry + Dead Letter Queue

```csharp id="0q5h2b"
cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
```

👉 Nếu fail:

* Đẩy vào DLQ
* Không mất message

---

## 🔥 D. Saga timeout handling

👉 Không để Processing vô hạn

```text
Processing → Timeout → Failed
```

---

## 🔥 E. Reconciliation job (rất thực tế)

Cron job:

```text
scan payment:
  status = Processing
  created > 5 phút
→ mark Failed hoặc retry
```

---

# 🎯 3. Kiến trúc chống “Processing mãi”

## Flow chuẩn:

```text id="r1w2hj"
Payment → Processing
        ↓
Saga
   ↓ OK → Completed
   ↓ FAIL → Failed
   ↓ timeout → Timeout
        ↓
Reconciliation job (fallback)
```

---

# 🧠 Insight quan trọng

> **Trong distributed system: “eventually consistent” phải có “eventually resolved”**

---

# ⚖️ Tổng hợp rủi ro & giải pháp

| Vấn đề            | Giải pháp              |
| ----------------- | ---------------------- |
| Retry quá nhiều   | Backoff + limit        |
| Duplicate request | Idempotency            |
| Event mất         | Outbox + durable queue |
| Consumer fail     | Retry + DLQ            |
| Processing mãi    | Timeout + cron job     |

---

# 🎬 Khi demo bạn nói gì

> “Hệ thống sử dụng outbox pattern, retry policy và reconciliation job để đảm bảo không có trạng thái xử lý bị treo vô hạn trong môi trường distributed.”

👉 Đây là câu “production-ready”

---

# 💥 Chốt hạ

## Có vấn đề không?

👉 Có — nếu không xử lý

---

## Có giải quyết được không?

👉 Có — và đã có pattern chuẩn:

* ✔ Outbox
* ✔ Retry
* ✔ Timeout
* ✔ Reconciliation

---

# 🔥 Một câu để nhớ

> **Distributed system không sợ fail — chỉ sợ không recover**

---

Nếu bạn muốn, tôi có thể:

* Viết luôn config MassTransit chuẩn (Outbox + Retry + DLQ)
* Hoặc code job xử lý “Processing stuck”
* Hoặc vẽ flow failure handling để bạn demo cực mạnh

Chỉ cần nói, tôi giúp bạn biến hệ thống này thành “đủ chuẩn production mindset”.

**ConfirmPaymentConsumer nhận `IConfirmPaymentCommand`:**
- Cập nhật `Payment.Status = Completed`
- Publish `IPaymentConfirmedEvent` (để Saga finalize)
- Publish `IPaymentCompletedEvent` (legacy, cho Report service)

**RejectPaymentConsumer nhận `IRejectPaymentCommand`:**
- Cập nhật `Payment.Status = Failed`, `FailureReason = reason`
- Publish `IPaymentRejectedEvent` (để Saga finalize)

### 7. Client poll trạng thái

```http
GET /api/v1/payment/{paymentId}
```

**Response khi đang xử lý:**
```json
{
  "paymentId": "9e8d7c6b-5a4b-3c2d-1e0f-9a8b7c6d5e4f",
  "invoiceId": "f1d2c3b4-a5e6-4d7f-8e9a-0b1c2d3e4f5a",
  "amount": 1500,
  "status": "Processing",
  "paymentDate": "2026-05-06T10:30:00Z",
  "failureReason": null
}
```

**Response khi thành công:**
```json
{
  "paymentId": "9e8d7c6b-5a4b-3c2d-1e0f-9a8b7c6d5e4f",
  "invoiceId": "f1d2c3b4-a5e6-4d7f-8e9a-0b1c2d3e4f5a",
  "amount": 1500,
  "status": "Completed",
  "paymentDate": "2026-05-06T10:30:00Z",
  "failureReason": null
}
```

**Response khi thất bại:**
```json
{
  "paymentId": "9e8d7c6b-5a4b-3c2d-1e0f-9a8b7c6d5e4f",
  "invoiceId": "f1d2c3b4-a5e6-4d7f-8e9a-0b1c2d3e4f5a",
  "amount": 1500,
  "status": "Failed",
  "paymentDate": "2026-05-06T10:30:00Z",
  "failureReason": "Invoice is already paid."
}
```

---

## Contracts mới

### Events

| Event | Publisher | Subscribers | Mục đích |
|-------|-----------|-------------|----------|
| `IPaymentInitiatedEvent` | Payment service | Saga orchestrator | Bắt đầu saga flow |
| `IInvoiceValidatedEvent` | Invoice service | Saga orchestrator | Invoice hợp lệ, saga confirm payment |
| `IInvoiceValidationFailedEvent` | Invoice service | Saga orchestrator | Invoice không hợp lệ, saga reject payment |
| `IPaymentConfirmedEvent` | Payment service | Saga orchestrator | Payment đã confirmed, saga finalize |
| `IPaymentRejectedEvent` | Payment service | Saga orchestrator | Payment đã rejected, saga finalize |

### Commands

| Command | Sender | Receiver | Mục đích |
|---------|--------|----------|----------|
| `IValidateInvoiceCommand` | Saga orchestrator | Invoice service | Yêu cầu validate invoice |
| `IConfirmPaymentCommand` | Saga orchestrator | Payment service | Yêu cầu confirm payment |
| `IRejectPaymentCommand` | Saga orchestrator | Payment service | Yêu cầu reject payment |

---

## Payment Status

| Status | Mô tả |
|--------|-------|
| `Processing` | Payment đã được tạo, đang chờ Saga validate invoice |
| `Completed` | Saga đã confirm: invoice hợp lệ, payment hoàn tất |
| `Failed` | Saga đã reject: invoice validation failed |
| `Reversed` | Compensation: payment bị đảo ngược sau khi đã Completed (legacy) |

---

## RabbitMQ Queues

### Payment Service
- `payment-confirm` - nhận `IConfirmPaymentCommand` từ Saga
- `payment-reject` - nhận `IRejectPaymentCommand` từ Saga
- `payment-compensation-requested` - legacy compensation
- `payment-invoice-created` - sync invoice read model

### Invoice Service
- `invoice-validate` - nhận `IValidateInvoiceCommand` từ Saga
- `invoice-apply-payment` - legacy Request-Reply endpoint (giữ lại cho tests)

### Orchestration Service
- `orchestration-payment-saga` - Saga state machine endpoint
- `orchestration-invoice-created` - legacy event observer
- `orchestration-payment-completed` - legacy event observer
- `orchestration-payment-compensation-requested` - legacy event observer

---

## Tracing với CorrelationId

Tất cả events/commands đều có `X-Correlation-ID` header được propagate tự động qua:
- `CorrelationIdMiddleware` (HTTP → HttpContext.Items)
- `CorrelationIdSendFilter` (HttpContext → MassTransit SendContext)
- `CorrelationIdPublishFilter` (HttpContext → MassTransit PublishContext)
- `CorrelationIdConsumeFilter` (MassTransit headers → Serilog LogContext)

**Query logs trong Grafana Loki:**
```logql
{service="payment-api"} | json | CorrelationId="trace-abc-456"
{service="invoice-api"} | json | CorrelationId="trace-abc-456"
{service="orchestration-api"} | json | CorrelationId="trace-abc-456"
```

---

## Testing

### Happy Path

```bash
# 1. Tạo payment
curl -X POST http://localhost:5000/api/v1/payment/pay \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: test-key-001" \
  -H "X-Correlation-ID: trace-test-001" \
  -d '{
    "invoiceId": "f1d2c3b4-a5e6-4d7f-8e9a-0b1c2d3e4f5a",
    "amount": 1500
  }'

# Response: 202 Accepted
# { "paymentId": "...", "status": "Processing", ... }

# 2. Poll trạng thái (sau 1-2 giây)
curl http://localhost:5000/api/v1/payment/{paymentId}

# Response: 200 OK
# { "paymentId": "...", "status": "Completed", ... }
```

### Failure Path - Invoice không tồn tại

```bash
curl -X POST http://localhost:5000/api/v1/payment/pay \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: test-key-002" \
  -d '{
    "invoiceId": "00000000-0000-0000-0000-000000000000",
    "amount": 1500
  }'

# Poll sau 1-2 giây:
# { "paymentId": "...", "status": "Failed", "failureReason": "Invoice not found." }
```

### Failure Path - Amount mismatch

```bash
curl -X POST http://localhost:5000/api/v1/payment/pay \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: test-key-003" \
  -d '{
    "invoiceId": "f1d2c3b4-a5e6-4d7f-8e9a-0b1c2d3e4f5a",
    "amount": 9999
  }'

# Poll sau 1-2 giây:
# { "paymentId": "...", "status": "Failed", "failureReason": "Amount mismatch: expected 1500, got 9999." }
```

---

## Migration từ Request-Reply

### Backward Compatibility

Hệ thống giữ lại các endpoints/consumers cũ để không break existing tests:
- `ApplyPaymentToInvoiceConsumer` (Invoice service) - vẫn hoạt động cho Request-Reply
- `IApplyPaymentToInvoiceRequest/Response` contracts - vẫn tồn tại

### Deprecation Plan

1. **Phase 1** (hiện tại): Cả 2 patterns hoạt động song song
2. **Phase 2**: Update tests để dùng Saga pattern
3. **Phase 3**: Xóa Request-Reply code:
   - Xóa `ApplyPaymentToInvoiceConsumer`
   - Xóa `IApplyPaymentToInvoiceRequest/Response`
   - Xóa `invoice-apply-payment` queue config

---

## Monitoring

### Saga State

Query saga state trong database:
```sql
SELECT 
    CorrelationId,
    CurrentState,
    PaymentId,
    InvoiceId,
    Amount,
    FailureReason,
    CreatedAt,
    UpdatedAt
FROM PaymentSagaStates
WHERE CurrentState != 'Final'
ORDER BY CreatedAt DESC;
```

### Metrics

- **Payment Processing Time**: từ `IPaymentInitiatedEvent` đến `IPaymentConfirmedEvent`
- **Saga Success Rate**: % saga kết thúc ở `Confirmed` state
- **Saga Failure Rate**: % saga kết thúc ở `Rejected` state
- **Stuck Sagas**: saga ở `Validating` state quá 30 giây

---

## Troubleshooting

### Payment stuck ở Processing

**Nguyên nhân:**
- Invoice service down
- RabbitMQ connection issue
- Saga orchestrator down

**Giải pháp:**
1. Check logs với CorrelationId
2. Check RabbitMQ queues: `invoice-validate` có message pending không?
3. Check saga state trong database
4. Restart services nếu cần (saga sẽ resume từ state cuối)

### Invoice đã Paid nhưng Payment vẫn Processing

**Nguyên nhân:**
- `IInvoiceValidatedEvent` không được publish
- Saga không nhận được event

**Giải pháp:**
1. Check Invoice service logs
2. Check RabbitMQ: `orchestration-payment-saga` queue có message không?
3. Manually publish `IInvoiceValidatedEvent` để trigger saga

### Saga bị duplicate

**Nguyên nhân:**
- `IPaymentInitiatedEvent` được publish 2 lần
- Idempotency key không hoạt động

**Giải pháp:**
1. Check Payment service logs: có duplicate request không?
2. Check saga state: có 2 saga với cùng PaymentId không?
3. Manually finalize saga thừa

---

## Best Practices

### Client Implementation

```typescript
async function processPayment(invoiceId: string, amount: number): Promise<PaymentResult> {
  const idempotencyKey = generateUniqueKey();
  
  // 1. Initiate payment
  const response = await fetch('/api/v1/payment/pay', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Idempotency-Key': idempotencyKey
    },
    body: JSON.stringify({ invoiceId, amount })
  });
  
  if (response.status !== 202) {
    throw new Error('Payment initiation failed');
  }
  
  const { paymentId } = await response.json();
  
  // 2. Poll trạng thái
  return await pollPaymentStatus(paymentId);
}

async function pollPaymentStatus(paymentId: string, maxAttempts = 30): Promise<PaymentResult> {
  for (let i = 0; i < maxAttempts; i++) {
    const response = await fetch(`/api/v1/payment/${paymentId}`);
    const payment = await response.json();
    
    if (payment.status === 'Completed') {
      return { success: true, payment };
    }
    
    if (payment.status === 'Failed') {
      return { success: false, reason: payment.failureReason };
    }
    
    // Still processing, wait 1 second
    await sleep(1000);
  }
  
  throw new Error('Payment processing timeout');
}
```

### Idempotency

- **LUÔN** gửi `X-Idempotency-Key` header
- Key phải unique per request (UUID recommended)
- Retry với cùng key sẽ trả về cùng `paymentId`

### Timeout

- Saga timeout: 30 giây (configurable)
- Client poll timeout: 30 lần × 1 giây = 30 giây
- Nếu timeout, check logs và saga state để debug

---

## References

- [MassTransit Saga Documentation](https://masstransit.io/documentation/patterns/saga)
- [Saga Pattern - Microsoft](https://learn.microsoft.com/en-us/azure/architecture/reference-architectures/saga/saga)
- [Event-Driven Architecture](https://martinfowler.com/articles/201701-event-driven.html)
