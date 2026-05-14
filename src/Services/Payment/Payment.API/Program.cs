using Bizcore.BuildingBlocks.Infrastructure;
using Payment.API;
using Payment.API.Infrastructure.Data;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── 1. Host & Service Registrations (Standardized) ──────────────────────────
builder.AddServiceDefaults("Payment.API");

builder.Services.AddBizcoreAuth(builder.Configuration);
builder.Services.AddBizcoreVersioning();
builder.Services.AddBizcoreSwagger("BizCore Payment API", "External payment integration service.");

// Load Service Specific Module
builder.Services.AddBizcoreModule<PaymentModule>(builder);

// ── 2. App Pipeline ───────────────────────────────────────────────────────────
var app = builder.Build();

app.MapDefaultEndpoints("BizCore Payment API v1");

// SignalR Hub Mapping
app.MapHub<Payment.API.Application.Hubs.PaymentHub>("/hubs/payment");

// Database Initialization
await app.Services.MigrateDatabaseAsync<AppDbContext>();

try
{
    await app.Services.MigrateDatabaseAsync<AppDbContext>();
    using var scope = app.Services.CreateScope();
    await DbSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), scope.ServiceProvider.GetRequiredService<ILogger<Program>>());
}
catch (Exception ex) { Log.Error(ex, "Error occurred during Invoice database initialization."); throw; }


app.Run();

public partial class Program { }
