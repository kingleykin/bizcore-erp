using Payment.API.Application.Services;
using Payment.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Payment.API.Application.Consumers;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using Asp.Versioning;
using Bizcore.BuildingBlocks.Middlewares;
using Bizcore.BuildingBlocks.MassTransit;
using Bizcore.BuildingBlocks;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Serilog Configuration + Loki
var lokiUrl = builder.Configuration.GetValue<string>("Loki:Url") ?? "http://loki:3100";
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Payment.API")
    .Enrich.WithProperty("Environment", "Development")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
    .WriteTo.GrafanaLoki(lokiUrl,
        labels: new[]
        {
            new LokiLabel { Key = "service", Value = "payment-api" },
            new LokiLabel { Key = "environment", Value = "Development" }
        },
        propertiesAsLabels: new[] { "CorrelationId" })
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Payment.View", policy => policy.RequireClaim("permission", Bizcore.BuildingBlocks.Permissions.Payment.View));
    options.AddPolicy("Payment.Create", policy => policy.RequireClaim("permission", Bizcore.BuildingBlocks.Permissions.Payment.Create));
    options.AddPolicy("Payment.Process", policy => policy.RequireClaim("permission", Bizcore.BuildingBlocks.Permissions.Payment.Process));
});

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

// MassTransit Configuration
builder.Services.AddMassTransit(x =>
{
    // Consumers cho Saga orchestrator commands
    x.AddConsumer<ConfirmPaymentConsumer>();
    x.AddConsumer<RejectPaymentConsumer>();

    // Legacy consumers (giữ lại cho backward compatibility)
    x.AddConsumer<PaymentCompensationRequestedConsumer>();
    x.AddConsumer<InvoiceCreatedConsumer>();

    x.AddEntityFrameworkOutbox<AppDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.UseCorrelationId(context);

        // Message retry policy: 3 lần, mỗi lần cách 5 giây
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

        // Saga orchestrator commands
        cfg.ReceiveEndpoint("payment-confirm", e =>
        {
            // Queue durability + dead letter
            e.Durable = true;
            e.AutoDelete = false;
            e.SetQueueArgument("x-dead-letter-exchange", $"{e.InputAddress.AbsolutePath}_error");
            e.SetQueueArgument("x-message-ttl", (int)TimeSpan.FromDays(7).TotalMilliseconds);
            
            e.ConfigureConsumer<ConfirmPaymentConsumer>(context);
        });

        cfg.ReceiveEndpoint("payment-reject", e =>
        {
            e.Durable = true;
            e.AutoDelete = false;
            e.SetQueueArgument("x-dead-letter-exchange", $"{e.InputAddress.AbsolutePath}_error");
            e.SetQueueArgument("x-message-ttl", (int)TimeSpan.FromDays(7).TotalMilliseconds);
            
            e.ConfigureConsumer<RejectPaymentConsumer>(context);
        });

        // Legacy endpoints
        cfg.ReceiveEndpoint("payment-compensation-requested", e =>
        {
            e.Durable = true;
            e.ConfigureConsumer<PaymentCompensationRequestedConsumer>(context);
        });

        cfg.ReceiveEndpoint("payment-invoice-created", e =>
        {
            e.Durable = true;
            e.ConfigureConsumer<InvoiceCreatedConsumer>(context);
        });
    });
});

builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IIdempotencyService, IdempotencyService>();

// Background services
builder.Services.AddHostedService<Payment.API.Application.BackgroundServices.PaymentReconciliationService>();
builder.Services.AddHostedService<Payment.API.Application.BackgroundServices.IdempotencyCleanupService>();

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

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Database Initialization
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();

    // Seed Invoices for local check
    if (!context.Invoices.Any())
    {
        context.Invoices.AddRange(
            new Payment.API.Domain.Entities.Invoice { Id = Guid.Parse("f1d2c3b4-a5e6-4d7f-8e9a-0b1c2d3e4f5a"), Status = InvoiceStatus.Pending },
            new Payment.API.Domain.Entities.Invoice { Id = Guid.Parse("a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d"), Status = InvoiceStatus.Pending },
            new Payment.API.Domain.Entities.Invoice { Id = Guid.Parse("9e8d7c6b-5a4b-3c2d-1e0f-9a8b7c6d5e4f"), Status = InvoiceStatus.Pending }
        );
        context.SaveChanges();
    }
}

app.Run();
