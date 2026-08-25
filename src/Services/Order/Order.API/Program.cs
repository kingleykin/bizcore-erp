using Order.API;
using Order.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── 1. Host & Service Registrations (Standardized) ──────────────────────────
builder.AddServiceDefaults("Order.API");

builder.Services.AddBizcoreAuth(builder.Configuration);
builder.Services.AddBizcoreVersioning();
builder.Services.AddBizcoreSwagger("BizCore Order API", "Sales Order Management Service");

// Load Service Specific Module
builder.Services.AddBizcoreModule<OrderModule>(builder);

// ── 2. App Pipeline ───────────────────────────────────────────────────────────
var app = builder.Build();

app.MapDefaultEndpoints("BizCore Order API v1");

// Database Initialization
try
{
    await app.Services.MigrateDatabaseAsync<AppDbContext>();
}
catch (Exception ex) { Log.Error(ex, "Error occurred during Order database initialization."); throw; }

app.Run();

public partial class Program { }
