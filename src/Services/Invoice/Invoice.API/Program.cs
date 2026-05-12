using Invoice.API;
using Invoice.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── 1. Host Extensions ────────────────────────────────────────────────────────
builder.Host.AddBizcoreLogging("Invoice.API");

// ── 2. Service Registrations (Centralized + Module) ──────────────────────────
builder.Services.AddBizcoreTelemetry("Invoice.API");
builder.Services.AddBizcoreInfrastructure();
builder.Services.AddBizcoreAuth(builder.Configuration);
builder.Services.AddBizcoreVersioning();
builder.Services.AddBizcoreSwagger("BizCore Invoice API", "Core Financial Processing Service");

// Load Service Specific Module
builder.Services.AddBizcoreModule<InvoiceModule>(builder);

// ── 3. App Pipeline ───────────────────────────────────────────────────────────
var app = builder.Build();

app.UseBizcorePipeline("BizCore Invoice API v1");

// Database Initialization & Seeding
try
{
    await app.Services.MigrateDatabaseAsync<AppDbContext>();
    using var scope = app.Services.CreateScope();
    await DbSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), scope.ServiceProvider.GetRequiredService<ILogger<Program>>());
}
catch (Exception ex) { Log.Error(ex, "Error occurred during Invoice database initialization."); throw; }

app.Run();
