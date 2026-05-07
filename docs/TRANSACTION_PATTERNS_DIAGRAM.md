# Transaction Patterns - Visual Diagrams

## 📊 1. Local Transaction Pattern

```
┌─────────────────────────────────────────────────────────────────┐
│                    Local Transaction Flow                       │
└─────────────────────────────────────────────────────────────────┘

Client Request
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│ Service Layer                                                   │
│                                                                 │
│  1. BEGIN TRANSACTION                                           │
│     ├─ IsolationLevel: Read Committed (default)                │
│     └─ TransactionId: abc-123                                   │
│                                                                 │
│  2. Business Logic                                              │
│     ├─ Validate input                                           │
│     ├─ Create Entity1 → Add to DbContext                        │
│     ├─ Create Entity2 → Add to DbContext                        │
│     └─ Update Entity3 → Mark as Modified                        │
│                                                                 │
│  3. SaveChangesAsync()                                          │
│     ├─ Generate SQL commands                                    │
│     ├─ Execute within transaction                               │
│     └─ All succeed or all rollback                              │
│                                                                 │
│  4. COMMIT TRANSACTION                                          │
│     └─ Make changes permanent                                   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
    │
    ▼
Response to Client

┌─────────────────────────────────────────────────────────────────┐
│ Error Handling                                                  │
│                                                                 │
│  Exception thrown?                                              │
│     ├─ YES → ROLLBACK TRANSACTION                               │
│     │         └─ All changes discarded                          │
│     │         └─ Log error                                      │
│     │         └─ Throw exception                                │
│     │                                                            │
│     └─ NO  → COMMIT TRANSACTION                                 │
│               └─ Changes persisted                              │
└─────────────────────────────────────────────────────────────────┘

Example Use Cases:
✅ Payment + IdempotencyRecord
✅ Invoice + AuditLog (if same DB)
✅ User + UserRole + UserPermission
```

---

## 📊 2. Outbox Pattern

```
┌─────────────────────────────────────────────────────────────────┐
│                      Outbox Pattern Flow                        │
└─────────────────────────────────────────────────────────────────┘

Client Request
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│ Service Layer                                                   │
│                                                                 │
│  1. BEGIN TRANSACTION                                           │
│     └─ TransactionId: xyz-456                                   │
│                                                                 │
│  2. Create Business Entity                                      │
│     └─ Invoice { Id, CustomerName, Amount, Status }             │
│                                                                 │
│  3. Publish Event (via MassTransit)                             │
│     ├─ InvoiceCreatedEvent { Id, CustomerName, Amount }         │
│     └─ ⚠️ NOT sent to RabbitMQ yet!                             │
│     └─ ✅ Saved to OutboxMessage table                          │
│                                                                 │
│  4. SaveChangesAsync()                                          │
│     ├─ INSERT INTO Invoices (...)                               │
│     ├─ INSERT INTO OutboxMessage (...)                          │
│     └─ Both in same transaction                                 │
│                                                                 │
│  5. COMMIT TRANSACTION                                          │
│     └─ Invoice + OutboxMessage both committed                   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
    │
    ▼
Response to Client (202 Accepted)

┌─────────────────────────────────────────────────────────────────┐
│ MassTransit Outbox Background Service                          │
│                                                                 │
│  Loop every 1 second:                                           │
│                                                                 │
│  1. Query OutboxMessage table                                   │
│     └─ SELECT * FROM OutboxMessage WHERE Delivered IS NULL      │
│                                                                 │
│  2. For each pending message:                                   │
│     ├─ Deserialize message                                      │
│     ├─ Publish to RabbitMQ                                      │
│     └─ Mark as Delivered                                        │
│                                                                 │
│  3. Retry on failure                                            │
│     ├─ Max retries: 3                                           │
│     ├─ Timeout: 5 minutes                                       │
│     └─ Exponential backoff                                      │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│ RabbitMQ                                                        │
│  └─ InvoiceCreatedEvent delivered to subscribers                │
└─────────────────────────────────────────────────────────────────┘

Benefits:
✅ Atomic: Invoice and Event committed together
✅ Reliable: Event survives RabbitMQ downtime
✅ No message loss: Event stored in DB
✅ Automatic retry: MassTransit handles failures
```

---

## 📊 3. Dual Write Problem (Anti-Pattern)

