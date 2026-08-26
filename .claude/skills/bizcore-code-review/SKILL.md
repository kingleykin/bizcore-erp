---
name: bizcore-code-review
description: Review a diff/PR in the Bizcore ERP repo against this project's own architecture and coding rules — Domain/Application/Infrastructure/API layering, MassTransit Transactional Inbox, RequirePermission, ErrorCodes, SensitiveData, EF Core concurrency. Use this in addition to (or instead of) a generic code review whenever reviewing changes inside src/Services/*, src/Gateway, or src/BuildingBlocks/*, or whenever the user asks to review/check a PR, diff, or "does this follow our conventions" for this repo specifically.
---

# Bizcore ERP code review checklist

This is the condensed, architecture-aware checklist for reviewing an actual diff. The full generic conventions live in [docs/06-conventions/CODE_REVIEW_GUIDE.md](../../../docs/06-conventions/CODE_REVIEW_GUIDE.md) and [docs/06-conventions/CODING_CONVENTIONS.md](../../../docs/06-conventions/CODING_CONVENTIONS.md); this skill covers the rules specific to this repo's architecture that a generic reviewer wouldn't know to check.

Read the diff, then walk it against each check below. For every finding, cite the file/line and the specific rule violated rather than asserting a vague concern — that's what lets the author verify it instead of taking it on faith.

## Architecture (Critical — these break runtime guarantees, not just style)

- **No business logic in Controllers.** An action should be ~3 lines: bind request → `_mediator.Send(command)` → return result. Any `if`/validation/calculation beyond that belongs in the Domain entity or a Command/Query Handler.
- **Domain stays framework-free.** No `DbContext`, ASP.NET types, or MassTransit types inside `Domain/`. If Domain code needs something from outside itself, that logic likely belongs in Application instead.
- **No nested transactions in Consumers/Handlers.** MassTransit already wraps every `Consume`/transactional-command `Handle` in a DB transaction (Transactional Inbox). A handler calling `_context.Database.BeginTransactionAsync()` or `_unitOfWork.BeginTransactionAsync()` throws `InvalidOperationException` at runtime — flag this as a bug, not a nit.
- **Handlers don't call `SaveChangesAsync()`.** `TransactionBehavior` commits via `IUnitOfWork` after the handler returns successfully; an explicit save inside a handler is either redundant or commits before the pipeline's own validation runs.
- **Inter-service calls go through events, not direct HTTP**, except where gRPC is deliberately used for a query. An `HttpClient` call from one service to another (outside a documented exception) is a coupling smell — ask whether it should be a MassTransit publish/consume instead.
- **gRPC is query-only, max 2 hops.** A gRPC client sending a command (anything that mutates state) is a rule violation — mutations go through RabbitMQ. Also check gRPC clients aren't injected directly into business services; they should be wrapped in a proxy (e.g. `AuditClientService`).

## Authorization & Security (Critical)

- Every non-public, non-auth endpoint carries `[RequirePermission(Permissions.X.Y)]`. A bare `[Authorize]` only proves the caller is logged in, not that they're allowed to do this specific action.
- New permission constants live in `Permissions.cs`, grouped by entity, with a matching seed entry in `src/Services/Admin/Admin.API/Infrastructure/Data/DbSeeder.cs` — the central permission catalog (see the [add-permission](../add-permission/SKILL.md) skill). A permission referenced in code but never seeded there always fails authorization in a fresh environment.
- PII fields on DTOs (email, phone, address, national ID, …) carry `[SensitiveData]` at the right level — `Sensitive` masks in logs, `Restricted` strips entirely. A PII field logged in plaintext because the tag was skipped is a data-leak bug, not a style issue.

## Exception Handling & Error Codes (Important)

Business-rule failures throw typed exceptions (`DomainException`, `NotFoundException`, `ValidationException`, `UnauthorizedException`) carrying a code from `Bizcore.BuildingBlocks.ErrorCodes` — never a raw string message, and never a bare `return BadRequest("...")` for a business rule. A missing error code should be added to `ErrorCodes`, not inlined as a string.

## Database & Concurrency (Important)

- New entities configured via Fluent API (`IEntityTypeConfiguration<T>` under `Infrastructure/Data/Configurations/`), not data annotations on the entity.
- `Version` is never assigned directly from a DTO — updates go through `.Property(x => x.Version).OriginalValue = request.Version`.
- Aggregate roots mutate state only through business methods that call `MarkStateChanged()`. A public setter that changes business-meaningful state outside such a method is a concurrency-tracking gap.
- New child entities added to an aggregate's collection are registered explicitly via `_context.Set<TChild>().Add(child)`.

## Naming & Style (Important, lower stakes than the above)

- PascalCase for classes/methods/properties, `_camelCase` for private fields, camelCase for locals/parameters, `I{Name}` for interfaces.
- Async methods end in `Async` and are awaited throughout — no `.Result`/`.Wait()` on async work.
- Events named `{Entity}{PastTenseAction}Event` (e.g. `PaymentCompletedEvent`); consumers named `{Event}Consumer`.
- Structured logging (`_logger.LogInformation("InvoiceCreated {@InvoiceEvent}", new { ... })`), never string-concatenated messages with embedded variables.

## Testing (Important)

- New business logic has a unit test in `Bizcore.UnitTests`, named `{MethodName}_{Scenario}_{ExpectedResult}`.
- Changes touching cross-service flow (event publish/consume) are covered in `Bizcore.ApiTests`, not just unit-tested in isolation — the interesting bugs here tend to be at the integration boundary.

## Output format

Report findings as a checklist grouped by the sections above: `file:line` — what's wrong — why it matters (tied to the rule above or the doc section). Omit sections with nothing to flag rather than marking them "OK" — keep output focused on actual findings.
