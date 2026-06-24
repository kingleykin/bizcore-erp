using Bizcore.BuildingBlocks.Infrastructure;
using Customer.API;
using Customer.API.Infrastructure.Data;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── 1. Host & Service Registrations (Standardized) ──────────────────────────
builder.AddServiceDefaults("Customer.API");

builder.Services.AddBizcoreAuth(builder.Configuration);
builder.Services.AddBizcoreVersioning();
builder.Services.AddBizcoreSwagger("BizCore Customer API", "External customer integration service.");

// Load Service Specific Module
builder.Services.AddBizcoreModule<CustomerModule>(builder);

// ── 2. App Pipeline ───────────────────────────────────────────────────────────
var app = builder.Build();

app.MapDefaultEndpoints("BizCore Customer API v1");

// SignalR Hub Mapping
// app.MapHub<Customer.API.Application.Hubs.CustomerHub>("/hubs/customer");

// Database Initialization
await app.Services.MigrateDatabaseAsync<CustomerDbContext>();

try
{
    await app.Services.MigrateDatabaseAsync<CustomerDbContext>();
    using var scope = app.Services.CreateScope();
    await DbSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<CustomerDbContext>(), scope.ServiceProvider.GetRequiredService<ILogger<Program>>());
}
catch (Exception ex) { Log.Error(ex, "Error occurred during Customer database initialization."); throw; }


app.Run();

public partial class Program { }
