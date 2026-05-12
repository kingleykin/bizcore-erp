using Bizcore.BuildingBlocks.Infrastructure;
using Payment.API;
using Payment.API.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// ── 1. Host Extensions ────────────────────────────────────────────────────────
builder.Host.AddBizcoreLogging("Payment.API");

// ── 2. Service Registrations (Centralized + Module) ──────────────────────────
builder.Services.AddBizcoreTelemetry("Payment.API");
builder.Services.AddBizcoreInfrastructure();
builder.Services.AddBizcoreAuth(builder.Configuration);
builder.Services.AddBizcoreVersioning();
builder.Services.AddBizcoreSwagger("BizCore Payment API", "External payment integration service.");

// Load Service Specific Module
builder.Services.AddBizcoreModule<PaymentModule>(builder);

// ── 3. App Pipeline ───────────────────────────────────────────────────────────
var app = builder.Build();

app.UseBizcorePipeline("BizCore Payment API v1");

// Database Initialization
await app.Services.MigrateDatabaseAsync<AppDbContext>();

app.Run();

public partial class Program { }
