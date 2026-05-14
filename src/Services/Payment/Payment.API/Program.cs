using Bizcore.BuildingBlocks.Infrastructure;
using Payment.API;
using Payment.API.Infrastructure.Data;

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

app.Run();

public partial class Program { }
