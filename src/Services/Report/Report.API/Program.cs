using Bizcore.BuildingBlocks.Infrastructure;
using Report.API;
using Report.API.Infrastructure.Data;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── 1. Host Extensions ────────────────────────────────────────────────────────
builder.Host.AddBizcoreLogging("Report.API");

// ── 2. Service Registrations (Centralized + Module) ──────────────────────────
builder.Services.AddBizcoreTelemetry("Report.API");
builder.Services.AddBizcoreInfrastructure();
builder.Services.AddBizcoreAuth(builder.Configuration);
builder.Services.AddBizcoreVersioning();
builder.Services.AddBizcoreSwagger("BizCore Report API", "Read-only Data Analytics & Reporting Service");

// Load Service Specific Module
builder.Services.AddBizcoreModule<ReportModule>(builder);

// ── 3. App Pipeline ───────────────────────────────────────────────────────────
var app = builder.Build();

app.UseBizcorePipeline("BizCore Report API v1");

// Database Initialization & Seeding
try
{
    await app.Services.MigrateDatabaseAsync<AppDbContext>();
    using var scope = app.Services.CreateScope();
    await DbSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), scope.ServiceProvider.GetRequiredService<ILogger<Program>>());
}
catch (Exception ex) { Log.Error(ex, "Error occurred during Report database initialization."); throw; }

app.Run();
