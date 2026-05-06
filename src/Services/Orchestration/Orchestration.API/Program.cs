using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.MassTransit;
using Bizcore.BuildingBlocks.Middlewares;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Orchestration.API.Application.Consumers;
using Orchestration.API.Application.Sagas;
using Orchestration.API.Application.Services;
using Orchestration.API.Domain.Entities;
using Orchestration.API.Infrastructure.Data;
using Prometheus;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using Asp.Versioning;
using System.Text;

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

var secretKey = "BizcoreERPSecretKeyMustBeVeryLongAndSecure!!!";
var key = Encoding.ASCII.GetBytes(secretKey);

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        "Orchestration.View",
        policy => policy.RequireClaim("permission", Permissions.Orchestration.View));
});

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

builder.Services.AddScoped<IProcessOrchestrationService, ProcessOrchestrationService>();

builder.Services.AddMassTransit(x =>
{
    // Legacy event observers (giữ lại cho backward compatibility)
    x.AddConsumer<InvoiceCreatedOrchestrationConsumer>();
    x.AddConsumer<PaymentCompletedOrchestrationConsumer>();
    x.AddConsumer<PaymentCompensationRequestedOrchestrationConsumer>();

    // Saga orchestrator
    x.AddSagaStateMachine<PaymentSaga, PaymentSagaState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Pessimistic;
            r.AddDbContext<DbContext, AppDbContext>((provider, builder) =>
            {
                builder.UseSqlServer(provider.GetRequiredService<IConfiguration>()
                    .GetConnectionString("DefaultConnection"));
            });
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
            h.Username(builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest");
            h.Password(builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest");
        });

        // Saga endpoint
        cfg.ReceiveEndpoint("orchestration-payment-saga", e =>
        {
            e.Durable = true;
            e.AutoDelete = false;
            e.SetQueueArgument("x-dead-letter-exchange", $"{e.InputAddress.AbsolutePath}_error");
            e.SetQueueArgument("x-message-ttl", (int)TimeSpan.FromDays(7).TotalMilliseconds);
            
            e.ConfigureSaga<PaymentSagaState>(context);
        });

        // Legacy event observer endpoints
        cfg.ReceiveEndpoint("orchestration-invoice-created", e =>
        {
            e.Durable = true;
            e.ConfigureConsumer<InvoiceCreatedOrchestrationConsumer>(context);
        });

        cfg.ReceiveEndpoint("orchestration-payment-completed", e =>
        {
            e.Durable = true;
            e.ConfigureConsumer<PaymentCompletedOrchestrationConsumer>(context);
        });

        cfg.ReceiveEndpoint("orchestration-payment-compensation-requested", e =>
        {
            e.Durable = true;
            e.ConfigureConsumer<PaymentCompensationRequestedOrchestrationConsumer>(context);
        });
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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.Run();
