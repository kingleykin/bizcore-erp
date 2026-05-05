using Invoice.API.Application.Services;
using Invoice.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Invoice.API.Application.Consumers;
using Serilog;
using FluentValidation.AspNetCore;
using FluentValidation;
using Bizcore.BuildingBlocks.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// Serilog Configuration
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .Enrich.FromLogContext()
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
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Invoice.View", policy => policy.RequireClaim("permission", Bizcore.BuildingBlocks.Permissions.Invoice.View));
    options.AddPolicy("Invoice.Create", policy => policy.RequireClaim("permission", Bizcore.BuildingBlocks.Permissions.Invoice.Create));
    options.AddPolicy("Invoice.Update", policy => policy.RequireClaim("permission", Bizcore.BuildingBlocks.Permissions.Invoice.Update));
});


using Asp.Versioning;

builder.Services.AddHealthChecks();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Add services to the container.
builder.Services.AddControllers(options =>
{
    options.Filters.Add<Invoice.API.Filters.HttpExceptionFilter>();
})
    .AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<Invoice.API.DTOs.CreateInvoiceRequestValidator>());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// MassTransit Configuration
builder.Services.AddMassTransit(x =>
{
    // Register Consumer
    x.AddConsumer<PaymentCompletedConsumer>();

    x.AddEntityFrameworkOutbox<AppDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetValue<string>("RabbitMQ:Host"), "/", h =>
        {
            h.Username(builder.Configuration.GetValue<string>("RabbitMQ:Username"));
            h.Password(builder.Configuration.GetValue<string>("RabbitMQ:Password"));
        });

        // Setup receive endpoint
        cfg.ReceiveEndpoint("invoice-payment-completed", e =>
        {
            e.ConfigureConsumer<PaymentCompletedConsumer>(context);
        });
    });
});

// Dependency Injection
builder.Services.AddScoped<IInvoiceService, InvoiceService>();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseSerilogRequestLogging();

app.MapHealthChecks("/health");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
