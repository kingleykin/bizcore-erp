# Idempotency Design - Production Implementation

## Tổng quan

Document này mô tả thiết kế và implementation của Idempotency pattern trong hệ thống, đảm bảo:
- ✅ Không có duplicate payments
- ✅ Thread-safe (race condition handling)
- ✅ Persistent across service restarts
- ✅ Works với multiple instances (load balancer)
- ✅ Request payload validation

---

## 1. Kiến trúc

### Database-backed Idempotency

```
┌─────────────────────────────────────────────────────────────┐
│                  Idempotency Flow                           │
└─────────────────────────────────────────────────────────────┘

Client Request (X-Idempotency-Key: "key-123")
    ↓
[Controller] Validate header
    ↓
[PaymentService] Call IdempotencyService
    ↓
[IdempotencyService] Check DB (IdempotencyRecords table)
    ├─ EXISTS → Return existing PaymentId
    │   ├─ Expired? → Reuse only if operation is terminal; otherwise keep/reconcile
    │   └─ Hash mismatch? → Return conflict error
    └─ NOT EXISTS → Insert record + Return new PaymentId
    ↓
[PaymentService] Create Payment + Publish event
```

### Database Schema

```sql
CREATE TABLE IdempotencyRecords (
    [Key] NVARCHAR(256) PRIMARY KEY,
    PaymentId UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    ExpiresAt DATETIME2 NOT NULL,
    RequestHash NVARCHAR(64) NULL,
    Status NVARCHAR(32) NOT NULL, -- InProgress, Completed, Failed, Expired
    ResponseJson NVARCHAR(MAX) NULL,
    StatusCode INT NULL,
    INDEX IX_IdempotencyRecords_ExpiresAt (ExpiresAt),
    INDEX IX_IdempotencyRecords_PaymentId (PaymentId)
);

CREATE UNIQUE INDEX UX_Payments_IdempotencyKey
ON Payments (IdempotencyKey)
WHERE IdempotencyKey IS NOT NULL;
```

---

## 2. Components

### IdempotencyRecord Entity

```csharp
public class IdempotencyRecord
{
    public string Key { get; set; }              // Unique, max 256 chars
    public Guid PaymentId { get; set; }          // Associated payment
    public DateTime CreatedAt { get; set; }      // Creation timestamp
    public DateTime ExpiresAt { get; set; }      // TTL expiration
    public string? RequestHash { get; set; }     // SHA256 of request payload
    public string Status { get; set; }           // InProgress, Completed, Failed, Expired
    public string? ResponseJson { get; set; }    // Cached response for replay
    public int? StatusCode { get; set; }         // Original HTTP status code
}
```

**Purpose:**
- `Key`: Idempotency key từ client (unique constraint)
- `PaymentId`: Payment đã được tạo cho key này
- `ExpiresAt`: TTL để cleanup (default 30 phút)
- `RequestHash`: Verify request consistency (same key, same payload)
- `Status`: Theo dõi request đang xử lý, đã hoàn tất, thất bại, hoặc hết hạn
- `ResponseJson`/`StatusCode`: Replay đúng response cho duplicate request

### IdempotencyService

**Interface:**
```csharp
public interface IIdempotencyService
{
    Task<IdempotencyCheckResult> CheckOrCreateAsync(
        string idempotencyKey,
        object requestPayload,
        Guid paymentId,
        TimeSpan ttl);

    Task<int> CleanupExpiredRecordsAsync(CancellationToken cancellationToken);

    Task CacheResponseAsync(
        string idempotencyKey,
        object response,
        int statusCode,
        CancellationToken cancellationToken);
}
```

**Key features:**
- ✅ Database-backed (persistent)
- ✅ Race condition handling (unique constraint + catch)
- ✅ Request payload validation (SHA256 hash)
- ✅ TTL support
- ✅ Response replay
- ✅ In-progress duplicate handling
- ✅ Automatic cleanup

---

