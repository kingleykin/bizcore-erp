# Bizcore ERP - Chuẩn Giao Tiếp Async API (Stripe-Style)

## Tổng quan Kiến trúc (Fintech-Grade)

Hệ thống Bizcore ERP áp dụng mô hình giao tiếp bất đồng bộ (Asynchronous) cho mọi nghiệp vụ lõi (Payment, Invoice Posting) thông qua **Saga Orchestrator** và **MassTransit**. 

Để giải quyết bài toán "trải nghiệm đồng bộ (sync UX)" cho người dùng mà không làm nghẽn cổ chai (bottleneck) hệ thống Backend, chúng ta áp dụng mô hình **Async Core + Sync Facade + Event Completion** (Tương tự Stripe, PayPal).

Tuyệt đối KHÔNG cố ép HTTP request chờ (block) Saga hoàn tất.

---

## 3 Lớp Kiến trúc (The 3 Layers)

### 1. Backend Core (Truth System) - BẮT BUỘC Async
- Sử dụng MassTransit Saga.
- Sử dụng Outbox pattern để đảm bảo tính nguyên tử (Atomicity).
- Không tồn tại Request/Response blocking HTTP.

### 2. API Layer (Sync Facade) - KHÔNG poll DB
- API chỉ làm nhiệm vụ: Nhận request → Check Idempotency → Tạo Transaction Record (Trạng thái `Processing`) → Trả về `202 Accepted`.
- Trả về `paymentId` kèm `statusUrl` (hoặc cấu hình HATEOAS).
- Tuyệt đối không giữ HTTP Connection mở, không dùng Task.Delay trong Controller.

### 3. Client Experience Layer - Trải nghiệm Sync
Trách nhiệm tạo ra "trải nghiệm đồng bộ" thuộc về Client (Frontend/Mobile). Client có 2 lựa chọn:
- **Polling (Default, Safe):** Gọi `GET /payments/{id}` định kỳ dựa trên gợi ý `RetryAfter`.
- **Push (Advanced UX):** Sử dụng SignalR / WebSocket lắng nghe sự kiện `payment.completed`.

---

## Flow Giao dịch Chuẩn

### Step 1: Client Submit
```http
POST /api/v1/payment/pay
X-Idempotency-Key: unique-uuid-123
```
**Response: 202 Accepted**
```json
{
  "paymentId": "9e8d7c6b...",
  "status": "Processing",
  "expiresIn": 60,
  "retryAfter": 2
}
```

### Step 2: Saga Xử lý Async
- Transaction được Outbox đẩy lên RabbitMQ.
- Saga Orchestrator thực hiện các lệnh (Commands) và đợi sự kiện (Events).
- Backend hoạt động ở mức Maximum Throughput, không bị giới hạn bởi connection pool của IIS/Kestrel.

### Step 3: Client Cập nhật Trạng thái
Sử dụng một SDK Wrapper ở tầng Frontend để tự động hóa việc polling, biến luồng API phức tạp thành một lệnh gọi duy nhất:

```typescript
// frontend-sdk/payment.ts
export async function processPaymentSync(payload: any): Promise<any> {
    const initResponse = await fetch('/api/v1/payment/pay', {
        method: 'POST',
        headers: { 'X-Idempotency-Key': crypto.randomUUID() },
        body: JSON.stringify(payload)
    });
    
    if (initResponse.status !== 202) throw new Error("Initiation failed");
    
    const data = await initResponse.json();
    const startTime = Date.now();
    
    // Polling Loop
    while (Date.now() - startTime < (data.expiresIn * 1000 || 60000)) {
        const statusRes = await fetch(`/api/v1/payment/${data.paymentId}`);
        const statusData = await statusRes.json();
        
        if (statusData.status === 'Completed') return statusData;
        if (statusData.status === 'Failed') throw new Error(statusData.failureReason);
        
        // Chờ theo exponential backoff
        await new Promise(r => setTimeout(r, (statusData.retryAfter || 2) * 1000));
    }
    throw new Error("Timeout");
}
```

