using Report.API.Application.Services;
using Report.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using Asp.Versioning;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Middlewares;
using MassTransit;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Serilog Configuration + Loki
var lokiUrl = builder.Configuration.GetValue<string>("Loki:Url") ?? "http://loki:3100";
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.GrafanaLoki(lokiUrl)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Report.API")
    .Enrich.WithProperty("Environment", "Development")
    .CreateLogger();

builder.Host.UseSerilog();

// JWT Authentication Configuration
var secretKey = "BizcoreERPSecretKeyMustBeVeryLongAndSecure!!!";
var key = System.Text.Encoding.ASCII.GetBytes(secretKey);

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.FromMinutes(5) // Allow small time differences between containers
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Report.View", policy => policy.RequireClaim("permission", Bizcore.BuildingBlocks.Permissions.Report.View));
});

builder.Services.AddHealthChecks();
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

// MassTransit Configuration
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<Report.API.Application.Consumers.InvoiceCreatedConsumer>();
    x.AddConsumer<Report.API.Application.Consumers.PaymentCompletedConsumer>();

    x.AddEntityFrameworkOutbox<AppDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetValue<string>("RabbitMQ:Host"), "/", h =>
        {
            h.Username(builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest");
            h.Password(builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest");
        });

        cfg.ReceiveEndpoint("report-invoice-created", e =>
        {
            e.ConfigureConsumer<Report.API.Application.Consumers.InvoiceCreatedConsumer>(context);
        });

        cfg.ReceiveEndpoint("report-payment-completed", e =>
        {
            e.ConfigureConsumer<Report.API.Application.Consumers.PaymentCompletedConsumer>(context);
        });
    });
});

builder.Services.AddScoped<IReportService, ReportService>();

// Prometheus
builder.Services.AddSingleton<ICollectorRegistry>(Metrics.DefaultRegistry);

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

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

// Database Initialization
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();

    // Seed Invoices for Dashboard
    if (!context.Invoices.Any())
    {
        context.Invoices.AddRange(
            new Report.API.Domain.Entities.Invoice { Id = Guid.Parse("f1d2c3b4-a5e6-4d7f-8e9a-0b1c2d3e4f5a"), CustomerName = "Công ty Công nghệ ABC", Amount = 1500, Status = InvoiceStatus.Pending, CreatedAt = DateTime.UtcNow.AddDays(-5) },
            new Report.API.Domain.Entities.Invoice { Id = Guid.Parse("a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d"), CustomerName = "Tập đoàn Kingley", Amount = 3200, Status = InvoiceStatus.Pending, CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new Report.API.Domain.Entities.Invoice { Id = Guid.Parse("9e8d7c6b-5a4b-3c2d-1e0f-9a8b7c6d5e4f"), CustomerName = "Cửa hàng Bán lẻ XYZ", Amount = 450, Status = InvoiceStatus.Pending, CreatedAt = DateTime.UtcNow.AddDays(-1) }
        );
        context.SaveChanges();
    }
}

app.Run();