```
┌─────────────────────────────────────────────────────────────────┐
│              ❌ Dual Write Problem (DON'T DO THIS)              │
└─────────────────────────────────────────────────────────────────┘

Client Request
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│ Service Layer (BAD IMPLEMENTATION)                              │
│                                                                 │
│  1. Create Invoice                                              │
│     └─ _context.Invoices.Add(invoice)                           │
│                                                                 │
│  2. SaveChangesAsync()                                          │
│     └─ ✅ Invoice committed to DB                               │
│                                                                 │
│  3. Publish Event                                               │
│     └─ await _publishEndpoint.Publish(event)                    │
│        └─ ❌ Network failure!                                   │
│        └─ ❌ RabbitMQ down!                                     │
│        └─ ❌ Exception thrown!                                  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│ Result: DATA INCONSISTENCY                                      │
│                                                                 │
│  ✅ Invoice exists in DB                                        │
│  ❌ Event NOT published                                         │
│  ❌ Other services don't know about Invoice                     │
│  ❌ System in inconsistent state                                │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘

Problems:
❌ Invoice created but no event
❌ Payment Service never updates
❌ Report Service missing data
❌ Manual intervention required
❌ Data corruption risk

Solution: Use Outbox Pattern!
```

---

## 📊 4. Partitioned Audit Hash Chain

```
┌─────────────────────────────────────────────────────────────────┐
│      Partitioned Append + Sequence for Hash Chain               │
└─────────────────────────────────────────────────────────────────┘

Concurrent Requests (Thread 1 & Thread 2)
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│ Thread 1                          │ Thread 2                    │
├───────────────────────────────────┼─────────────────────────────┤
│                                   │                             │
│ BEGIN TRANSACTION                 │                             │
│ (Read Committed)                  │                             │
│   │                               │                             │
│   ├─ Lock: Read range             │                             │
│   │  (Last AuditEntry)            │                             │
│   │                               │                             │
│   ├─ Read PreviousHash            │ BEGIN TRANSACTION           │
│   │  = "abc123"                   │ (waits for same partition)  │
│   │                               │   │                         │
│   ├─ Compute Hash                 │   ├─ ⏳ WAIT                │
│   │  = SHA256(data + "abc123")    │   │  (Blocked by Thread 1)  │
│   │  = "def456"                   │   │                         │
│   │                               │   │                         │
│   ├─ Insert AuditEntry            │   │                         │
│   │  Hash = "def456"              │   │                         │
│   │  PreviousHash = "abc123"      │   │                         │
│   │                               │   │                         │
│   └─ COMMIT                       │   │                         │
│      ✅ Success                    │   │                         │
│                                   │   │                         │
│                                   │   ├─ 🔓 UNBLOCKED           │
│                                   │   │                         │
│                                   │   ├─ Read PreviousHash      │
│                                   │   │  = "def456" (Thread 1)  │
│                                   │   │                         │
│                                   │   ├─ Compute Hash           │
│                                   │   │  = SHA256(data + "def456")│
│                                   │   │  = "ghi789"             │
│                                   │   │                         │
│                                   │   ├─ Insert AuditEntry      │
│                                   │   │  Hash = "ghi789"        │
│                                   │   │  PreviousHash = "def456"│
│                                   │   │                         │
│                                   │   └─ COMMIT                 │
│                                   │      ✅ Success              │
│                                   │                             │
└───────────────────────────────────┴─────────────────────────────┘

Result: Hash Chain Intact
┌─────────────────────────────────────────────────────────────────┐
│ AuditEntry 1                                                    │
│  Hash: "abc123"                                                 │
│  PreviousHash: null                                             │
├─────────────────────────────────────────────────────────────────┤
│ AuditEntry 2 (Thread 1)                                         │
│  Hash: "def456"                                                 │
│  PreviousHash: "abc123" ✅                                      │
├─────────────────────────────────────────────────────────────────┤
│ AuditEntry 3 (Thread 2)                                         │
│  Hash: "ghi789"                                                 │
│  PreviousHash: "def456" ✅                                      │
└─────────────────────────────────────────────────────────────────┘

Without partition lock/sequence (Race Condition):
┌─────────────────────────────────────────────────────────────────┐
│ Thread 1 & Thread 2 both assign the same previous hash/sequence │
│ Both compute hash based on "abc123"                             │
│ Both insert with PreviousHash = "abc123"                        │
│ ❌ Hash chain broken! Two entries point to same previous        │
└─────────────────────────────────────────────────────────────────┘
```

---

## 📊 5. Saga Pattern (Cross-Service)

