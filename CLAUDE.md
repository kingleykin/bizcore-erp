# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> Full AI-oriented architecture context also lives at [docs/02-project-overview/PROJECT_INDEX.md](docs/02-project-overview/PROJECT_INDEX.md) — read it for deeper detail than this file covers. The doc tree under `docs/` is organized numerically (`01-getting-started` … `09-testing`); [docs/README.md](docs/README.md) is the index.

## Commands

The solution file is `src/Bizcore.slnx` (new .NET `.slnx` format, not `.sln`). Run all `dotnet` commands from the repo root.

```bash
# Build everything
dotnet build src/Bizcore.slnx

# Run one service locally (each service is self-contained ASP.NET Core)
dotnet run --project src/Services/Invoice/Invoice.API

# Run the full stack (all services + SQL Server, RabbitMQ, Redis, MinIO, LGTM observability stack)
docker compose up -d --build

# Unit tests (fast, InMemory DB / mocks)
dotnet test src/Tests/Bizcore.UnitTests

# API/integration tests (Testcontainers spins up real SQL Server/Redis/RabbitMQ — requires Docker running)
dotnet test src/Tests/Bizcore.ApiTests

# Run a single test
dotnet test src/Tests/Bizcore.UnitTests --filter "FullyQualifiedName~CreateInvoiceCommandHandlerTests.CreateAsync_WithValidData_ReturnsInvoice"

# E2E tests (Playwright; requires the full stack already running via docker compose)
dotnet build src/Tests/Bizcore.E2ETests
pwsh src/Tests/Bizcore.E2ETests/bin/Debug/net10.0/playwright.ps1 install   # once, after first build
dotnet test src/Tests/Bizcore.E2ETests

# EF Core migrations (run against a specific service, e.g. Invoice)
dotnet ef migrations add <MigrationName> --project src/Services/Invoice/Invoice.API --startup-project src/Services/Invoice/Invoice.API
dotnet ef database update --project src/Services/Invoice/Invoice.API --startup-project src/Services/Invoice/Invoice.API

# Full test run + HTML coverage dashboard (ApiTests project)
./run-tests.sh      # Linux/macOS
./run-tests.ps1      # Windows
```

Frontend (`src/WebUI`, React/Vite) has its own `package.json` — use `npm install` / `npm run dev` inside that directory.

## Architecture

**Bizcore ERP** is an event-driven microservices ERP system (.NET 8). Core business flow: **Identity/Auth → Invoice → Payment → Report**, observed end-to-end by a centralized **Audit** service.

### Services (`src/Services/*/*.API`)

Admin, Invoice, Payment, Report, Orchestration, Audit, File, Customer, Order, Product — each is an independently deployable ASP.NET Core Web API with its own SQL Server database (logical DBs on one shared instance: `InvoiceDb`, `PaymentDb`, etc.). All traffic enters through `src/Gateway/Gateway.API` (YARP reverse proxy), which also does centralized authentication. Routes/clusters for each service are registered in `Gateway.API/appsettings.json`, and each service container is wired into `docker-compose.yml`.

### Every service follows the same 4-layer DDD-Lite structure

```
{Service}.API/
├── Domain/          # Entities, Enums, Exceptions — no framework dependencies, no DB context
├── Application/      # Commands/Queries + Handlers (MediatR), Consumers (MassTransit), DTOs, Validators
├── Infrastructure/    # AppDbContext, EF Core Migrations, EntityTypeConfigurations, external Clients
└── Controllers/       # HTTP endpoints only — no business logic here
```

- `Program.cs` is intentionally thin: it wires up `AddServiceDefaults()` (logging, OpenTelemetry, health checks) plus `AddBizcoreModule<{Service}Module>()`. All DI/service registration for a service lives in its `{Service}Module.cs` implementing `IServiceModule` — that's the file to read first to understand what a service depends on.
- Aggregate roots inherit `AggregateRoot` (all entities inherit `BaseEntity` for `Id`/`CreatedAt`/`UpdatedAt`/`Version`). Version only increments when a business method calls `MarkStateChanged()` — this drives optimistic concurrency via `EntityVersionInterceptor`. Never assign `entity.Version` directly from a DTO; always set `.Property(x => x.Version).OriginalValue` on the EF entry.
- Entity Framework config lives in per-entity `IEntityTypeConfiguration<T>` classes under `Infrastructure/Data/Configurations/`, not inline in `OnModelCreating`.

### Shared code (`src/BuildingBlocks/`)

