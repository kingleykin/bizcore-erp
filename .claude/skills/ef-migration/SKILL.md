---
name: ef-migration
description: Add or update an Entity Framework Core migration for a Bizcore ERP microservice — configuring a new entity with a Fluent API IEntityTypeConfiguration class, registering its DbSet, running dotnet ef migrations add/database update against the right service, and following the repo's optimistic-concurrency (Version/MarkStateChanged) rules. Use whenever the user wants to add a new table/entity, change a schema, add a column, or mentions "migration", "dotnet ef", "DbContext", or a new field/entity on an existing aggregate in one of the src/Services/*/*.API projects.
---

# EF Core migration workflow (Bizcore ERP)

Full narrative: [docs/01-getting-started/DEV_GUIDE.md](../../../docs/01-getting-started/DEV_GUIDE.md) ("Bước 3: Cấu hình DB & Migrations") and [docs/02-project-overview/PROJECT_INDEX.md](../../../docs/02-project-overview/PROJECT_INDEX.md) section 4.3 for the concurrency model this all sits on top of.

Identify which service you're touching (e.g. `Invoice` → `src/Services/Invoice/Invoice.API`) — every command below needs both `--project` and `--startup-project` pointed at that same service, since each microservice owns its own `AppDbContext` and migrations folder.

## 1. Configure the entity (Fluent API, not data annotations)

Don't put `[Required]`/`[MaxLength]` attributes on the entity itself — the Domain layer stays free of persistence concerns. Instead create `{EntityName}Configuration.cs` under `Infrastructure/Data/Configurations/`:

```csharp
public class {EntityName}Configuration : IEntityTypeConfiguration<{EntityName}>
{
    public void Configure(EntityTypeBuilder<{EntityName}> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        // Version/concurrency is handled globally via ModelBuilderExtensions — don't configure it here.
    }
}
```

These are picked up automatically via `modelBuilder.ApplyConfigurationsFromAssembly(...)` in `AppDbContext.OnModelCreating` — no manual registration needed beyond creating the file.

## 2. Register the DbSet

Add the entity to that service's `AppDbContext.cs`:

```csharp
public DbSet<{EntityName}> {EntityNamePlural} { get; set; }
```

## 3. Concurrency rules to get right before generating the migration

These matter at migration/entity-design time, not just at handler-writing time:

- Entities inherit `BaseEntity` (gives `Id`, `CreatedAt`, `UpdatedAt`, `Version`). If the entity is an aggregate root (owns child collections, is the transaction boundary), it must inherit `AggregateRoot` instead — only `AggregateRoot` types get automatic version tracking via `EntityVersionInterceptor`.
- `Version` only increments when a business method calls `MarkStateChanged()` inside the entity — this is a deliberate signal, not automatic dirty-tracking, so that only intentional business mutations count as a concurrency-relevant change.
- When a Handler updates an existing entity from a DTO, **never** assign `entity.Version = request.Version` directly — that bypasses EF's concurrency check entirely. Always do:
  ```csharp
  _context.Entry(entity).Property(x => x.Version).OriginalValue = request.Version;
  ```
- When adding a new child entity to an aggregate's collection (e.g. an `InvoiceLine` added to `Invoice`), EF can misjudge it as `Modified` instead of `Added` if the child already has a Guid assigned. Register it explicitly:
  ```csharp
  _context.Set<{ChildEntity}>().Add(child);
  ```

## 4. Generate and apply the migration

```bash
dotnet ef migrations add {DescriptiveName} --project src/Services/{Service}/{Service}.API --startup-project src/Services/{Service}/{Service}.API
dotnet ef database update --project src/Services/{Service}/{Service}.API --startup-project src/Services/{Service}/{Service}.API
```

The migration file lands in `Infrastructure/Migrations/` for that service. If `dotnet ef` isn't found:

```bash
dotnet tool install --global dotnet-ef
```

## 5. Sanity check

Review the generated migration's `Up()`/`Down()` before applying — confirm it only touches the tables you intended (a wrong `--project`/`--startup-project` pair, or a stray DbSet on the wrong context, can otherwise generate a migration against the wrong service's schema).
