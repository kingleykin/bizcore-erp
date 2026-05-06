using Payment.API.Application.Services;
using Payment.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Payment.API.Application.Consumers;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using Asp.Versioning;
using Bizcore.BuildingBlocks.Middlewares;
using Bizcore.BuildingBlocks;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Serilog Configuration + Loki
var lokiUrl = builder.Configuration.GetValue<string>("Loki:Url") ?? "http://loki:3100";
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.GrafanaLoki(lokiUrl)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Payment.API")
    .Enrich.WithProperty("Environment", "Development")
    .CreateLogger();

builder.Host.UseSerilog();

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
    x.AddConsumer<PaymentCompensationRequestedConsumer>();
    x.AddConsumer<InvoiceCreatedConsumer>();

    x.AddEntityFrameworkOutbox<AppDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetValue<string>("RabbitMQ:Host"), "/", h =>
        {
            h.Username(builder.Configuration.GetValue<string>("RabbitMQ:Username")?? "guest");
            h.Password(builder.Configuration.GetValue<string>("RabbitMQ:Password")?? "guest");
        });

        cfg.ReceiveEndpoint("payment-compensation-requested", e =>
        {
            e.ConfigureConsumer<PaymentCompensationRequestedConsumer>(context);
        });

        cfg.ReceiveEndpoint("payment-invoice-created", e =>
        {
            e.ConfigureConsumer<InvoiceCreatedConsumer>(context);
        });
    });
});

builder.Services.AddScoped<IPaymentService, PaymentService>();

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

app.UseHttpsRedirection();
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
