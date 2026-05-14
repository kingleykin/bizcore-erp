using Bizcore.BuildingBlocks.Infrastructure;
using Orchestration.API;
using Orchestration.API.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// ── 1. Host & Service Registrations (Standardized) ──────────────────────────
builder.AddServiceDefaults("Orchestration.API");

builder.Services.AddBizcoreAuth(builder.Configuration);
builder.Services.AddBizcoreVersioning();
builder.Services.AddBizcoreSwagger("BizCore Orchestration API", "Process Manager & Saga Orchestrator");

// Load Service Specific Module
builder.Services.AddBizcoreModule<OrchestrationModule>(builder);

// ── 2. App Pipeline ───────────────────────────────────────────────────────────
var app = builder.Build();

app.MapDefaultEndpoints("BizCore Orchestration API v1");

// Database Initialization
await app.Services.MigrateDatabaseAsync<AppDbContext>();

app.Run();
