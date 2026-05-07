using Asp.Versioning;
using Audit.API.Application.Consumers;
using Audit.API.Application.Jobs;
using Audit.API.Application.Services;
using Audit.API.Infrastructure.Data;
using Audit.API.Infrastructure.Services;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Middlewares;
using Hangfire;
using Hangfire.SqlServer;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using System.Text;

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
            ValidIssuer              = builder.Configuration["Jwt:Issuer"] ?? "bizcore-identity",
            ValidateAudience         = true,
            ValidAudience            = builder.Configuration["Jwt:Audience"] ?? "bizcore-erp",
            ClockSkew                = TimeSpan.Zero
        };
    });

// ── 4. Authorization Policies ────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Audit.View",   p => p.RequireClaim("permission", Permissions.Audit.View));
    options.AddPolicy("Audit.Export", p => p.RequireClaim("permission", Permissions.Audit.Export));
});

// ── 5. MassTransit — Consumer only (no Outbox on Audit side) ─────────────────
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<AuditEventConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetValue<string>("RabbitMQ:Host"), "/", h =>
        {
            h.Username(builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest");
            h.Password(builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest");
        });

        cfg.ReceiveEndpoint("audit-events", e =>
        {
            e.Durable     = true;
            e.AutoDelete  = false;

            // Dead Letter Queue — messages that fail all retries go here
            e.SetQueueArgument("x-dead-letter-exchange", "audit-events_error");
            e.SetQueueArgument("x-message-ttl", (int)TimeSpan.FromDays(7).TotalMilliseconds);

            // Retry policy: 5 attempts with exponential back-off
            e.UseMessageRetry(r => r.Intervals(
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(120)));

            e.ConfigureConsumer<AuditEventConsumer>(context);
        });
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

// ── Database Initialization ───────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await db.Database.EnsureCreatedAsync();
        logger.LogInformation("AuditDb initialized successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "AuditDb initialization failed.");
        throw;
    }
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
