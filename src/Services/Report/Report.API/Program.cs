using Report.API.Application.Services;
using Report.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using Asp.Versioning;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.MassTransit;
using Bizcore.BuildingBlocks.Middlewares;
using MassTransit;
using Prometheus;
using StackExchange.Redis;
using Bizcore.BuildingBlocks.Authorization;
using Bizcore.BuildingBlocks.Authorization.Consumers;
using Bizcore.BuildingBlocks.Messaging;
using MassTransit.QuartzIntegration;
using Quartz;
using Bizcore.BuildingBlocks.Infrastructure;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Serilog Configuration + Loki
var lokiUrl = builder.Configuration.GetValue<string>("Loki:Url") ?? "http://loki:3100";
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Report.API")
    .Enrich.WithProperty("Environment", "Development")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
    .WriteTo.GrafanaLoki(lokiUrl,
        labels: new[]
        {
            new LokiLabel { Key = "service", Value = "report-api" },
            new LokiLabel { Key = "environment", Value = "Development" }
        },
        propertiesAsLabels: new[] { "CorrelationId" })
    .CreateLogger();

builder.Host.UseSerilog();

// JWT Authentication Configuration
var secretKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
var key = System.Text.Encoding.ASCII.GetBytes(secretKey);

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "bizcore-admin",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "bizcore-erp",
            ClockSkew = TimeSpan.FromMinutes(5) // Allow small time differences between containers
        };
    });

// Redis Caching cho Permissions
var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "redis:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(redisConnection));
builder.Services.AddScoped<IPermissionCache, RedisPermissionCache>();

// Dynamic Authorization
builder.Services.AddSingleton<IAuthorizationPolicyProvider, DynamicAuthorizationPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

DatabaseExtensions.PreCreateDatabase(builder.Configuration.GetConnectionString("DefaultConnection")!);

// MassTransit Configuration
builder.Services.AddMassTransit(x =>
{
    // Automated registration
    x.AddConsumers(typeof(Program).Assembly);
    x.AddConsumers(typeof(RolePermissionsChangedConsumer).Assembly);

    x.AddQuartz();
    x.AddQuartzConsumers();

    // Outbox & Inbox Configuration
    x.AddBusinessOutbox<AppDbContext>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.ConfigureBusinessBus(context);

        cfg.Host(builder.Configuration.GetValue<string>("RabbitMQ:Host"), "/", h =>
        {
            h.Username(builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest");
            h.Password(builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest");
        });

        // Report Service Queue (Consolidated)
        cfg.ReceiveEndpoint(QueueNames.ReportService, e =>
        {
            e.ApplyBusinessEndpointSettings();
            
            // Register consumers
            e.ConfigureConsumer<Report.API.Application.Consumers.InvoiceCreatedConsumer>(context);
            e.ConfigureConsumer<Report.API.Application.Consumers.PaymentCompletedConsumer>(context);

            // Enable Inbox (Deduplication)
            e.UseEntityFrameworkOutbox<AppDbContext>(context);
        });

        // Permission Updates (Shared)
        cfg.ReceiveEndpoint("report-permission-updates", e =>
        {
            e.ApplyBusinessEndpointSettings();
            e.ConfigureConsumer<RolePermissionsChangedConsumer>(context);
        });

        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddScoped<IReportService, ReportService>();

// Prometheus
builder.Services.AddSingleton<ICollectorRegistry>(Metrics.DefaultRegistry);

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<CorrelationIdPropagationMiddleware>();

app.MapHealthChecks("/health");

app.UseSerilogRequestLogging();

// Prometheus Metrics Middleware
app.UseHttpMetrics();
app.MapMetrics();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection(); // Removed for Docker internal HTTP traffic
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Database Initialization & Seeding
try
{
    await app.Services.MigrateDatabaseAsync<AppDbContext>();
    
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DbSeeder.SeedAsync(context, seedLogger);
}
catch (Exception ex)
{
    Log.Error(ex, "Error occurred during database initialization/seeding.");
    throw;
}

app.Run();
