# 📑 COMPLETE INDEX - CODING CONVENTIONS DOCUMENTATION

> **Comprehensive index of all conventions, rules, and resources for Bizcore ERP**

---

## 📚 All Documents

### Entry Points
- **START_HERE.md** - Quick navigation based on your role
- **CONVENTIONS_README.md** - Overview and document structure

### Main Documentation
- **CODING_CONVENTIONS.md** - Complete reference guide (12 sections)
- **CONVENTIONS_QUICK_REFERENCE.md** - Quick lookup (ready-to-use)
- **CODE_REVIEW_GUIDE.md** - Code review standards and checklists
- **IMPLEMENTATION_GUIDE.md** - Team rollout plan (4 weeks)

### Configuration
- **.editorconfig** - Automated IDE enforcement

---

## 🎯 Section Index

### CODING_CONVENTIONS.md Sections

| Section | Topic | Key Points |
|---------|-------|-----------|
| 1 | Giới thiệu chung | Consistency, tools support |
| 2 | Quy tắc đặt tên | PascalCase, camelCase, interfaces, events |
| 3 | Cấu trúc mã nguồn | 4-layer DDD, layer responsibilities |
| 4 | Clean Code & Architecture | SOLID, DRY, no logic in controllers |
| 5 | Exception Handling | Domain exceptions, types, global handler |
| 6 | Logging & Observability | Structured logging, levels, correlation IDs |
| 7 | Asynchronous & Event-Driven | Async/await, events, idempotency |
| 8 | Security & Authorization | Policies, permissions, sensitive data |
| 9 | Database & Entity Framework | Async queries, transactions, concurrency |
| 10 | Testing Conventions | Unit tests, naming, best practices |
| 11 | Code Review Checklist | Complete review checklist |
| 12 | Frontend (React/TypeScript) | Component structure, naming, API clients |

---

## ⚡ Quick Rules Index

### The 5 Most Important Rules

1. **No Business Logic in Controllers**
   - Business logic → Services
   - Controllers → Accept requests, return responses
   - See: CODING_CONVENTIONS.md Section 4.1

2. **Always Add Authorization**
   - [Authorize(Policy = Permissions.X)]
   - Never [Authorize] alone
   - See: CODING_CONVENTIONS.md Section 8.1

3. **Use Events for Inter-Service Communication**
   - Publish/Subscribe via MassTransit
   - Loosely coupled, async
   - See: CODING_CONVENTIONS.md Section 7.2

4. **Throw Typed Exceptions**
   - DomainException, NotFoundException, etc.
   - Never return error codes
   - See: CODING_CONVENTIONS.md Section 5

5. **Use Async/Await Everywhere**
   - All I/O must be async
   - Method names end with Async
   - See: CODING_CONVENTIONS.md Section 7.1

---

## 📖 Naming Conventions Index

### By Type

| Type | Pattern | Example | See |
|------|---------|---------|-----|
| Class | PascalCase | InvoiceService | CODING_CONVENTIONS.md 2.1 |
| Interface | I + PascalCase | IInvoiceService | CODING_CONVENTIONS.md 2.3 |
| Method | PascalCase + Async | GetByIdAsync() | CODING_CONVENTIONS.md 2.1 |
| Property | PascalCase | CustomerName | CODING_CONVENTIONS.md 2.1 |
| Private Field | _camelCase | _invoiceService | CODING_CONVENTIONS.md 2.2 |
| Local Variable | camelCase | invoiceId | CODING_CONVENTIONS.md 2.2 |
| Parameter | camelCase | customerId | CODING_CONVENTIONS.md 2.2 |
| Constant | SCREAMING_SNAKE_CASE | MAX_INVOICE_AMOUNT | CODING_CONVENTIONS.md 2.1 |
| Event | {Entity}{Action}Event | PaymentCompletedEvent | CODING_CONVENTIONS.md 2.4 |
| Consumer | {Event}Consumer | PaymentCompletedConsumer | CODING_CONVENTIONS.md 2.4 |
| File | Match Class Name | InvoiceService.cs | CODING_CONVENTIONS.md 2.5 |

---

## 🏗️ Architecture Index

### 4-Layer DDD Structure

```
API Layer (Controllers)
  ↓
Application Layer (Services, Handlers)
  ↓
Infrastructure Layer (DbContext, Repos)
  ↓
Domain Layer (Entities, Enums, Interfaces)
```

**Responsibilities**:
- Domain: Entities, validations, enums - NO dependencies
- Application: Services, handlers, events - Orchestrates business
- Infrastructure: DbContext, repos - Technical implementations
- API: Controllers, endpoints - HTTP concerns only

