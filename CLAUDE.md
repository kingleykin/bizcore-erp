# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> Deeper AI-oriented architecture context: [docs/02-project-overview/PROJECT_INDEX.md](docs/02-project-overview/PROJECT_INDEX.md). The `docs/` tree is numbered `01-getting-started` … `09-testing`; [docs/README.md](docs/README.md) is the index. This file stays intentionally shorter than those — read them for anything not covered here.

## Commands

Solution file is `src/Bizcore.slnx` (`.slnx` format, not `.sln`). Run `dotnet` commands from the repo root.

```bash
dotnet build src/Bizcore.slnx                              # build everything
dotnet run --project src/Services/Invoice/Invoice.API      # run one service locally
docker compose up -d --build                                # full stack: all services + SQL Server, RabbitMQ, Redis, MinIO, LGTM stack

dotnet test src/Tests/Bizcore.UnitTests                     # unit tests (InMemory/mocks, fast)
dotnet test src/Tests/Bizcore.ApiTests                      # integration tests (Testcontainers — needs Docker running)
dotnet test src/Tests/Bizcore.UnitTests --filter "FullyQualifiedName~CreateInvoiceCommandHandlerTests.CreateAsync_WithValidData_ReturnsInvoice"  # single test

dotnet build src/Tests/Bizcore.E2ETests                     # E2E (Playwright) — needs full stack already running
pwsh src/Tests/Bizcore.E2ETests/bin/Debug/net10.0/playwright.ps1 install   # once, after first build
dotnet test src/Tests/Bizcore.E2ETests

dotnet ef migrations add <Name> --project src/Services/Invoice/Invoice.API --startup-project src/Services/Invoice/Invoice.API
dotnet ef database update --project src/Services/Invoice/Invoice.API --startup-project src/Services/Invoice/Invoice.API

./run-tests.sh   # or run-tests.ps1 — ApiTests + HTML coverage dashboard
```

Frontend (`src/WebUI`, React/Vite) is a separate `npm` project — `npm install` / `npm run dev` inside that directory.

## Architecture

Event-driven microservices ERP (.NET 8). Core flow: **Identity/Auth → Invoice → Payment → Report**, observed end-to-end by a centralized **Audit** service.

### Services (`src/Services/*/*.API`)