### Step 4: Layer 4 - SignalR UX Acceleration (Optional nhưng được khuyến nghị)
Sử dụng SignalR kết hợp Redis Backplane để push event trực tiếp về UI, loại bỏ độ trễ của polling.

**Client Code (SignalR + Fallback Polling):**
```typescript
import * as signalR from "@microsoft/signalr";

export async function processPaymentWithRealtimeFallback(payload: any, token: string): Promise<any> {
    // 1. Submit Request
    const initResponse = await fetch('/api/v1/payment/pay', {
        method: 'POST',
        headers: { 
            'X-Idempotency-Key': crypto.randomUUID(),
            'Authorization': `Bearer ${token}` 
        },
        body: JSON.stringify(payload)
    });
    const data = await initResponse.json();
    
    // 2. Thiết lập SignalR
    const connection = new signalR.HubConnectionBuilder()
        .withUrl(`/hubs/payment?access_token=${token}`)
        .withAutomaticReconnect()
        .build();

    let isResolved = false;

    return new Promise(async (resolve, reject) => {
        // Lắng nghe Push Event
        connection.on("PaymentStatusUpdated", (event) => {
            isResolved = true;
            connection.stop();
            if (event.status === 'Completed') resolve(event);
            else reject(new Error(event.failureReason));
        });

        await connection.start();
        // Subscribe vào kênh riêng của PaymentId này
        await connection.invoke("WatchPayment", data.paymentId);

        // 3. Fallback: Polling Loop (Để đảm bảo Correctness nếu SignalR rớt mạng)
        const startTime = Date.now();
        while (!isResolved && Date.now() - startTime < (data.expiresIn * 1000 || 60000)) {
            const statusRes = await fetch(`/api/v1/payment/${data.paymentId}`);
            const statusData = await statusRes.json();
            
            if (statusData.status === 'Completed') {
                isResolved = true;
                connection.stop();
                return resolve(statusData);
            }
            if (statusData.status === 'Failed') {
                isResolved = true;
                connection.stop();
                return reject(new Error(statusData.failureReason));
            }
            
            await new Promise(r => setTimeout(r, (statusData.retryAfter || 2) * 1000));
        }
        
        if (!isResolved) {
            connection.stop();
            reject(new Error("Timeout"));
        }
    });
}
```

---

## Kiến trúc Hệ thống Tổng thể

```text
                ┌──────────────┐
Client ───────► │ API POST     │ (Layer 1: Sync Facade)
                └──────┬───────┘
                       │ 202 Accepted
                       ▼
                ┌──────────────┐
                │ Saga (async) │ (Layer 2: Async Processing)
                └──────┬───────┘
                       ▼
              ┌─────────────────┐
              │ DB State Update │ (Source of Truth)
              └──────┬──────────┘
                     ▼
        ┌────────────────────────┐
        │ Event (MassTransit)    │
        └──────┬─────────────────┘
               ▼
     ┌──────────────────────┐
     │ SignalR (push event) │ (Layer 4: UX Acceleration)
     └──────────────────────┘
               │
               ▼
        Client UI update

Fallback:
Client → GET /status (polling)    (Layer 3: Correctness Fallback)
```

---

## Các Tiêu chuẩn Bắt buộc (Guardrails)

1. **Idempotency Key**: BẮT BUỘC cho mọi POST/PUT API thay đổi trạng thái tài chính. Server phải kiểm tra và trả về cache nếu duplicate.
2. **Exponential Backoff**: Endpoint `GET status` phải cung cấp thuộc tính `retryAfter` (ví dụ: 2s -> 5s -> 10s) để tránh Client spam Request.
3. **Gateway Timeout**: Mọi API Facade phải trả về kết quả trong dưới 500ms.

Kiến trúc này giúp hệ thống Bizcore ERP sẵn sàng chịu tải **10k TPS**, loại bỏ hoàn toàn Thread Starvation và nguy cơ sập Load Balancer do treo Connection.