```
┌─────────────────────────────────────────────────────────────────┐
│                    Saga Pattern (Eventual Consistency)          │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ Payment Service                                                 │
│                                                                 │
│  1. Client initiates payment                                    │
│     └─ POST /api/v1/payment/pay                                 │
│                                                                 │
│  2. Local Transaction                                           │
│     ├─ Create Payment (Status: Processing)                      │
│     ├─ Create IdempotencyRecord                                 │
│     └─ Publish PaymentInitiatedEvent (via Outbox)               │
│                                                                 │
│  3. Wait for validation...                                      │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
    │
    │ PaymentInitiatedEvent
    ▼
┌─────────────────────────────────────────────────────────────────┐
│ Invoice Service                                                 │
│                                                                 │
│  4. Consume PaymentInitiatedEvent                               │
│                                                                 │
│  5. Validate Invoice                                            │
│     ├─ Invoice exists?                                          │
│     ├─ Amount matches?                                          │
│     └─ Status = Pending?                                        │
│                                                                 │
│  6a. ✅ Validation Success                                      │
│      ├─ Update Invoice (Status: Paid)                           │
│      └─ Publish ValidateInvoiceSuccessCommand                   │
│                                                                 │
│  6b. ❌ Validation Failed                                       │
│      └─ Publish PaymentCompensationRequestedEvent               │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
    │
    │ ValidateInvoiceSuccessCommand OR
    │ PaymentCompensationRequestedEvent
    ▼
┌─────────────────────────────────────────────────────────────────┐
│ Payment Service                                                 │
│                                                                 │
│  7a. ✅ Success Path                                            │
│      └─ Consume ValidateInvoiceSuccessCommand                   │
│         └─ Update Payment (Status: Completed)                   │
│                                                                 │
│  7b. ❌ Compensation Path                                       │
│      └─ Consume PaymentCompensationRequestedEvent               │
│         └─ Update Payment (Status: Reversed)                    │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│ Orchestration Service (Observer)                                │
│                                                                 │
│  8. Track entire flow                                           │
│     ├─ ProcessFlow { InvoiceId, PaymentId, Status }             │
│     └─ FlowSteps:                                               │
│        ├─ Step 1: PaymentInitiated                              │
│        ├─ Step 2: InvoiceValidated                              │
│        └─ Step 3: PaymentCompleted/Reversed                     │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘

Key Points:
✅ Each service has its own local transaction
✅ No distributed transaction (2PC)
✅ Eventual consistency via events
✅ Compensation for rollback
✅ Orchestration for visibility
```

---

## 📊 6. Transaction Decision Tree

```
┌─────────────────────────────────────────────────────────────────┐
│              Transaction Pattern Decision Tree                  │
└─────────────────────────────────────────────────────────────────┘

START: Need to persist data?
    │
    ├─ Single table, no events
    │  └─ ✅ Simple SaveChangesAsync()
    │
    ├─ Multiple tables, same DB
    │  └─ ✅ Local Transaction
    │     └─ BEGIN TRANSACTION
    │        └─ SaveChangesAsync()
    │           └─ COMMIT
    │
    ├─ Single/Multiple tables + Publish event
    │  └─ ✅ Outbox Pattern
    │     └─ BEGIN TRANSACTION
    │        ├─ SaveChangesAsync()
    │        ├─ Publish (saved to Outbox)
    │        └─ COMMIT
    │           └─ MassTransit delivers from Outbox
    │
    ├─ Audit with Hash Chain
    │  └─ ✅ Partitioned Audit Append
    │     └─ BEGIN TRANSACTION
    │        ├─ Lock Partition Head
    │        ├─ Assign Sequence
    │        ├─ Read PreviousHash by Sequence
    │        ├─ Compute Hash
    │        ├─ SaveChangesAsync()
    │        └─ COMMIT
    │
    └─ Cross-service coordination
       └─ ✅ Saga Pattern
          ├─ Service A: Local Transaction + Publish Event
          ├─ Service B: Consume Event + Local Transaction
          └─ Compensation: Publish Compensation Event

┌─────────────────────────────────────────────────────────────────┐
│ Isolation Level Decision                                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Default (Read Committed)                                       │
│  └─ Use for: 95% of cases                                       │
│     └─ Payment, Invoice, Identity, Report                       │
│                                                                 │
│  Partitioned audit append                                       │
│  └─ Use for: Audit Service                                      │
│     └─ Hash Chain integrity with per-partition sequence/lock    │
│                                                                 │
│  Read Uncommitted                                               │
│  └─ ❌ NEVER USE                                                │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 📊 7. Outbox Tables Schema

```
┌─────────────────────────────────────────────────────────────────┐
│                    MassTransit Outbox Tables                    │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ InboxState (Deduplication for incoming messages)               │
├─────────────────────────────────────────────────────────────────┤
│ MessageId (PK)        │ UNIQUEIDENTIFIER                        │
│ ConsumerId            │ UNIQUEIDENTIFIER                        │
│ LockId                │ UNIQUEIDENTIFIER                        │
│ RowVersion            │ TIMESTAMP                               │
│ Received              │ DATETIME2                               │
│ ReceiveCount          │ INT                                     │
│ ExpirationTime        │ DATETIME2                               │
│ Consumed              │ DATETIME2 (nullable)                    │
│ Delivered             │ DATETIME2 (nullable)                    │
│ LastSequenceNumber    │ BIGINT (nullable)                       │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ OutboxState (Tracking outbox delivery)                          │
├─────────────────────────────────────────────────────────────────┤
│ OutboxId (PK)         │ UNIQUEIDENTIFIER                        │
│ LockId                │ UNIQUEIDENTIFIER                        │
│ RowVersion            │ TIMESTAMP                               │
│ Created               │ DATETIME2                               │
│ Delivered             │ DATETIME2 (nullable)                    │
│ LastSequenceNumber    │ BIGINT (nullable)                       │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ OutboxMessage (Pending messages to deliver)                     │
├─────────────────────────────────────────────────────────────────┤
│ SequenceNumber (PK)   │ BIGINT (Identity)                       │
│ EnqueueTime           │ DATETIME2                               │
│ SentTime              │ DATETIME2                               │
│ Headers               │ NVARCHAR(MAX) (JSON)                    │
│ Properties            │ NVARCHAR(MAX) (JSON)                    │
│ InboxMessageId        │ UNIQUEIDENTIFIER (nullable)             │
│ InboxConsumerId       │ UNIQUEIDENTIFIER (nullable)             │
│ OutboxId              │ UNIQUEIDENTIFIER (nullable)             │
│ MessageId             │ UNIQUEIDENTIFIER                        │
│ ContentType           │ NVARCHAR(256)                           │
│ MessageType           │ NVARCHAR(MAX) (JSON array)              │
│ Body                  │ VARBINARY(MAX) (Serialized message)     │
│ ConversationId        │ UNIQUEIDENTIFIER (nullable)             │
│ CorrelationId         │ UNIQUEIDENTIFIER (nullable)             │
│ InitiatorId           │ UNIQUEIDENTIFIER (nullable)             │
│ RequestId             │ UNIQUEIDENTIFIER (nullable)             │
│ SourceAddress         │ NVARCHAR(256) (nullable)                │
│ DestinationAddress    │ NVARCHAR(256) (nullable)                │
│ ResponseAddress       │ NVARCHAR(256) (nullable)                │
│ FaultAddress          │ NVARCHAR(256) (nullable)                │
│ ExpirationTime        │ DATETIME2 (nullable)                    │
└─────────────────────────────────────────────────────────────────┘

