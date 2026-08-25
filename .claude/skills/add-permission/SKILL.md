---
name: add-permission
description: Add a new authorization permission end-to-end in Bizcore ERP — the constant in Bizcore.BuildingBlocks.Permissions, the DbSeeder entry that makes it exist in the database, and the [RequirePermission] attribute on the controller action. Use whenever the user wants to add a new permission/role capability, protect a new endpoint, or mentions "RequirePermission", "quyền", "phân quyền", or a 403/permission-denied issue on an endpoint that currently has no permission check.
---

# Adding a permission (Bizcore ERP)

Source: [docs/01-getting-started/DEV_GUIDE.md](../../../docs/01-getting-started/DEV_GUIDE.md) section 4.3, and the Authorization callout in [docs/02-project-overview/PROJECT_INDEX.md](../../../docs/02-project-overview/PROJECT_INDEX.md).

Permissions in this system are simple string codes, centrally defined, checked dynamically at request time against Redis (with JWT claims as fallback) rather than baked into the token forever — that's what lets a permission change take effect for a logged-in user without forcing a re-login.

## 1. Define the constant

Add it to the nested static class for the relevant entity/service in `src/BuildingBlocks/Bizcore.BuildingBlocks/Permissions.cs`. Follow the existing `{Entity}.{Action}` naming (`View`, `Create`, `Update`, `Delete`/`Cancel`, etc. — match whatever verbs are idiomatic for that entity):

```csharp
public static class {Entity}
{
    public const string View = "{Entity}.View";
    public const string Create = "{Entity}.Create";
    // ...
}
```

If this is a brand-new entity/module, also add a `Menu.{Entity}` constant if it needs its own nav item — permissions are categorized as Menu / Page / Action / Field, and most CRUD permissions are Action-level.

## 2. Seed it

A permission constant existing in code isn't enough — it must also exist as a row in the database for any role to be granted it. Add it to the relevant service's (usually `Admin.API`'s) `DbSeeder.cs`, alongside the other permission seed entries, so a fresh database gets it automatically.

## 3. Protect the endpoint

On the controller action:

```csharp
[RequirePermission(Permissions.{Entity}.{Action})]
[HttpPost]
public async Task<ActionResult<...>> Create(...)
```

Never leave a non-public, non-auth endpoint with bare `[Authorize]` and no permission — that only checks the user is logged in, not that they're allowed to do this specific action.

## 4. Grant it

Assign the new permission to a role either via the admin UI or `POST /api/v1/roles/{id}/permissions`. Because of real-time cache invalidation over the event bus, this takes effect immediately for already-logged-in users — no service restart or re-login needed.

## Debugging a permission that isn't taking effect

- Check the JWT's `permission` claim at [jwt.io](https://jwt.io).
- Check the Redis key `user_permissions:{userId}` with Redis Insight — this is checked before falling back to the JWT claim.
- Turn on `Debug`-level logging for the `Bizcore.BuildingBlocks.Authorization` namespace to see the policy evaluation in detail.
