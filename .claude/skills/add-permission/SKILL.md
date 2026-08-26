---
name: add-permission
description: Add a new authorization permission end-to-end in Bizcore ERP — the constant in Bizcore.BuildingBlocks.Permissions, the seed entry in Admin.API's DbSeeder that makes it exist in the database, and the [RequirePermission] attribute on the controller action. Use whenever the user wants to add a new permission/role capability, protect a new endpoint, or mentions "RequirePermission", "quyền", "phân quyền", or a 403/permission-denied issue on an endpoint that currently has no permission check.
---

# Adding a permission (Bizcore ERP)

Source: [docs/01-getting-started/DEV_GUIDE.md](../../../docs/01-getting-started/DEV_GUIDE.md) §4.3, and the Authorization callout in [docs/02-project-overview/PROJECT_INDEX.md](../../../docs/02-project-overview/PROJECT_INDEX.md).

Permissions are string codes, checked dynamically at request time against Redis (JWT claims as fallback) rather than baked permanently into a token — that's what lets a permission grant take effect for an already-logged-in user without forcing a re-login.

## 1. Define the constant

Add it to `src/BuildingBlocks/Bizcore.BuildingBlocks/Permissions.cs`, in the nested static class for the relevant entity (create one if the entity is new). Follow the existing `{Entity}.{Action}` naming — see `Permissions.Product` or `Permissions.Order` for the current pattern (`View`, `Create`, `Update`, plus whatever entity-specific action fits, e.g. `Cancel`, `Deactivate`):

```csharp
public static class {Entity}
{
    public const string View = "{Entity}.View";
    public const string Create = "{Entity}.Create";
}
```

If the entity needs its own navigation item, also add a `Permissions.Menu.{Entity}` constant (categories are Menu / Page / Action / Field — most CRUD permissions are Action-level).

## 2. Seed it — in Admin.API, not the owning service

A constant existing in code isn't enough; it must exist as a database row before any role can be granted it. Add it to `src/Services/Admin/Admin.API/Infrastructure/Data/DbSeeder.cs`, next to the other `PermDef` entries — this is the **single central permission catalog** for the entire system, regardless of which service the permission actually protects (e.g. `Invoice`/`Payment`/`Product` permissions are all seeded here, not in `Invoice.API`'s own `DbSeeder.cs`, which only seeds Invoice business data). Seeding it in the wrong service's seeder is a common mistake that leaves the permission silently missing from every environment.

## 3. Protect the endpoint

```csharp
[RequirePermission(Permissions.{Entity}.{Action})]
[HttpPost]
public async Task<ActionResult<...>> Create(...)
```

See any existing controller (e.g. `src/Services/Product/Product.API/Controllers/ProductsController.cs`) for the pattern — every action pairs an `[Http...]` attribute with `[RequirePermission]`. A bare `[Authorize]` with no permission argument only proves the caller is logged in, not that they're allowed to do this specific action — that's a gap, not a valid check.

## 4. Grant it

Assign the permission to a role via the admin UI or `POST /api/v1/roles/{id}/permissions`. It takes effect immediately for already-logged-in users through real-time cache invalidation over the event bus — no restart or re-login required.

## Debugging a permission that isn't taking effect

- Check the JWT's `permission` claim at [jwt.io](https://jwt.io).
- Check the Redis key `user_permissions:{userId}` (Redis Insight) — checked before the JWT fallback.
- Enable `Debug`-level logging on `Bizcore.BuildingBlocks.Authorization` to see policy evaluation.
- Confirm the constant was actually seeded (step 2) — a permission that exists only in `Permissions.cs` but never made it into `Admin.API`'s `DbSeeder.cs` will never be assignable to a role.