See: CODING_CONVENTIONS.md Section 3

---

## 🔄 Pattern Index

### Common Patterns with Examples

| Pattern | Use Case | See |
|---------|----------|-----|
| Factory Methods | Entity creation with validation | CONVENTIONS_QUICK_REFERENCE.md |
| Service Classes | Business logic orchestration | CONVENTIONS_QUICK_REFERENCE.md |
| Event Consumers | Async event processing | CONVENTIONS_QUICK_REFERENCE.md |
| Controllers | HTTP endpoints | CONVENTIONS_QUICK_REFERENCE.md |
| Domain Entities | Business logic, validation | CONVENTIONS_QUICK_REFERENCE.md |
| Outbox Pattern | Atomic DB + event publishing | CODING_CONVENTIONS.md 9.3 |
| Local Transactions | Multi-entity operations | CODING_CONVENTIONS.md 9.3 |
| Idempotency | Replay-safe consumers | CODING_CONVENTIONS.md 7.3 |

---

## 🔍 Code Review Index

### CODE_REVIEW_GUIDE.md Sections

| Section | Checklist Items | Priority |
|---------|-----------------|----------|
| 1 | Architecture & Design | CRITICAL |
| 2 | Naming Conventions | IMPORTANT |
| 3 | Async/Await | CRITICAL |
| 4 | Exception Handling | IMPORTANT |
| 5 | Authorization & Security | CRITICAL |
| 6 | Logging | IMPORTANT |
| 7 | Database & ORM | IMPORTANT |
| 8 | Testing | IMPORTANT |
| 9 | Code Quality | GUIDELINE |
| 10 | Performance | GUIDELINE |

See: CODE_REVIEW_GUIDE.md for complete checklists

---

## 🛠 Tool Configuration Index

### .editorconfig Rules

**For C# Files**:
- Indentation: 4 spaces
- Naming rules: PascalCase, camelCase, _camelCase, I{Name}
- Brace style: All new line
- Spacing: Around operators, after commas

**For Other Files**:
- JSON, YAML: 2 spaces
- Markdown: No trim trailing whitespace
- All: UTF-8, LF line endings

See: .editorconfig in project root

---

## 📊 Exception Types Index

### Defined Exceptions

- **DomainException** - Business rule violation
- **NotFoundException** - Resource not found
- **UnauthorizedException** - Access denied
- **ValidationException** - Input validation failed

All in: `Bizcore.BuildingBlocks.Exceptions`

See: CODING_CONVENTIONS.md Section 5

---

## 🔐 Security Index

### Authorization Patterns

**Defined in Permissions class**:
- Invoice.View, Invoice.Create, Invoice.Update, Invoice.Delete
- Payment.View, Payment.Create
- Audit.View, Audit.AdminMode

**Usage**:
```csharp
[Authorize(Policy = Permissions.Invoice.Create)]
```

See: CODING_CONVENTIONS.md Section 8.1, 8.2

---

## 📝 Logging Index

### Log Levels & When to Use

| Level | Use For | See |
|-------|---------|-----|
| Debug | Development troubleshooting | CODING_CONVENTIONS.md 6.1 |
| Info | Business events, milestones | CODING_CONVENTIONS.md 6.1 |
| Warning | Recoverable issues | CODING_CONVENTIONS.md 6.1 |
| Error | Exception with recovery | CODING_CONVENTIONS.md 6.1 |
| Critical | System unstable | CODING_CONVENTIONS.md 6.1 |

### Structured Logging Format

```csharp
_logger.LogInformation("Invoice created: {InvoiceId}, {Amount}", 
    invoice.Id, invoice.Amount);
```

See: CODING_CONVENTIONS.md Section 6, CONVENTIONS_QUICK_REFERENCE.md

---

## 🧪 Testing Index

### Test Naming Convention

Pattern: `{MethodName}_{Scenario}_{ExpectedResult}`

Examples:
- CreateAsync_WithValidData_ReturnsInvoice
- GetByIdAsync_WithInvalidId_ThrowsNotFoundException
- MarkAsPaid_WithPendingInvoice_Succeeds

See: CODING_CONVENTIONS.md Section 10

---

## 📋 Frontend Index

### React/TypeScript Conventions

- Components: PascalCase (InvoiceList.tsx)
- Functions/Variables: camelCase (handleSubmit)
- Constants: SCREAMING_SNAKE_CASE (API_BASE_URL)
- Interfaces: PascalCase with I prefix (IInvoice)

See: CODING_CONVENTIONS.md Section 12

---

## 🚀 Rollout Timeline

**4-Week Implementation Plan**:

| Week | Focus | Deliverable |
|------|-------|-------------|
| 1 | Awareness & Setup | Tools configured, team trained |
| 2 | Soft Enforcement | Review suggestions, gentle feedback |
| 3 | Medium Enforcement | Request changes for major violations |
| 4+ | Strict Enforcement | Reject non-compliant PRs |

See: IMPLEMENTATION_GUIDE.md

---

## 🎓 Training Path

### For New Developers (30 min)
1. START_HERE.md (5 min)
2. CONVENTIONS_QUICK_REFERENCE.md (5 min)
3. CODING_CONVENTIONS.md Sections 2-4 (15 min)
4. Setup IDE with .editorconfig (5 min)

### For Code Reviewers (45 min)
1. CODE_REVIEW_GUIDE.md (full read)
2. CONVENTIONS_QUICK_REFERENCE.md (bookmark)
3. Practice review with checklist

### For Team Leads (2 hours)
1. CONVENTIONS_README.md (10 min)
2. IMPLEMENTATION_GUIDE.md (20 min)
3. CODING_CONVENTIONS.md (full)
4. CODE_REVIEW_GUIDE.md (full)

---

## ✅ Success Criteria

### Phase 1 (Week 1-2)
- ✅ 100% team trained
- ✅ Tools configured
- ✅ Sample PRs reviewed

### Phase 2 (Week 3-4)
- ✅ 80%+ PRs compliant
- ✅ New devs writing compliant code
- ✅ Violations caught in review

### Phase 3 (Week 5+)
- ✅ 95%+ compliance
- ✅ Automated checks working
- ✅ Code reviews faster

---

## 🎯 By Topic Quick Links

### Naming
- Conventions: CODING_CONVENTIONS.md Section 2
- Quick ref: CONVENTIONS_QUICK_REFERENCE.md
- Review guide: CODE_REVIEW_GUIDE.md Section 2

### Architecture
- Details: CODING_CONVENTIONS.md Sections 3-4
- Review guide: CODE_REVIEW_GUIDE.md Section 1
- Patterns: CONVENTIONS_QUICK_REFERENCE.md

### Exceptions
- Full guide: CODING_CONVENTIONS.md Section 5
- Quick ref: CONVENTIONS_QUICK_REFERENCE.md
- Review guide: CODE_REVIEW_GUIDE.md Section 4

### Logging
- Full guide: CODING_CONVENTIONS.md Section 6
- Templates: CONVENTIONS_QUICK_REFERENCE.md
- Review guide: CODE_REVIEW_GUIDE.md Section 6

### Async/Await
- Full guide: CODING_CONVENTIONS.md Section 7
- Quick ref: CONVENTIONS_QUICK_REFERENCE.md
- Review guide: CODE_REVIEW_GUIDE.md Section 3

### Security
- Full guide: CODING_CONVENTIONS.md Section 8
- Review guide: CODE_REVIEW_GUIDE.md Section 5
- Patterns: CONVENTIONS_QUICK_REFERENCE.md

### Database
- Full guide: CODING_CONVENTIONS.md Section 9
- Review guide: CODE_REVIEW_GUIDE.md Section 7
- Patterns: CONVENTIONS_QUICK_REFERENCE.md

### Testing
- Full guide: CODING_CONVENTIONS.md Section 10
- Review guide: CODE_REVIEW_GUIDE.md Section 8
- Patterns: CONVENTIONS_QUICK_REFERENCE.md

### Code Review
- Complete guide: CODE_REVIEW_GUIDE.md (all sections)
- Checklist: CODE_REVIEW_GUIDE.md Section 1-10

### Rollout
- Implementation: IMPLEMENTATION_GUIDE.md
- Timeline: IMPLEMENTATION_GUIDE.md Section "4-Week Plan"

### Setup
- IDE: .editorconfig
- Tools: IMPLEMENTATION_GUIDE.md Section "Tool Configuration"

---

## 📞 Support

**Questions about**:
- **Specific rule**: Check CODING_CONVENTIONS.md index above
- **Code review**: See CODE_REVIEW_GUIDE.md
- **Team implementation**: See IMPLEMENTATION_GUIDE.md
- **Quick answer**: See CONVENTIONS_QUICK_REFERENCE.md

**For anything not listed**: 
1. Check CONVENTIONS_README.md FAQ
2. Ask team lead or architect
3. Create GitHub issue if convention needs clarification

---

## 📊 Document Statistics

Total: **6 documents + 1 config file**
- ~3,500 lines of documentation
- 100+ code examples
- 150+ checklist items
- 80+ ready-to-use templates
- Complete coverage of all topics

---

**Last Updated**: 2024-05-07  
**Version**: 1.0  
**Scope**: Complete Bizcore ERP Project

