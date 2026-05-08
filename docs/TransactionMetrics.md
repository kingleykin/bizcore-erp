Class `TransactionMetrics` này là lớp tập trung toàn bộ metric phục vụ **observability** cho hệ thống microservice của bạn.
Nó giúp bạn trả lời các câu hỏi kiểu:

* Transaction nào chậm?
* Service nào rollback nhiều?
* Outbox có bị backlog không?
* Saga nào bị timeout?
* Consumer nào nhận duplicate message?
* Có bao nhiêu message đang chết trong DLQ?

Nói ngắn gọn:

> Đây là lớp biến hệ thống của bạn từ “black box” thành “observable system”.

---

# 1. Tại sao class này quan trọng

Trong monolith nhỏ:

* log là đủ

Nhưng với microservice:

* async message
* eventual consistency
* retry
* outbox
* saga
* compensation
* distributed transaction

=> log KHÔNG còn đủ.

Bạn cần:

| Thành phần | Dùng để làm gì              |
| ---------- | --------------------------- |
| Log        | Debug từng request          |
| Metrics    | Theo dõi sức khỏe realtime  |
| Trace      | Theo dõi flow cross-service |

Class này là phần **metrics**.

---

# 2. `Histogram TransactionDuration`

```csharp
public static readonly Histogram TransactionDuration
```

Theo dõi:

* transaction chạy bao lâu

Ví dụ:

| operation        | duration |
| ---------------- | -------- |
| CreateInvoice    | 12ms     |
| ProcessPayment   | 800ms    |
| SagaCompensation | 5s       |

---

## Dùng để phát hiện gì?

### ✅ Slow transaction

Ví dụ:

* bình thường payment = 50ms
* hôm nay = 3s

=> có vấn đề:

* DB lock
* deadlock
* network
* RabbitMQ lag
* outbox backlog

---

## Cách dùng

```csharp
var timer = TransactionMetrics.TransactionDuration
    .WithLabels("payment-service", "initiate-payment", "success")
    .NewTimer();

try
{
    // business logic
}
finally
{
    timer.Dispose();
}
```

---

# 3. `TransactionTotal`

```csharp
public static readonly Counter TransactionTotal
```

Đếm tổng transaction.

Ví dụ:

| operation     | status  | count  |
| ------------- | ------- | ------ |
| CreateInvoice | success | 10,000 |
| CreateInvoice | failed  | 120    |

---

## Dùng để làm gì?

### ✅ Detect failure spike

Ví dụ:

* bình thường failed = 0.1%
* đột nhiên = 25%

=> production incident.

---

## Cách dùng

```csharp
TransactionMetrics.TransactionTotal
    .WithLabels("invoice-service", "create-invoice", "success")
    .Inc();
```

---

# 4. `OutboxPendingCount`

```csharp
public static readonly Gauge OutboxPendingCount
```

Cái này cực kỳ quan trọng với Outbox Pattern.

---

## Theo dõi gì?

Có bao nhiêu message đang nằm trong:

```sql
OutboxMessage
```

chưa được gửi lên RabbitMQ.

---

## Bình thường

```text
0 -> 10
```

---

## Có vấn đề

```text
50,000 pending messages
```

=> cực nguy hiểm.

---

## Điều đó nghĩa là gì?

Outbox delivery bị stuck:

* RabbitMQ down
* consumer crash
* DB lock
* outbox processor chết

---

## Nếu không có metric này

Bạn sẽ:

* không biết event đang bị kẹt
* invoice tạo rồi nhưng payment không chạy
* saga đứng im

=> nightmare debug.

---

# 5. `OutboxDeliveredTotal`

```csharp
public static readonly Counter OutboxDeliveredTotal
```

Theo dõi số message đã gửi thành công/thất bại từ outbox.

---

## Ví dụ

| status  | count  |
| ------- | ------ |
| success | 100000 |
| failed  | 500    |

---

## Dùng để phát hiện

### RabbitMQ instability

Nếu failed tăng mạnh:

* broker timeout
* network issue
* serialization issue

---

# 6. `InboxDuplicateCount`

```csharp
public static readonly Counter InboxDuplicateCount
```

Theo dõi:

