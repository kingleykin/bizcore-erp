using Admin.API;
using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── 1. Host & Service Registrations (Standardized) ──────────────────────────
builder.AddServiceDefaults("Admin.API");

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 5 * 1024 * 1024; // 5 MB
});

builder.Services.AddBizcoreAuth(builder.Configuration);
builder.Services.AddBizcoreVersioning();
builder.Services.AddBizcoreSwagger("BizCore Admin API", "Enterprise Organization, Master Data & Identity Service");

// Load Service Specific Module
builder.Services.AddBizcoreModule<AdminModule>(builder);

// ── 2. App Pipeline ───────────────────────────────────────────────────────────
var app = builder.Build();

app.MapDefaultEndpoints("BizCore Admin API v1");

// Additional security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    await next();
});

// Database Initialization & Seeding
try
{
    await app.Services.MigrateDatabaseAsync<AdminDbContext>();
    using var scope = app.Services.CreateScope();
    await DbSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AdminDbContext>(), scope.ServiceProvider.GetRequiredService<ILogger<Program>>());
}
catch (Exception ex) { Log.Error(ex, "Error occurred during Admin database initialization."); throw; }

app.Run();

namespace Admin.API
{
    public partial class Program { }
}
