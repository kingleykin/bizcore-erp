using Bizcore.BuildingBlocks.Infrastructure;
using Report.API;
using Report.API.Infrastructure.Data;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── 1. Host & Service Registrations (Standardized) ──────────────────────────
builder.AddServiceDefaults("Report.API");

builder.Services.AddBizcoreAuth(builder.Configuration);
builder.Services.AddBizcoreVersioning();
builder.Services.AddBizcoreSwagger("BizCore Report API", "Read-only Data Analytics & Reporting Service");

// Load Service Specific Module
builder.Services.AddBizcoreModule<ReportModule>(builder);

// ── 2. App Pipeline ───────────────────────────────────────────────────────────
var app = builder.Build();

app.MapDefaultEndpoints("BizCore Report API v1");

// Database Initialization & Seeding
try
{
    await app.Services.MigrateDatabaseAsync<AppDbContext>();
    using var scope = app.Services.CreateScope();
    await DbSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), scope.ServiceProvider.GetRequiredService<ILogger<Program>>());
}
catch (Exception ex) { Log.Error(ex, "Error occurred during Report database initialization."); throw; }

app.Run();