Workflow:
1. Publish() → Insert into OutboxMessage
2. SaveChangesAsync() → Commit OutboxMessage + Business Entity
3. Background Service → Query OutboxMessage WHERE SentTime IS NULL
4. Deliver to RabbitMQ → Update SentTime
5. Cleanup → Delete old messages
```

---

## 📊 8. Performance Comparison

```
┌─────────────────────────────────────────────────────────────────┐
│              Transaction Pattern Performance                    │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ Pattern               │ Latency │ Throughput │ Reliability      │
├───────────────────────┼─────────┼────────────┼──────────────────┤
│ No Transaction        │  ~5ms   │ ⚡⚡⚡ High  │ ❌ Unsafe        │
│ Read Committed        │ ~10ms   │ ⚡⚡ Medium │ ✅ Safe          │
│ Partitioned Audit     │ ~15-30ms│ ⚡⚡ Medium│ ✅ Safe          │
│ Serializable fallback │ ~50ms+  │ ⚡ Low     │ ⚠️ Temporary    │
│ Outbox                │ ~15ms   │ ⚡⚡ Medium │ ✅ Safe+Reliable │
│ Saga (Cross-Service)  │ ~100ms  │ ⚡ Low     │ ✅ Eventually    │
└─────────────────────────────────────────────────────────────────┘

Latency Breakdown (Outbox Pattern):
┌─────────────────────────────────────────────────────────────────┐
│ Operation                              │ Time                   │
├────────────────────────────────────────┼────────────────────────┤
│ BEGIN TRANSACTION                      │ ~1ms                   │
│ Business Logic                         │ ~2ms                   │
│ Publish (insert to OutboxMessage)      │ ~1ms                   │
│ SaveChangesAsync()                     │ ~5ms                   │
│ COMMIT                                 │ ~1ms                   │
├────────────────────────────────────────┼────────────────────────┤
│ Total (Synchronous)                    │ ~10ms                  │
├────────────────────────────────────────┼────────────────────────┤
│ Outbox Delivery (Asynchronous)         │ ~5ms (background)      │
└─────────────────────────────────────────────────────────────────┘

Throughput (Requests per second):
┌─────────────────────────────────────────────────────────────────┐
│ No Transaction:        ~1000 req/s                              │
│ Read Committed:        ~500 req/s                               │
│ Global Serializable:   ~100 req/s (avoid)                       │
│ Partitioned Audit:     ~1000+ req/s with per-partition locking  │
│ Outbox:                ~400 req/s                               │
└─────────────────────────────────────────────────────────────────┘
```

---

*Visual diagrams này giúp hiểu rõ cách hoạt động của các Transaction Patterns.*
*Cập nhật lần cuối: 07/05/2026*
