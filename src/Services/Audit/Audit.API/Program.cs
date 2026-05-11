using Asp.Versioning;
using Audit.API.Application.Consumers;
using Audit.API.Application.Jobs;
using Audit.API.Application.Services;
using Audit.API.Infrastructure.Data;
using Audit.API.Infrastructure.Services;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Middlewares;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Authorization;
using Bizcore.BuildingBlocks.Authorization.Consumers;
using Microsoft.AspNetCore.Authorization;
using Hangfire;
using Hangfire.SqlServer;
using MassTransit;
using Bizcore.BuildingBlocks.MassTransit;
using Bizcore.BuildingBlocks.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using System.Text;
using StackExchange.Redis;
using Bizcore.BuildingBlocks.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ── 1. Serilog + Loki ────────────────────────────────────────────────────────
var lokiUrl = builder.Configuration.GetValue<string>("Loki:Url") ?? "http://loki:3100";
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Audit.API")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
    .WriteTo.GrafanaLoki(lokiUrl,
        labels: new[]
        {
            new LokiLabel { Key = "service", Value = "audit-api" },
            new LokiLabel { Key = "environment", Value = builder.Environment.EnvironmentName }
        },
        propertiesAsLabels: new[] { "CorrelationId" })
    .CreateLogger();

builder.Host.UseSerilog();

// ── 2. Database ───────────────────────────────────────────────────────────────
var connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;

// Đảm bảo DB tồn tại trước khi Hangfire khởi tạo (qua master)
DatabaseExtensions.PreCreateDatabase(connStr);

builder.Services.AddDbContext<AuditDbContext>(options => options.UseSqlServer(connStr));

// ── 3. JWT Authentication ────────────────────────────────────────────────────
var secretKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
var key = Encoding.ASCII.GetBytes(secretKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(key),
            ValidateIssuer           = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"] ?? "bizcore-admin",
            ValidateAudience         = true,
            ValidAudience            = builder.Configuration["Jwt:Audience"] ?? "bizcore-erp",
            ClockSkew                = TimeSpan.Zero
        };
    });

// ── 4. Dynamic Authorization ────────────────────────────────────────────────
// Redis Caching cho Permissions
var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "redis:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(redisConnection));
builder.Services.AddScoped<IPermissionCache, RedisPermissionCache>();

builder.Services.AddSingleton<IAuthorizationPolicyProvider, DynamicAuthorizationPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

// ── 5. MassTransit — Consumer only (no Outbox on Audit side) ─────────────────
builder.Services.AddMassTransit(x =>
{
    // Automated registration
    x.AddConsumers(typeof(Program).Assembly);
    x.AddConsumers(typeof(RolePermissionsChangedConsumer).Assembly);

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.ConfigureBusinessBus(context);

        cfg.Host(builder.Configuration.GetValue<string>("RabbitMQ:Host"), "/", h =>
        {
            h.Username(builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest");
            h.Password(builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest");
        });

        // Audit Events Service Queue
        cfg.ReceiveEndpoint(QueueNames.AuditService, e =>
        {
            e.ApplyBusinessEndpointSettings();
            e.ConfigureConsumer<AuditEventConsumer>(context);
        });

        // Permission Updates (Shared among all services)
        cfg.ReceiveEndpoint("audit-permission-updates", e =>
        {
            e.ApplyBusinessEndpointSettings();
            e.ConfigureConsumer<RolePermissionsChangedConsumer>(context);
        });

        cfg.ConfigureEndpoints(context);
    });
});

// ── 6. Hangfire ───────────────────────────────────────────────────────────────
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connStr, new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout       = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout   = TimeSpan.FromMinutes(5),
        QueuePollInterval            = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks           = true
    }));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 2;
    options.Queues      = new[] { "default" };
});

// ── 7. MVC + API Versioning ───────────────────────────────────────────────────
builder.Services.AddControllers();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion                  = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions                  = true;
    options.ApiVersionReader                   = new UrlSegmentApiVersionReader();
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat       = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// ── 8. Swagger ────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "BizCore Audit API",
        Version     = "v1",
        Description = "Centralized, append-only audit trail with hash-chain tamper detection."
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization. Enter 'Bearer {token}'",
        Name        = "Authorization",
        In          = ParameterLocation.Header,
        Type        = SecuritySchemeType.ApiKey,
        Scheme      = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── 9. Health Checks ─────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddSqlServer(connStr, name: "audit-db", tags: new[] { "db", "sql" });

// ── 10. Application Services ──────────────────────────────────────────────────
builder.Services.AddScoped<HashChainService>();
builder.Services.AddScoped<IUnitOfWork, AuditUnitOfWork>();
builder.Services.AddScoped<IAuditQueryService, AuditQueryService>();
builder.Services.AddScoped<RetentionCleanupJob>();
builder.Services.AddScoped<IntegrityVerificationJob>();
builder.Services.AddHttpContextAccessor();

// ── App Pipeline ──────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<CorrelationIdPropagationMiddleware>();

app.UseSerilogRequestLogging();
app.UseHttpMetrics();

app.MapHealthChecks("/health");
app.MapMetrics();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "BizCore Audit API v1");
    c.RoutePrefix = "swagger";
});

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter() }
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── Database Initialization & Seeding ────────────────────────────────────────
try
{
    await app.Services.MigrateDatabaseAsync<AuditDbContext>();
    
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    var seedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DbSeeder.SeedAsync(context, seedLogger);
    
    Log.Information("AuditDb initialized and migrated successfully.");
}
catch (Exception ex)
{
    Log.Error(ex, "Error occurred during AuditDb initialization/migration.");
    throw;
}

// ── Hangfire Recurring Jobs ───────────────────────────────────────────────────
RecurringJob.AddOrUpdate<RetentionCleanupJob>(
    "audit-retention-cleanup",
    job => job.ExecuteAsync(CancellationToken.None),
    Cron.Daily(2, 0)); // 02:00 UTC daily

RecurringJob.AddOrUpdate<IntegrityVerificationJob>(
    "audit-integrity-check",
    job => job.ExecuteAsync(CancellationToken.None),
    Cron.Weekly(DayOfWeek.Sunday, 3, 0)); // Sunday 03:00 UTC weekly

app.Run();
