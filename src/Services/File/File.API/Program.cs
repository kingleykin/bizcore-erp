using Bizcore.BuildingBlocks.Infrastructure;
using File.API;

var builder = WebApplication.CreateBuilder(args);

// ── 1. Host & Service Registrations (Standardized) ──────────────────────────
builder.AddServiceDefaults("File.API");

builder.Services.AddBizcoreAuth(builder.Configuration);
builder.Services.AddBizcoreVersioning();
builder.Services.AddBizcoreSwagger("BizCore File API", "File storage and management service.");

// Load Service Specific Module
builder.Services.AddBizcoreModule<FileModule>(builder);

// ── 2. App Pipeline ───────────────────────────────────────────────────────────
var app = builder.Build();

app.MapDefaultEndpoints("BizCore File API v1");

app.Run();

public partial class Program { }