## 3. Flow chi tiết

### Happy Path - First Request

```
1. Client → POST /pay
   Headers: X-Idempotency-Key: "payment-inv-123-20260506"
   Body: { "invoiceId": "123", "amount": 1500 }

2. PaymentService.InitiatePaymentAsync()
   - Generate PaymentId = "abc-def-..."
   - Call IdempotencyService.CheckOrCreateAsync()

3. IdempotencyService
   - Compute RequestHash = SHA256({"invoiceId":"123","amount":1500})
   - Query DB: SELECT * FROM IdempotencyRecords WHERE Key = "payment-inv-123-20260506"
   - Result: NOT FOUND

4. IdempotencyService
   - INSERT INTO IdempotencyRecords (Key, PaymentId, RequestHash, CreatedAt, ExpiresAt)
   - Return: IsNew = true, PaymentId = "abc-def-..."

5. PaymentService
   - Create Payment entity (Status = Processing)
   - SaveChanges()
   - Publish IPaymentInitiatedEvent

6. Response → 202 Accepted
   { "paymentId": "abc-def-...", "status": "Processing" }
```

### Duplicate Request - Same Key, Same Payload

```
1. Client → POST /pay (retry after 5s)
   Headers: X-Idempotency-Key: "payment-inv-123-20260506" ← SAME
   Body: { "invoiceId": "123", "amount": 1500 }           ← SAME

2. IdempotencyService
   - Compute RequestHash = SHA256({"invoiceId":"123","amount":1500})
   - Query DB: SELECT * FROM IdempotencyRecords WHERE Key = "payment-inv-123-20260506"
   - Result: FOUND (PaymentId = "abc-def-...", RequestHash matches)

3. IdempotencyService
   - Check ExpiresAt > NOW → Valid
   - Check RequestHash == computed hash → Match
   - Return: IsNew = false, PaymentId = "abc-def-..."

4. PaymentService
   - Skip payment creation
   - Return existing PaymentId

5. Response → 202 Accepted
   { "paymentId": "abc-def-...", "status": "Processing" } ← SAME PaymentId
```

### Conflict - Same Key, Different Payload

```
1. Client → POST /pay
   Headers: X-Idempotency-Key: "payment-inv-123-20260506" ← SAME
   Body: { "invoiceId": "123", "amount": 2000 }           ← DIFFERENT

2. IdempotencyService
   - Compute RequestHash = SHA256({"invoiceId":"123","amount":2000})
   - Query DB: SELECT * FROM IdempotencyRecords WHERE Key = "payment-inv-123-20260506"
   - Result: FOUND (PaymentId = "abc-def-...", RequestHash = "old-hash")

3. IdempotencyService
   - Check RequestHash != computed hash → MISMATCH
   - Return: IsNew = false, ConflictReason = "Idempotency key already used with different request payload"

4. PaymentService
   - Return error

5. Response → 400 Bad Request
   { "error": "Idempotency key already used with different request payload" }
```

### Race Condition Handling

```
Time    Thread 1                                Thread 2
----    --------                                --------
T1      Query DB → NOT FOUND
T2                                              Query DB → NOT FOUND
T3      INSERT IdempotencyRecord
T4                                              INSERT IdempotencyRecord → FAIL (unique constraint)
T5      Return IsNew = true
T6                                              Catch DbUpdateException
T7                                              Re-query DB → FOUND (Thread 1's record)
T8                                              Return IsNew = false, PaymentId from Thread 1
```

**Result:**
- ✅ Only 1 payment created (Thread 1)
- ✅ Thread 2 gets existing PaymentId
- ✅ No duplicate payments

---

## 4. Request Hash Validation

### Purpose
Đảm bảo cùng idempotency key phải có cùng request payload.

### Implementation

```csharp
private static string ComputeRequestHash(object payload)
{
    var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    });

    var bytes = Encoding.UTF8.GetBytes(json);
    var hash = SHA256.HashData(bytes);
    return Convert.ToHexString(hash);
}
```