Admin, Invoice, Payment, Report, Orchestration, Audit, File, Customer, Order, Product — each an independently deployable ASP.NET Core Web API with its own logical SQL Server database (`InvoiceDb`, `PaymentDb`, …, one shared instance). All traffic enters through `src/Gateway/Gateway.API` (YARP), which also does centralized authentication. Every service needs three registrations beyond its own folder — easy to miss one:
1. `src/Bizcore.slnx` — a `/Services/{Name}/` `<Folder>` entry (cosmetic: solution/IDE visibility only, doesn't affect runtime).
2. `Gateway.API/appsettings.json` — a route + cluster.
3. `docker-compose.yml` — the service block.

### 4-layer DDD-Lite structure, identical in every service

```
{Service}.API/
├── Domain/           Entities, Enums, Exceptions — no framework/DB dependencies
├── Application/       Commands/Queries + Handlers (MediatR), Consumers (MassTransit), DTOs, Validators
├── Infrastructure/     AppDbContext, EF Migrations, Data/Configurations/{Entity}Configuration.cs, external Clients
└── Controllers/        HTTP endpoints only — binds request, calls MediatR, returns result; no business logic
```

`Program.cs` follows one fixed shape across every service (see `src/Services/Order/Order.API/Program.cs` for the canonical, current example — copy it rather than reconstructing from memory):

```csharp
builder.AddServiceDefaults("{Service}.API");                 // logging, OpenTelemetry, health checks
builder.Services.AddBizcoreAuth(builder.Configuration);
builder.Services.AddBizcoreVersioning();
builder.Services.AddBizcoreSwagger("BizCore {Service} API", "description");
builder.Services.AddBizcoreModule<{Service}Module>(builder);  // all DI for this service lives in {Service}Module : IServiceModule

var app = builder.Build();
app.MapDefaultEndpoints("BizCore {Service} API v1");

await app.Services.MigrateDatabaseAsync<AppDbContext>();      // + DbSeeder.SeedAsync(...) for business-data seeding
app.Run();

public partial class Program { }                              // required for WebApplicationFactory in Bizcore.ApiTests
```

Read `{Service}Module.cs` first when exploring an unfamiliar service — that's where its dependencies are registered, not `Program.cs`.

Aggregate roots inherit `AggregateRoot` (all entities inherit `BaseEntity`: `Id`/`CreatedAt`/`UpdatedAt`/`Version`). `Version` increments only when a business method calls `MarkStateChanged()`, which drives optimistic concurrency via `EntityVersionInterceptor`. Never assign `entity.Version` from a DTO — set `.Property(x => x.Version).OriginalValue = request.Version` on the EF entry instead. New child entities added to an aggregate's collection need explicit `_context.Set<TChild>().Add(child)` or EF may mistrack them as `Modified`.

### Shared code (`src/BuildingBlocks/`)

- `Bizcore.BuildingBlocks` — `ErrorCodes`, `Permissions`, `QueueNames`, audit constants, shared events/exceptions, `IServiceModule`.
- `Bizcore.BuildingBlocks.Grpc` — gRPC client/resilience helpers (`AddBizcoreGrpcClient`).
- `Bizcore.BuildingBlocks.Storage` — MinIO (S3-compatible) client.
- `Bizcore.Localization` — centralized i18n resources.

### Inter-service communication

- Default to **async messaging** (RabbitMQ/MassTransit), not direct HTTP, between services.
- **Transactional Inbox**: every MassTransit consumer is auto-wrapped in a DB transaction. Never call `BeginTransactionAsync()` inside `Consume`/`Handle` — nesting throws `InvalidOperationException`.
- **Outbox**: `_publishEndpoint.Publish` writes to `OutboxMessages` in the same transaction as the business data; a background worker delivers it — this is what keeps DB-write + message-publish atomic.
- **gRPC is query-only**, max 2 hops, never for commands. Wrap clients in a proxy service (e.g. `AuditClientService`) rather than injecting them directly into business logic.
- Handlers never call `SaveChangesAsync()` — `TransactionBehavior` (MediatR pipeline) commits via `IUnitOfWork` after the handler succeeds.

### Saga orchestration

Multi-step cross-service flows (e.g. Payment → Invoice approval → notification) are MassTransit State Machine Sagas in `Orchestration.API` (`Application/Sagas/*Saga.cs` + `Domain/Entities/*SagaState.cs`). `Orchestration.API` carries no business logic itself — it sequences events/commands and records `ProcessFlow`/`FlowStep` for observability.

### Authorization

Dynamic permission-code checks (`Permissions` class in `Bizcore.BuildingBlocks`), backed by Redis cache with JWT-claims fallback — changes take effect immediately via cache invalidation, no re-login needed. Every endpoint except public/auth ones needs `[RequirePermission(Permissions.X.Y)]`. New permissions: add the constant to `Permissions.cs`, then seed it in `src/Services/Admin/Admin.API/Infrastructure/Data/DbSeeder.cs` — that's the single central permission catalog; other services' `DbSeeder.cs` files seed business data only, not permissions.

### Error handling & observability

- Throw typed exceptions (`DomainException`, `NotFoundException`, etc.) carrying an `ErrorCodes` constant — never a raw string. `GlobalExceptionMiddleware` standardizes the HTTP response; the frontend (react-i18next) localizes it.
- PII fields on DTOs need `[SensitiveData]` (`Sensitive` = masked in logs, `Restricted` = stripped entirely).
- `X-Correlation-ID` and `X-Culture` propagate automatically across HTTP and RabbitMQ — don't thread them manually.
- Mutating endpoints handle idempotency (`X-Idempotency-Key`); RabbitMQ consumers must be safe to reprocess.

## Adding a new microservice

A [new-service skill](.claude/skills/new-service/SKILL.md) automates this checklist; full narrative in [docs/01-getting-started/DEV_GUIDE.md](docs/01-getting-started/DEV_GUIDE.md) — note its `Program.cs` template there predates the real one shown above, so prefer copying an existing service's actual `Program.cs`.

## Git workflow

Reduced Git Flow: `main` (production, never commit directly) ← `develop` (integration) ← `feature/BC-{ID}-{desc}` / `bugfix/BC-{ID}-{desc}` / `hotfix/BC-{ID}-{desc}`. Conventional Commits (`feat`, `fix`, `docs`, `refactor`, `perf`, `test`, `chore` — `type(scope): description`). PRs target `develop`, need ≥1 approval and passing CI, merge via Squash and Merge. Detail: [docs/06-conventions/GIT_WORKFLOW.md](docs/06-conventions/GIT_WORKFLOW.md).

## Project skills

`.claude/skills/` — [new-service](.claude/skills/new-service/SKILL.md), [ef-migration](.claude/skills/ef-migration/SKILL.md), [add-permission](.claude/skills/add-permission/SKILL.md), [bizcore-code-review](.claude/skills/bizcore-code-review/SKILL.md). These trigger automatically on matching requests; invoke explicitly with `/new-service` etc. if needed.