* số message duplicate bị detect

---

# Vì sao duplicate xảy ra?

RabbitMQ/MassTransit là:

> at-least-once delivery

Tức:

* consumer xử lý xong
* nhưng ACK fail
* broker resend

=> cùng message chạy lại.

---

## Metric này giúp gì?

Nếu duplicate tăng mạnh:

| Nguyên nhân      |
| ---------------- |
| consumer chậm    |
| timeout          |
| broker reconnect |
| retry storm      |

---

# 7. `SagaActiveCount`

```csharp
public static readonly Gauge SagaActiveCount
```

Theo dõi số saga đang active.

Ví dụ:

| saga        | state               | count |
| ----------- | ------------------- | ----- |
| PaymentSaga | ProcessingPayment   | 500   |
| PaymentSaga | WaitingCompensation | 20    |

---

# Tác dụng

### ✅ Detect stuck saga

Ví dụ:

```text
WaitingPayment = 50,000
```

=> payment service chết.

---

# 8. `SagaTimeoutCount`

```csharp
public static readonly Counter SagaTimeoutCount
```

Đếm số saga bị timeout.

---

## Ví dụ

PaymentSaga:

* tạo invoice
* chờ payment 5 phút

Nếu hết timeout:

* trigger compensation

Metric này cho biết:

* hệ thống orchestration đang fail ở đâu

---

# 9. `CompensationCount`

```csharp
public static readonly Counter CompensationCount
```

Theo dõi:

* số rollback business logic

Ví dụ:

| reason               | count |
| -------------------- | ----- |
| PaymentFailed        | 200   |
| InventoryUnavailable | 50    |

---

# Tác dụng

Nếu compensation tăng:

=> consistency issue tăng.

Ví dụ:

* payment success thấp
* inventory race condition
* external provider unstable

---

# 10. `DlqMessageCount`

```csharp
public static readonly Counter DlqMessageCount
```

Theo dõi số message bị đẩy vào:

> Dead Letter Queue (DLQ)

---

# Điều này cực kỳ quan trọng

Message vào DLQ nghĩa là:

> system không xử lý nổi message nữa

---

## Nguyên nhân

| Nguyên nhân        |
| ------------------ |
| deserialize fail   |
| business exception |
| schema mismatch    |
| missing handler    |
| poison message     |

---

## Nếu DLQ tăng

=> production đang cháy.

---

# 11. Vì sao dùng static readonly

```csharp
public static readonly Counter ...
```

Vì Prometheus metric:

* global singleton
* không nên tạo lại mỗi request

Nếu tạo mới liên tục:

* memory leak
* duplicate metric registration

---

# 12. Buckets trong Histogram

```csharp
Buckets = Histogram.ExponentialBuckets(0.001, 2, 10)
```

Sinh ra:

```text
0.001
0.002
0.004
0.008
...
0.512
```

Prometheus sẽ biết:

* bao nhiêu request < 1ms
* bao nhiêu < 2ms
* bao nhiêu < 4ms

=> build percentile:

* p50
* p95
* p99

---

# 13. Thứ class này thực sự mang lại

Không phải chỉ “đếm”.

Mà là:

| Không có metrics               | Có metrics                     |
| ------------------------------ | ------------------------------ |
| Không biết hệ thống chết ở đâu | Thấy ngay bottleneck           |
| Chỉ debug bằng log             | Monitor realtime               |
| Phản ứng sau khi lỗi           | Detect sớm                     |
| Khó scale                      | Scale có dữ liệu               |
| Không biết saga stuck          | Thấy chính xác state nào stuck |

---

# 14. Kiến trúc production chuẩn thường là

```text
Service
   ↓
Prometheus metrics endpoint
   ↓
Prometheus scrape
   ↓
Grafana dashboard
   ↓
AlertManager
   ↓
Slack/Telegram/Email alert
```

---

# 15. Đây mới là giá trị lớn nhất

Khi hệ thống nhỏ:

* metrics nhìn có vẻ thừa

Khi hệ thống production:

* metrics = sinh tồn

Đặc biệt với:

* Saga
* Outbox
* Event-driven architecture
* Distributed transaction

thì metrics gần như bắt buộc.