**Payload for hash:**
```csharp
new { payment.InvoiceId, payment.Amount }
```

**Example:**
```
Input: { "invoiceId": "123", "amount": 1500 }
JSON: {"invoiceId":"123","amount":1500}
SHA256: A1B2C3D4E5F6...
```

### Why SHA256?
- Fast (< 1ms)
- Collision-resistant
- Fixed length (64 hex chars)
- Standard library support

---

## 5. TTL & Cleanup

### TTL (Time To Live)

**Default:** 30 phút

```csharp
ExpiresAt = DateTime.UtcNow.Add(TimeSpan.FromMinutes(30))
```

**Rationale:**
- Đủ lâu cho client retry (network timeout, user retry)
- Không quá lâu (tránh table phình to)
- Có thể config per environment

### Cleanup Job

**IdempotencyCleanupService:**
- Chạy mỗi 1 giờ
- Xóa records có `ExpiresAt < NOW`
- Background service (không block requests)

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Initial delay
    
    while (!stoppingToken.IsCancellationRequested)
    {
        await CleanupExpiredRecordsAsync(stoppingToken);
        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
    }
}
```

**Query:**
```sql
DELETE FROM IdempotencyRecords
WHERE ExpiresAt < GETUTCDATE()
```

---

## 6. Validation Rules

### Idempotency Key Format

**Rules:**
- ✅ Required (không được null/empty)
- ✅ Max length: 256 characters
- ✅ Recommended format: `{prefix}-{business-id}-{timestamp}`

**Examples:**
```
✅ Good:
- payment-invoice-123-20260506103000
- user-abc-payment-invoice-456
- session-xyz-pay-789

❌ Bad:
- "" (empty)
- "a" (too short, không meaningful)
- "very-long-key-..." (> 256 chars)
```

### Client-side Generation

**Option 1: Business-based (Recommended)**
```typescript
function generateIdempotencyKey(invoiceId: string): string {
  const timestamp = Date.now();
  return `payment-invoice-${invoiceId}-${timestamp}`;
}
```

**Option 2: UUID per operation**
```typescript
function generateIdempotencyKey(): string {
  return `payment-${uuidv4()}`;
}
```

**Option 3: Session + operation**
```typescript
function generateIdempotencyKey(sessionId: string, invoiceId: string): string {
  return `${sessionId}-payment-${invoiceId}`;
}
```

**Storage:**
```typescript
// Store key để retry với cùng key
const key = generateIdempotencyKey(invoiceId);
localStorage.setItem(`payment-${invoiceId}`, key);

