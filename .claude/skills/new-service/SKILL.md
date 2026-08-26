---
name: new-service
description: Scaffold a brand-new Bizcore ERP microservice end-to-end — the ASP.NET Core project, its 4-layer folder structure, Module/Program.cs wiring, solution registration, Gateway routing, docker-compose service block, and permissions. Use this whenever the user asks to create a new microservice, add a new bounded context/domain (e.g. "add an Inventory service", "scaffold a new Shipping API"), or says a new service is missing from the Gateway/docker-compose/solution. Also use it if a service directory already exists under src/Services but is missing one of these pieces (e.g. "the Order service isn't showing up in the IDE" or "wire up the new Product service").
---

# New Bizcore microservice

Every microservice here is a separate ASP.NET Core Web API with its own database, wired into four places outside its own folder: the solution file, the Gateway, docker-compose, and the shared `Permissions` class. A service can build and run standalone while being invisible to the Gateway, absent from `docker compose up`, or missing from the IDE's solution — skipping any one step below produces exactly that silent gap. Treat the checklist as one unit of work.

Narrative version: [docs/01-getting-started/DEV_GUIDE.md](../../../docs/01-getting-started/DEV_GUIDE.md) §8 (checklist), §2 (folder layout), §4.3 (permissions), §4.5 (PII). Its `Program.cs`/Module snippet is stale relative to the real code — step 2 below points at the current source of truth instead of repeating it here.

Pick the service name once and reuse it everywhere: `{Name}.API` (project), `{name}-api` (docker hostname/service), `{Name}Db` (database).

## 1. Project + folder structure

Create `src/Services/{Name}/{Name}.API/{Name}.API.csproj`, referencing `Bizcore.BuildingBlocks` — copy the `<ProjectReference>` from an existing `.csproj` (e.g. `src/Services/Order/Order.API/Order.API.csproj`) rather than typing it from memory, since the relative path depth must match exactly. Folders:

```
{Name}.API/
├── Domain/{Entities,Enums,Exceptions}/
├── Application/{Commands,Queries,Consumers,DTOs,Validators}/
├── Infrastructure/Data/{Configurations,Migrations}/
└── Controllers/
```

Domain stays framework-free: no EF Core, no ASP.NET types, no MassTransit — that isolation is what keeps business rules unit-testable without spinning up infrastructure.

## 2. Module + Program.cs — copy, don't retype

Create `{Name}Module.cs` implementing `IServiceModule`; all DI/service registration for the service goes there, which is what keeps every service's `Program.cs` identical instead of drifting.

Rather than reconstructing `Program.cs` from a template, **read and copy** `src/Services/Order/Order.API/Program.cs` (or `Product.API`/`Invoice.API` — they're all identical in shape) and adjust the names. That file is the current, compiling source of truth; the pattern includes `builder.AddServiceDefaults(...)`, `AddBizcoreAuth`/`AddBizcoreVersioning`/`AddBizcoreSwagger`, `AddBizcoreModule<{Name}Module>`, `app.MapDefaultEndpoints(...)`, a `MigrateDatabaseAsync<AppDbContext>()` + `DbSeeder.SeedAsync(...)` block, and a trailing `public partial class Program { }` (required for `WebApplicationFactory` in `Bizcore.ApiTests`) — don't drop that last line even though it looks like dead code.

## 3. Register in the solution file

Add to `src/Bizcore.slnx`, following the existing `/Services/Order/`, `/Services/Product/`, `/Services/Customer/` entries:

```xml
<Folder Name="/Services/{Name}/">
  <Project Path="Services/{Name}/{Name}.API/{Name}.API.csproj" />
</Folder>
```

Purely a solution/IDE-visibility concern — the service runs fine without this — but it's the step most often forgotten because nothing breaks at runtime when it's missing.

## 4. Gateway routing

In `src/Gateway/Gateway.API/appsettings.json`, under `"Routes"`:

```json
"{name}-route": {
  "ClusterId": "{name}-cluster",
  "Match": { "Path": "/api/v1/{name-plural}/{**catch-all}" },
  "AuthorizationPolicy": "Secure"
}
```

and under `"Clusters"`:

```json
"{name}-cluster": { "Destinations": { "d1": { "Address": "http://{name}-api:8080" } } }
```

## 5. docker-compose service block

Copy an existing service block (`order-api` or `product-api` in `docker-compose.yml` are the most recently added, cleanest templates) rather than writing one from scratch, and adjust: image/hostname/build path, `ConnectionStrings__DefaultConnection` database name, `ServiceName`, and `depends_on` (include `sql-server`, `rabbitmq`, `redis`, `loki`, plus any service this one calls synchronously via gRPC). Also add `{name}-api` to the `gateway` service's own `depends_on` list. If the service will run in a container, add `src/Services/{Name}/{Name}.API/Dockerfile` by copying an existing one and adjusting project paths.

## 6. Permissions and PII

Add constants to `src/BuildingBlocks/Bizcore.BuildingBlocks/Permissions.cs` in a new nested static class named after the entity (`Permissions.{Name}.View/Create/Update/...`). Seed them in `src/Services/Admin/Admin.API/Infrastructure/Data/DbSeeder.cs` — **not** the new service's own `DbSeeder.cs`, which seeds that service's business data only. Admin's `DbSeeder.cs` is the single central permission catalog for the whole system; see the [add-permission](../add-permission/SKILL.md) skill for the full flow. Tag PII DTO fields (name, email, phone, address, …) with `[SensitiveData]` per [docs/05-observability/LOGGING_GUIDE.md](../../../docs/05-observability/LOGGING_GUIDE.md) so they're masked/stripped from logs automatically.

## 7. Verify

```bash
dotnet build src/Bizcore.slnx     # confirms the project compiles and is picked up by the solution
docker compose config             # sanity-checks the compose file parses before a full `up -d --build`
```
