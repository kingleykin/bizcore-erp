using Invoice.API.Application.Services;
using Invoice.API.Application.Clients;
using Invoice.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Invoice.API.Application.Consumers;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using FluentValidation.AspNetCore;
using FluentValidation;
using Bizcore.BuildingBlocks.Middlewares;
using Bizcore.BuildingBlocks.MassTransit;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Behaviors;
using Bizcore.BuildingBlocks.Authorization;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Prometheus;
using StackExchange.Redis;
using Bizcore.BuildingBlocks.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Serilog Configuration + Loki
var lokiUrl = builder.Configuration.GetValue<string>("Loki:Url") ?? "http://loki:3100";
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Invoice.API")
    .Enrich.WithProperty("Environment", "Development")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
    .WriteTo.GrafanaLoki(lokiUrl,
        labels: new[]
        {
            new LokiLabel { Key = "service", Value = "invoice-api" },
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
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "bizcore-identity",
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

// Dynamic Authorization — policy tự động tạo runtime theo permission name
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

// Add services to the container.
builder.Services.AddControllers(options =>
{
    options.Filters.Add<Invoice.API.Filters.HttpExceptionFilter>();
});

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Invoice.API.DTOs.CreateInvoiceRequestValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

DatabaseExtensions.PreCreateDatabase(builder.Configuration.GetConnectionString("DefaultConnection")!);

// Register UnitOfWork for transaction management
builder.Services.AddScoped<IUnitOfWork, InvoiceUnitOfWork>();

// Register MediatR with Transaction Pipeline
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    
    // Add pipeline behaviors (order matters: Logging -> Validation -> Transaction)
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
});

// MassTransit Configuration
builder.Services.AddMassTransit(x =>
{
    // Saga orchestrator command consumer
    x.AddConsumer<ValidateInvoiceCommandConsumer>();
    x.AddConsumer<PaymentCompletedConsumer>();

    // Legacy Request-Reply consumer (giữ lại tạm để không break existing tests)
    x.AddConsumer<ApplyPaymentToInvoiceConsumer>();

    x.AddEntityFrameworkOutbox<AppDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.UseCorrelationId(context);

        // Message retry policy
        cfg.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30)
        ));

        cfg.Host(builder.Configuration.GetValue<string>("RabbitMQ:Host"), "/", h =>
        {
            h.Username(builder.Configuration.GetValue<string>("RabbitMQ:Username")?? "guest");
            h.Password(builder.Configuration.GetValue<string>("RabbitMQ:Password")?? "guest");
        });

        // Saga orchestrator command endpoint
        cfg.ReceiveEndpoint("invoice-validate", e =>
        {
            e.Durable = true;
            e.AutoDelete = false;
            e.SetQueueArgument("x-dead-letter-exchange", $"{e.InputAddress.AbsolutePath}_error");
            e.SetQueueArgument("x-message-ttl", (int)TimeSpan.FromDays(7).TotalMilliseconds);
            
            e.ConfigureConsumer<ValidateInvoiceCommandConsumer>(context);
        });

        // Legacy Request-Reply endpoint (giữ lại tạm)
        cfg.ReceiveEndpoint("invoice-apply-payment", e =>
        {
            e.Durable = true;
            e.ConfigureConsumer<ApplyPaymentToInvoiceConsumer>(context);
        });

        // Luồng Saga - lắng nghe kết quả thanh toán cuối cùng
        cfg.ReceiveEndpoint("invoice-payment-completed", e =>
        {
            e.Durable = true;
            e.ConfigureConsumer<PaymentCompletedConsumer>(context);
        });
    });
});

// Dependency Injection
builder.Services.AddScoped<IInvoiceService, InvoiceService>();

// AuditService HTTP Client
builder.Services.AddHttpClient<IAuditServiceClient, AuditServiceClient>(client =>
{
    var auditUrl = builder.Configuration.GetValue<string>("AuditService:BaseUrl")
        ?? "http://audit-api:8080";
    client.BaseAddress = new Uri(auditUrl);
    client.Timeout     = TimeSpan.FromSeconds(10);
});

// Prometheus
builder.Services.AddSingleton<ICollectorRegistry>(Metrics.DefaultRegistry);

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<CorrelationIdPropagationMiddleware>();

app.UseSerilogRequestLogging();

// Prometheus Metrics Middleware
app.UseHttpMetrics();

app.MapHealthChecks("/health");
app.MapMetrics();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// 10. Database Initialization & Seeding
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
