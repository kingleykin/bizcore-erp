---
name: ef-migration
description: Add or update an Entity Framework Core migration for a Bizcore ERP microservice — configuring a new entity with a Fluent API IEntityTypeConfiguration class, registering its DbSet, running dotnet ef migrations add/database update against the right service, and following the repo's optimistic-concurrency (Version/MarkStateChanged) rules. Use whenever the user wants to add a new table/entity, change a schema, add a column, or mentions "migration", "dotnet ef", "DbContext", or a new field/entity on an existing aggregate in one of the src/Services/*/*.API projects.
---

# EF Core migration workflow (Bizcore ERP)

Narrative: [docs/01-getting-started/DEV_GUIDE.md](../../../docs/01-getting-started/DEV_GUIDE.md) §"Bước 3: Cấu hình DB & Migrations"; concurrency model: [docs/02-project-overview/PROJECT_INDEX.md](../../../docs/02-project-overview/PROJECT_INDEX.md) §4.3.

Identify the target service (e.g. `Invoice` → `src/Services/Invoice/Invoice.API`) — every `dotnet ef` command below needs both `--project` and `--startup-project` pointed at that same service, since each microservice owns its own `AppDbContext` and its own `Infrastructure/Migrations/` folder. Pointing at the wrong service silently generates a migration against the wrong schema.

## 1. Configure the entity — Fluent API, not data annotations

Domain entities stay free of persistence attributes (`[Required]`, `[MaxLength]`, etc.) — that's what keeps the Domain layer usable without EF Core loaded. Create `{EntityName}Configuration.cs` under `Infrastructure/Data/Configurations/`. For the exact current shape, read an existing configuration in the target service rather than a generic template — e.g. `src/Services/Order/Order.API/Infrastructure/Data/Configurations/OrderConfiguration.cs` shows `HasKey`, `HasIndex` (including unique indexes), `Property(...).HasMaxLength(...).IsRequired()`, and enum-to-string conversion via `.HasConversion<string>()`. Don't configure `Version`/concurrency here — that's applied globally via `ModelBuilderExtensions`.

These configuration classes are picked up automatically by `modelBuilder.ApplyConfigurationsFromAssembly(...)` in `AppDbContext.OnModelCreating` — creating the file is enough, no manual registration.

## 2. Register the DbSet

In the target service's `AppDbContext.cs`:

```csharp
public DbSet<{EntityName}> {EntityNamePlural} { get; set; }
```

## 3. Concurrency rules — get these right before generating the migration

- Entities inherit `BaseEntity` (`Id`/`CreatedAt`/`UpdatedAt`/`Version`). If the entity is a transaction boundary that owns child collections, it must inherit `AggregateRoot` instead — only `AggregateRoot` types get automatic version tracking via `EntityVersionInterceptor`.
- `Version` increments only when a business method explicitly calls `MarkStateChanged()` — deliberate, not automatic dirty-tracking, so only intentional business mutations count as concurrency-relevant.
- When a handler updates an entity from a DTO, never assign `entity.Version = request.Version` directly (that bypasses the concurrency check). Set the EF entry's original value instead — see the real usage in `src/Services/Invoice/Invoice.API/Application/Commands/UpdateInvoiceStatusCommandHandler.cs`:
  ```csharp
  _context.Entry(entity).Property(x => x.Version).OriginalValue = request.Version;
  ```
- When adding a new child entity to an aggregate's collection (e.g. an `OrderItem` added to `Order`), EF can misjudge it as `Modified` instead of `Added` if the child's Guid is already assigned. Register it explicitly: `_context.Set<{ChildEntity}>().Add(child);`

## 4. Generate and apply

```bash
dotnet ef migrations add {DescriptiveName} --project src/Services/{Service}/{Service}.API --startup-project src/Services/{Service}/{Service}.API
dotnet ef database update --project src/Services/{Service}/{Service}.API --startup-project src/Services/{Service}/{Service}.API
```

Migration files land in that service's `Infrastructure/Migrations/`. If the `ef` command isn't found: `dotnet tool install --global dotnet-ef`.

## 5. Sanity check before applying

Review the generated migration's `Up()`/`Down()` — confirm it only touches the intended tables. A wrong `--project`/`--startup-project` pair is the most common cause of a migration landing against the wrong service's schema.
