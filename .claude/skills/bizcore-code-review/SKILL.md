---
name: bizcore-code-review
description: Review a diff/PR in the Bizcore ERP repo against this project's own architecture and coding rules — Domain/Application/Infrastructure/API layering, MassTransit Transactional Inbox, RequirePermission, ErrorCodes, SensitiveData, EF Core concurrency. Use this in addition to (or instead of) a generic code review whenever reviewing changes inside src/Services/*, src/Gateway, or src/BuildingBlocks/*, or whenever the user asks to review/check a PR, diff, or "does this follow our conventions" for this repo specifically.
---

# Bizcore ERP code review checklist

This encodes rules specific to this repo that a generic reviewer wouldn't know to check — the full generic conventions are in [docs/06-conventions/CODE_REVIEW_GUIDE.md](../../../docs/06-conventions/CODE_REVIEW_GUIDE.md) and [docs/06-conventions/CODING_CONVENTIONS.md](../../../docs/06-conventions/CODING_CONVENTIONS.md); this skill is the condensed, architecture-aware version to run against an actual diff.

Read the diff first, then walk it against each check below. For anything that fails, cite the file/line and point to the relevant doc section rather than just asserting a rule — that's what lets the author verify the finding instead of taking it on faith.

## Architecture (Critical — these break the system's guarantees, not just style)

- **No business logic in Controllers.** A controller action should be ~3 lines: bind request → `_mediator.Send(command)` → return result. Any `if`/validation/calculation beyond that belongs in the Domain entity or a Command/Query Handler.
- **Domain layer stays framework-free.** No `DbContext`, no ASP.NET types, no MassTransit types inside `Domain/`. If a Domain entity needs to reference something outside itself, that's a sign the logic belongs in Application instead.
- **No nested transactions in Consumers/Handlers.** MassTransit already wraps every `Consume`/transactional-command `Handle` in a DB transaction (Transactional Inbox). A handler calling `_context.Database.BeginTransactionAsync()` or `_unitOfWork.BeginTransactionAsync()` will throw `InvalidOperationException` at runtime — flag this immediately, it's not a style nit.
- **Handlers don't call `SaveChangesAsync()` themselves.** `TransactionBehavior` commits via `IUnitOfWork` after the handler returns successfully. A handler that saves explicitly is either redundant or (worse) committing before validation the pipeline expects to run.
- **Inter-service calls go through events, not direct HTTP**, except where gRPC is deliberately used. If you see an `HttpClient` call from one service to another for anything but a documented exception, that's a coupling smell — ask whether it should be a MassTransit publish/consume instead.
- **gRPC is query-only, and only 2 hops deep.** A gRPC client used to send a command (something that mutates state) is a rule violation — mutations go through RabbitMQ. Also check gRPC clients aren't injected directly into a business service; they should be wrapped in a proxy (e.g. `AuditClientService`).

## Authorization & Security (Critical)

- Every new non-public, non-auth endpoint must carry `[RequirePermission(Permissions.X.Y)]`. A bare `[Authorize]` with no permission argument only proves the caller is logged in, not that they're allowed to do this action — that's a gap, not a valid permission check.
- New permission constants should live in `Permissions.cs` grouped by entity, and have a matching `DbSeeder` entry (see the [add-permission](../add-permission/SKILL.md) skill for the full flow) — a permission referenced in code but never seeded will always fail authorization in a fresh environment.
- PII fields on DTOs (email, phone, address, national ID, etc.) need `[SensitiveData]` with the right level — `Sensitive` gets masked in logs, `Restricted` gets stripped entirely. A DTO field logged in plaintext that should've been tagged is a data-leak bug, not a style issue.

## Exception Handling & Error Codes (Important)

- Business-rule failures should throw typed exceptions (`DomainException`, `NotFoundException`, `ValidationException`, `UnauthorizedException`) carrying a code from `Bizcore.BuildingBlocks.ErrorCodes` — never a raw string message returned directly, and never a bare `return BadRequest("...")` for a business rule. If the error code doesn't exist yet, it should be added to `ErrorCodes`, not inlined.

## Database & Concurrency (Important)

- New entities configured via Fluent API (`IEntityTypeConfiguration<T>` under `Infrastructure/Data/Configurations/`), not data annotations on the entity.
- `Version` is never assigned directly from a DTO (`entity.Version = dto.Version` is wrong) — updates must go through `.Property(x => x.Version).OriginalValue = request.Version`.
- Aggregate roots mutate state only through business methods that call `MarkStateChanged()` — a public setter that changes business-meaningful state without going through a method that calls this is a concurrency-tracking gap.
- New child entities added to an aggregate's collection should be registered explicitly with `_context.Set<TChild>().Add(child)`.

## Naming & Style (Important, lower stakes than the above)

- PascalCase for classes/methods/properties, `_camelCase` for private fields, camelCase for locals/parameters, `I{Name}` for interfaces.
- Async methods end in `Async` and are awaited throughout (no `.Result`/`.Wait()` blocking on async work).
- Events named `{Entity}{PastTenseAction}Event` (e.g. `PaymentCompletedEvent`), consumers named `{Event}Consumer`.
- Structured logging (`_logger.LogInformation("InvoiceCreated {@InvoiceEvent}", new { ... })`), never string-concatenated log messages with embedded variables.

## Testing (Important)

- New business logic has a corresponding unit test in `Bizcore.UnitTests`, named `{MethodName}_{Scenario}_{ExpectedResult}`.
- Changes touching cross-service flow (event publish/consume) are covered in `Bizcore.ApiTests`, not just unit-tested in isolation, since the interesting bugs here tend to be at the integration boundary.

## Output format

Report findings as a checklist grouped by the sections above, each line stating: file:line, what's wrong, and why it matters (tie back to the relevant rule above or the doc section). Skip sections with nothing to flag rather than listing them as "OK" — keep the output focused on actual findings.
