using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Infrastructure;
using Bizcore.BuildingBlocks.Authorization;
using Bizcore.BuildingBlocks.Authorization.Consumers;
using Bizcore.BuildingBlocks.MassTransit;
using Bizcore.BuildingBlocks.Middlewares;
using Bizcore.BuildingBlocks.Messaging;
using Microsoft.AspNetCore.Authorization;
using MassTransit;
using MassTransit.QuartzIntegration;
using Quartz;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Orchestration.API.Application.Consumers;
using Bizcore.BuildingBlocks.Contracts;
using Orchestration.API.Application.Sagas;
using Orchestration.API.Application.Services;
using Orchestration.API.Domain.Entities;
using Orchestration.API.Infrastructure.Data;
using Prometheus;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using Asp.Versioning;
using System.Text;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var lokiUrl = builder.Configuration.GetValue<string>("Loki:Url") ?? "http://loki:3100";
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Orchestration.API")
    .Enrich.WithProperty("Environment", "Development")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
    .WriteTo.GrafanaLoki(lokiUrl,
        labels: new[]
        {
            new LokiLabel { Key = "service", Value = "orchestration-api" },
            new LokiLabel { Key = "environment", Value = "Development" }
        },
        propertiesAsLabels: new[] { "CorrelationId" })
    .CreateLogger();

builder.Host.UseSerilog();

var secretKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
var key = Encoding.ASCII.GetBytes(secretKey);

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "bizcore-admin",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "bizcore-erp",
            ClockSkew = TimeSpan.FromMinutes(5)
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

builder.Services.AddScoped<IProcessOrchestrationService, ProcessOrchestrationService>();

builder.Services.AddMassTransit(x =>
{
    // Register shared consumers from BuildingBlocks
    x.AddConsumers(typeof(RolePermissionsChangedConsumer).Assembly);
    
    x.AddQuartz();
    x.AddQuartzConsumers();

    // Outbox & Inbox Configuration
    x.AddBusinessOutbox<AppDbContext>();

    // Command Mappings (Sender Topology)
    // We send commands to Service-Level Exchanges, not directly to Queues
    x.MapBusinessCommand<IValidateInvoiceCommand>(QueueNames.InvoiceService);
    x.MapBusinessCommand<IConfirmPaymentCommand>(QueueNames.PaymentService);
    x.MapBusinessCommand<IRejectPaymentCommand>(QueueNames.PaymentService);

    // Legacy event observers (giữ lại cho backward compatibility)
    x.AddConsumer<InvoiceCreatedOrchestrationConsumer>();
    x.AddConsumer<PaymentCompletedOrchestrationConsumer>();
    x.AddConsumer<PaymentCompensationRequestedOrchestrationConsumer>();
    x.AddConsumer<RolePermissionsChangedConsumer>();


    // Saga orchestrator
    x.AddSagaStateMachine<PaymentSaga, PaymentSagaState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Pessimistic;
            r.ExistingDbContext<AppDbContext>();
        });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.ConfigureBusinessBus(context);
        
        // Cấu hình Quartz Scheduler thay cho RabbitMQ Plugin
        cfg.UsePublishMessageScheduler();

        // Message retry policy
        cfg.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30)
        ));

        cfg.Host(builder.Configuration.GetValue<string>("RabbitMQ:Host"), "/", h =>
        {
            h.Username(builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest");
            h.Password(builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest");
        });

        // Centralized Orchestration Service Endpoint
        cfg.ReceiveEndpoint(QueueNames.OrchestrationService, e =>
        {
            e.ApplyBusinessEndpointSettings();
            
            // Register Saga
            e.ConfigureSaga<PaymentSagaState>(context);

            // Register all event consumers for this service
            e.ConfigureConsumer<InvoiceCreatedOrchestrationConsumer>(context);
            e.ConfigureConsumer<PaymentCompletedOrchestrationConsumer>(context);
            e.ConfigureConsumer<PaymentCompensationRequestedOrchestrationConsumer>(context);
            e.ConfigureConsumer<RolePermissionsChangedConsumer>(context);

            // Enable Inbox (Deduplication) for this service endpoint
            e.UseEntityFrameworkOutbox<AppDbContext>(context);
        });

        // Đăng ký Saga và Consumers
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddSingleton<ICollectorRegistry>(Metrics.DefaultRegistry);

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<CorrelationIdPropagationMiddleware>();

app.MapHealthChecks("/health");

app.UseSerilogRequestLogging();
app.UseHttpMetrics();
app.MapMetrics();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Database Initialization
await app.Services.MigrateDatabaseAsync<AppDbContext>();

app.Run();