// Retry
const storedKey = localStorage.getItem(`payment-${invoiceId}`);
await fetch('/api/v1/payment/pay', {
  headers: { 'X-Idempotency-Key': storedKey }
});
```

---

## 7. Comparison: Old vs New

| Aspect | Old (IMemoryCache) | New (Database) |
|--------|-------------------|----------------|
| **Persistence** | ❌ Lost on restart | ✅ Persistent |
| **Multi-instance** | ❌ Per-instance cache | ✅ Shared DB |
| **Race condition** | ❌ Not handled | ✅ Unique constraint |
| **Payload validation** | ❌ No validation | ✅ SHA256 hash |
| **TTL** | ✅ 30 min | ✅ 30 min |
| **Cleanup** | ✅ Auto (cache eviction) | ✅ Background job |
| **Performance** | ⚡ Very fast (memory) | ⚡ Fast (indexed query) |
| **Scalability** | ❌ Limited | ✅ Scales with DB |

---

## 8. Performance Considerations

### Database Query Performance

**Check query:**
```sql
SELECT Key, PaymentId, ExpiresAt, RequestHash
FROM IdempotencyRecords
WHERE Key = @key
```

**Index:**
```sql
PRIMARY KEY (Key)  -- Clustered index
```

**Performance:**
- Single row lookup by primary key
- O(log n) complexity
- Typical latency: < 5ms

### Optimization Tips

1. **Index on ExpiresAt** (for cleanup):
   ```sql
   CREATE INDEX IX_IdempotencyRecords_ExpiresAt ON IdempotencyRecords(ExpiresAt)
   ```

2. **Partition by date** (for large scale):
   ```sql
   CREATE PARTITION FUNCTION PF_IdempotencyRecords (DATETIME2)
   AS RANGE RIGHT FOR VALUES ('2026-01-01', '2026-02-01', ...)
   ```

3. **Read-through cache** (optional):
   ```csharp
   // Check IMemoryCache first (fast path)
   if (_cache.TryGetValue(key, out Guid cachedPaymentId))
       return new IdempotencyCheckResult(false, cachedPaymentId);
   
   // Fallback to DB (slow path)
   var record = await _context.IdempotencyRecords.FindAsync(key);
   
   // Cache result
   if (record != null)
       _cache.Set(key, record.PaymentId, TimeSpan.FromMinutes(5));
   ```

---

## 9. Testing

### Test Cases

#### 1. First Request
```csharp
[Fact]
public async Task InitiatePayment_FirstRequest_CreatesPayment()
{
    var key = "test-key-001";
    var result = await _paymentService.InitiatePaymentAsync(payment, key);
    
    result.Accepted.Should().BeTrue();
    result.PaymentId.Should().NotBeNull();
    
    // Verify idempotency record created
    var record = await _context.IdempotencyRecords.FindAsync(key);
    record.Should().NotBeNull();
    record.PaymentId.Should().Be(result.PaymentId);
}
```

#### 2. Duplicate Request
```csharp
[Fact]
public async Task InitiatePayment_DuplicateRequest_ReturnsSamePaymentId()
{
    var key = "test-key-002";
    
    // First request
    var result1 = await _paymentService.InitiatePaymentAsync(payment, key);
    
    // Duplicate request
    var result2 = await _paymentService.InitiatePaymentAsync(payment, key);
    
    result1.PaymentId.Should().Be(result2.PaymentId);
    
    // Verify only 1 payment created
    var payments = await _context.Payments.Where(p => p.IdempotencyKey == key).ToListAsync();
    payments.Should().HaveCount(1);
}
```

#### 3. Conflict - Different Payload
```csharp
[Fact]
public async Task InitiatePayment_SameKeyDifferentPayload_ReturnsConflict()
{
    var key = "test-key-003";
    
    // First request
    var payment1 = new Payment { InvoiceId = Guid.NewGuid(), Amount = 1500 };
    var result1 = await _paymentService.InitiatePaymentAsync(payment1, key);
    result1.Accepted.Should().BeTrue();
    
    // Second request with different payload
    var payment2 = new Payment { InvoiceId = Guid.NewGuid(), Amount = 2000 };
    var result2 = await _paymentService.InitiatePaymentAsync(payment2, key);
    
    result2.Accepted.Should().BeFalse();
    result2.ErrorReason.Should().Contain("different request payload");
}
```

#### 4. Race Condition
```csharp
[Fact]
public async Task InitiatePayment_RaceCondition_OnlyOnePaymentCreated()
{
    var key = "test-key-004";
    var payment = new Payment { InvoiceId = Guid.NewGuid(), Amount = 1500 };
    
    // Simulate concurrent requests
    var tasks = Enumerable.Range(0, 10)
        .Select(_ => _paymentService.InitiatePaymentAsync(payment, key))
        .ToArray();
    
    var results = await Task.WhenAll(tasks);
    
    // All should return same PaymentId
    var paymentIds = results.Select(r => r.PaymentId).Distinct();
    paymentIds.Should().HaveCount(1);
    
    // Only 1 payment in DB
    var payments = await _context.Payments.Where(p => p.IdempotencyKey == key).ToListAsync();
    payments.Should().HaveCount(1);
}
```

#### 5. Expired Record

Chỉ tạo operation mới khi record cũ đã ở trạng thái terminal (`Completed`, `Failed`, hoặc `Expired`) và không còn business operation nào có thể hoàn tất muộn. Nếu record vẫn `InProgress`, duplicate request phải trả lại trạng thái hiện tại hoặc trigger reconciliation, không được xóa và tạo payment mới.

```csharp
[Fact]
public async Task InitiatePayment_ExpiredRecord_CreatesNewPayment()
{
    var key = "test-key-005";
    
    // Create expired record
    var expiredRecord = new IdempotencyRecord
    {
        Key = key,
        PaymentId = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow.AddHours(-2),
        ExpiresAt = DateTime.UtcNow.AddHours(-1), // Expired 1 hour ago
        Status = "Expired"
    };
    _context.IdempotencyRecords.Add(expiredRecord);
    await _context.SaveChangesAsync();
    
    // New request with same key
    var result = await _paymentService.InitiatePaymentAsync(payment, key);
    
    result.Accepted.Should().BeTrue();
    result.PaymentId.Should().NotBe(expiredRecord.PaymentId); // New payment
}
```

---

## 10. Monitoring & Alerts

### Metrics

```csharp
// Idempotency hit rate
idempotency_check_total{result="new|duplicate|conflict"}

