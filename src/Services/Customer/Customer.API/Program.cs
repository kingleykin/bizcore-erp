using Customer.API;
using Customer.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── 1. Host & Service Registrations (Standardized) ──────────────────────────
builder.AddServiceDefaults("Customer.API");

builder.Services.AddBizcoreAuth(builder.Configuration);
builder.Services.AddBizcoreVersioning();
builder.Services.AddBizcoreSwagger("BizCore Customer API", "Customer & Customer Group Management Service");

// Load Service Specific Module
builder.Services.AddBizcoreModule<CustomerModule>(builder);

// ── 2. App Pipeline ───────────────────────────────────────────────────────────
var app = builder.Build();

app.MapDefaultEndpoints("BizCore Customer API v1");

// Database Initialization & Seeding
try
{
    await app.Services.MigrateDatabaseAsync<AppDbContext>();
    using var scope = app.Services.CreateScope();
    await DbSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), scope.ServiceProvider.GetRequiredService<ILogger<Program>>());
}
catch (Exception ex) { Log.Error(ex, "Error occurred during Customer database initialization."); throw; }

app.Run();

namespace Customer.API
{
    public partial class Program { }
}
