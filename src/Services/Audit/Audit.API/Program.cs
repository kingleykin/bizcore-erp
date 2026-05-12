using Audit.API;
using Audit.API.Application.Jobs;
using Audit.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Infrastructure;
using Hangfire;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── 1. Host Extensions ────────────────────────────────────────────────────────
builder.Host.AddBizcoreLogging("Audit.API");

// ── 2. Service Registrations (Centralized + Module) ──────────────────────────
builder.Services.AddBizcoreTelemetry("Audit.API");
builder.Services.AddBizcoreInfrastructure();
builder.Services.AddBizcoreAuth(builder.Configuration);
builder.Services.AddBizcoreVersioning();
builder.Services.AddBizcoreSwagger("BizCore Audit API", "Centralized audit trail with hash-chain.");

// Load Service Specific Module
builder.Services.AddBizcoreModule<AuditModule>(builder);

// ── 3. App Pipeline ───────────────────────────────────────────────────────────
var app = builder.Build();

app.UseBizcorePipeline("BizCore Audit API v1");

// gRPC Mapping
app.MapGrpcService<Audit.API.Application.Grpc.AuditGrpcService>();
if (app.Environment.IsDevelopment()) app.MapGrpcReflectionService();

// Database Initialization
try
{
    await app.Services.MigrateDatabaseAsync<AuditDbContext>();
    using var scope = app.Services.CreateScope();
    await DbSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AuditDbContext>(), scope.ServiceProvider.GetRequiredService<ILogger<Program>>());
}
catch (Exception ex) { Log.Error(ex, "Error occurred during AuditDb initialization."); throw; }

// Hangfire Recurring Jobs
RecurringJob.AddOrUpdate<RetentionCleanupJob>("audit-retention-cleanup", j => j.ExecuteAsync(CancellationToken.None), Cron.Daily(2, 0));
RecurringJob.AddOrUpdate<IntegrityVerificationJob>("audit-integrity-check", j => j.ExecuteAsync(CancellationToken.None), Cron.Weekly(DayOfWeek.Sunday, 3, 0));

app.Run();
