---
name: new-service
description: Scaffold a brand-new Bizcore ERP microservice end-to-end — the ASP.NET Core project, its 4-layer folder structure, Module/Program.cs wiring, solution registration, Gateway routing, docker-compose service block, and permissions. Use this whenever the user asks to create a new microservice, add a new bounded context/domain (e.g. "add an Inventory service", "scaffold a new Shipping API"), or says a new service is missing from the Gateway/docker-compose/solution. Also use it if a service directory already exists under src/Services but is missing one of these pieces (e.g. "the Order service isn't showing up in the IDE" or "wire up the new Product service").
---

# New Bizcore microservice

Every microservice in this repo is a separate ASP.NET Core Web API with its own database, but they're all wired together through four places outside the service's own folder: the solution file, the Gateway, docker-compose, and the shared Permissions class. It's easy to build a perfectly working service that simply doesn't show up anywhere else in the system because one of these four registrations was skipped — the service will even run standalone and pass its own tests while being invisible to the Gateway or absent from `docker compose up`. Treat all of the steps below as one unit of work, not optional extras.

Full narrative version of this checklist: [docs/01-getting-started/DEV_GUIDE.md](../../../docs/01-getting-started/DEV_GUIDE.md) (section 8 has the checklist, section 2 the folder layout, section 4.6 the Module pattern, section 4.3 permissions, section 4.5 logging/PII).

Ask the user for the service name if not given (e.g. `Shipping`), and use it consistently: `{Name}.API` as the project, `{name}-api` as the docker service/hostname, `{Name}Db` as the database name.

## 1. Create the project and folder structure

Under `src/Services/{Name}/{Name}.API/`, create an ASP.NET Core Web API project (`{Name}.API.csproj`) referencing `Bizcore.BuildingBlocks` (copy the `<ProjectReference>` pattern from an existing service like `src/Services/Order/Order.API/Order.API.csproj`). Add the standard 4-layer folders:

```
{Name}.API/
├── Domain/
│   ├── Entities/
│   ├── Enums/
│   └── Exceptions/
├── Application/
│   ├── Commands/
│   ├── Queries/
│   ├── Consumers/
│   ├── DTOs/
│   └── Validators/
├── Infrastructure/
│   ├── Data/
│   │   └── Configurations/
│   └── Migrations/
└── Controllers/
```

Domain must stay framework-free (no EF Core, no ASP.NET types) — that's what keeps business rules testable in isolation.

## 2. Module pattern + Program.cs

Create `{Name}Module.cs` implementing `IServiceModule`, and move all DI/service registration into it — this is what keeps `Program.cs` a thin, uniform bootstrap across every service in the repo instead of each one drifting into its own snowflake setup. `Program.cs` should look like:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Host.AddBizcoreLogging("{Name}.API");

builder.Services.AddBizcoreTelemetry("{Name}.API");
builder.Services.AddBizcoreInfrastructure();
builder.Services.AddBizcoreAuth(builder.Configuration);
builder.Services.AddBizcoreVersioning();
builder.Services.AddBizcoreSwagger("{Name} API", "Description");

builder.Services.AddBizcoreModule<{Name}Module>(builder);

var app = builder.Build();
app.UseBizcorePipeline("{Name} API v1");
app.Run();
```

## 3. Register in the solution file

Add the project to `src/Bizcore.slnx` under a new folder, following the existing pattern (see the `/Services/Order/`, `/Services/Product/`, `/Services/Customer/` entries for the exact shape):

```xml
<Folder Name="/Services/{Name}/">
  <Project Path="Services/{Name}/{Name}.API/{Name}.API.csproj" />
</Folder>
```

Without this, the project builds and runs fine standalone, but it won't appear in an IDE that opens `Bizcore.slnx`, and `dotnet build src/Bizcore.slnx` won't build it either.

## 4. Gateway routing

In `src/Gateway/Gateway.API/appsettings.json`, add a route and a cluster (lowercase, hyphenated, matching the docker hostname):

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

Add a `{name}-api` service to `docker-compose.yml`. Copy the shape of an existing service block verbatim (`order-api` or `product-api` are good templates) and adjust names/ports/dependencies:

```yaml
{name}-api:
  image: bizcore-{name}-api
  build:
    context: .
    dockerfile: src/Services/{Name}/{Name}.API/Dockerfile
  environment:
    - ASPNETCORE_ENVIRONMENT=Development
    - ASPNETCORE_URLS=http://+:8080
    - ConnectionStrings__DefaultConnection=Server=sql-server;Database={Name}Db;User Id=sa;Password=Password123!;TrustServerCertificate=True
    - ConnectionStrings__Redis=redis:6379
    - RabbitMQ__Host=rabbitmq
    - Loki__Url=http://loki:3100
    - OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
    - ServiceName={Name}.API
    - Jwt__SecretKey=BizcoreERPSecretKeyMustBeVeryLongAndSecure!!!
  depends_on:
    - sql-server
    - rabbitmq
    - redis
    - loki
```

Add any other services this one calls synchronously (e.g. `- customer-api`) to `depends_on`, and add `{name}-api` to the Gateway's own `depends_on` list so the Gateway waits for it too. Also add a `{Name}.API/Dockerfile` if the service will run in Docker (copy an existing service's Dockerfile and adjust project paths).

## 6. Permissions and PII

Add permission constants to `Bizcore.BuildingBlocks.Permissions`, grouped in a nested static class named after the entity (e.g. `Permissions.{Name}.View/Create/Update/Delete`), and seed them in the new service's `DbSeeder` so they actually exist in the database — see the [add-permission](../add-permission/SKILL.md) skill for the full flow. Tag any PII fields on DTOs (name, email, phone, address, etc.) with `[SensitiveData(Level = SensitiveLevel.Sensitive)]` or `.Restricted` per [docs/05-observability/LOGGING_GUIDE.md](../../../docs/05-observability/LOGGING_GUIDE.md), so they get masked/stripped from logs automatically instead of leaking in plaintext.

## 7. Verify

Run `dotnet build src/Bizcore.slnx` to confirm the new project compiles and is picked up by the solution, then `docker compose config` to sanity-check the compose file parses before trying a full `docker compose up -d --build`.
