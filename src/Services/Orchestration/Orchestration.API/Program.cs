using Bizcore.BuildingBlocks.Infrastructure;
using Orchestration.API;
using Orchestration.API.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// ── 1. Host Extensions ────────────────────────────────────────────────────────
builder.Host.AddBizcoreLogging("Orchestration.API");

// ── 2. Service Registrations (Centralized + Module) ──────────────────────────
builder.Services.AddBizcoreTelemetry("Orchestration.API");
builder.Services.AddBizcoreInfrastructure();
builder.Services.AddBizcoreAuth(builder.Configuration);
builder.Services.AddBizcoreVersioning();
builder.Services.AddBizcoreSwagger("BizCore Orchestration API", "Process Manager & Saga Orchestrator");

// Load Service Specific Module
builder.Services.AddBizcoreModule<OrchestrationModule>(builder);

// ── 3. App Pipeline ───────────────────────────────────────────────────────────
var app = builder.Build();

app.UseBizcorePipeline("BizCore Orchestration API v1");

// Database Initialization
await app.Services.MigrateDatabaseAsync<AppDbContext>();

app.Run();