- `Bizcore.BuildingBlocks` — cross-cutting contracts: `ErrorCodes`, `Permissions`, `QueueNames`, audit constants, shared events/exceptions, `IServiceModule`.
- `Bizcore.BuildingBlocks.Grpc` — gRPC client/resilience helpers (`AddBizcoreGrpcClient`).
- `Bizcore.BuildingBlocks.Storage` — MinIO (S3-compatible) client for file/object storage.
- `Bizcore.Localization` — centralized i18n resource management.

### Inter-service communication rules

- **Default to async messaging** (RabbitMQ via MassTransit), not direct HTTP, between services.
- **Transactional Inbox**: every MassTransit consumer is automatically wrapped in a DB transaction by the infrastructure. Never call `BeginTransactionAsync()` inside a `Consume`/`Handle` method — it's already open and nesting throws `InvalidOperationException`.
- **Outbox Pattern**: publishing via `_publishEndpoint.Publish` writes to an `OutboxMessages` table in the same transaction as the business data; a background worker delivers it — this is how DB-write + message-publish stays atomic.
- **gRPC is query-only** (synchronous reads between services, max 2 hops in a chain) — never used for commands/writes. Don't inject a raw gRPC client into a business service; wrap it in a proxy service (e.g. `AuditClientService`).
- Handlers never call `SaveChangesAsync()` themselves — `TransactionBehavior` (MediatR pipeline) commits via `IUnitOfWork` after the handler succeeds.

### Saga orchestration

Multi-step cross-service business processes (e.g. Payment → Invoice approval → notification) are modeled as MassTransit State Machine Sagas in `Orchestration.API` (`Application/Sagas/*Saga.cs` + `Domain/Entities/*SagaState.cs`). `Orchestration.API` itself carries no business logic — it only sequences events/commands and tracks `ProcessFlow`/`FlowStep` for observability.

### Authorization

Dynamic, permission-code-based (`Permissions` class in `Bizcore.BuildingBlocks`), backed by Redis cache with JWT-claims fallback. Every new endpoint (except public/auth ones) must carry `[RequirePermission(Permissions.X.Y)]`. Adding a new permission means: add the constant to `Permissions`, then seed it in the relevant service's `DbSeeder`.

### Error handling & observability

- Throw typed exceptions (`DomainException`, `NotFoundException`, etc.) carrying a code from `Bizcore.BuildingBlocks.ErrorCodes` — never return raw error strings. `GlobalExceptionMiddleware` translates these to standardized HTTP responses that the frontend (react-i18next) localizes.
- PII/sensitive fields on DTOs must carry `[SensitiveData]` (`Sensitive` = masked, `Restricted` = stripped entirely from logs).
- Every request carries `X-Correlation-ID` and `X-Culture`; both propagate automatically across HTTP and RabbitMQ — don't read/thread them manually.
- Mutating endpoints must handle idempotency (`X-Idempotency-Key`), and RabbitMQ consumers must be safe to reprocess.

## Adding a new microservice

Full walkthrough: [docs/01-getting-started/DEV_GUIDE.md](docs/01-getting-started/DEV_GUIDE.md). Checklist:

1. New ASP.NET Core Web API project, referencing `Bizcore.BuildingBlocks`.
2. Add a `{Service}Module : IServiceModule`; keep `Program.cs` to the standard template (`AddServiceDefaults`, `AddBizcoreTelemetry`, `AddBizcoreInfrastructure`, `AddBizcoreAuth`, `AddBizcoreModule<T>`).
3. Register the project in `src/Bizcore.slnx` under a new `/Services/{Name}/` folder — easy to forget since it's not required for the service to run, only to show up in the IDE/solution.
4. Add a route + cluster entry in `src/Gateway/Gateway.API/appsettings.json`.
5. Add the service (and any Dockerfile) to `docker-compose.yml`.
6. Define new permissions in `Bizcore.BuildingBlocks.Permissions` and seed them.
7. Tag PII fields with `[SensitiveData]`.

## Git workflow

Reduced Git Flow: `main` (production, never commit directly) ← `develop` (integration) ← `feature/BC-{ID}-{desc}` / `bugfix/BC-{ID}-{desc}` / `hotfix/BC-{ID}-{desc}`. Commits follow Conventional Commits (`feat`, `fix`, `docs`, `refactor`, `perf`, `test`, `chore` — `type(scope): description`). PRs target `develop`, need ≥1 approval and passing CI, and merge via Squash and Merge. Full detail: [docs/06-conventions/GIT_WORKFLOW.md](docs/06-conventions/GIT_WORKFLOW.md).