// Cleanup metrics
idempotency_cleanup_records_deleted_total
idempotency_cleanup_duration_seconds
```

### Alerts

```yaml
- alert: IdempotencyConflictRateHigh
  expr: rate(idempotency_check_total{result="conflict"}[5m]) > 0.01
  annotations:
    summary: "High idempotency conflict rate (> 1%)"

- alert: IdempotencyTableGrowing
  expr: idempotency_records_count > 1000000
  annotations:
    summary: "Idempotency table has > 1M records"
```

### Queries

```sql
-- Check table size
SELECT COUNT(*) FROM IdempotencyRecords;

-- Check expired records
SELECT COUNT(*) FROM IdempotencyRecords WHERE ExpiresAt < GETUTCDATE();

-- Top keys by usage
SELECT Key, COUNT(*) as RequestCount
FROM Payments
WHERE IdempotencyKey IS NOT NULL
GROUP BY IdempotencyKey
ORDER BY RequestCount DESC;
```

---

## 11. Best Practices

### Client-side

✅ **DO:**
- Generate unique key per business operation
- Store key locally để retry với cùng key
- Include timestamp trong key để avoid collision
- Retry với cùng key khi network timeout

❌ **DON'T:**
- Reuse key cho different operations
- Use sequential numbers (predictable)
- Use sensitive data trong key
- Generate new key mỗi lần retry

### Server-side

✅ **DO:**
- Validate key format và length
- Use database-backed implementation
- Hash request payload để detect conflicts
- Cleanup expired records regularly
- Log idempotency hits/misses

❌ **DON'T:**
- Use in-memory cache only
- Skip payload validation
- Keep records forever
- Ignore race conditions

---

## 12. Summary

| Feature | Status | Notes |
|---------|--------|-------|
| ✅ Database-backed | Implemented | IdempotencyRecords table |
| ✅ Race condition safe | Implemented | Unique constraint + catch |
| ✅ Payload validation | Implemented | SHA256 hash |
| ✅ TTL support | Implemented | 30 minutes default; do not delete non-terminal operations blindly |
| ✅ Response replay | Implemented | `ResponseJson` + `StatusCode` |
| ✅ In-progress handling | Implemented | Duplicate request should return the existing operation status |
| ✅ Automatic cleanup | Implemented | Hourly background job |
| ✅ Multi-instance safe | Implemented | Shared database |
| ✅ Persistent | Implemented | Survives restarts |
| ✅ Conflict detection | Implemented | Hash mismatch error |

**Hệ thống giờ đã có idempotency implementation chuẩn production!** 🚀
