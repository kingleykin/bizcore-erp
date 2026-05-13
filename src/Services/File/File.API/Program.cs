using Bizcore.BuildingBlocks.Infrastructure;
using File.API;

var builder = WebApplication.CreateBuilder(args);

// 1. Host Extensions
builder.Host.AddBizcoreLogging("File.API");

// 2. Service Registrations
builder.Services.AddBizcoreTelemetry("File.API");
builder.Services.AddBizcoreInfrastructure();
builder.Services.AddBizcoreAuth(builder.Configuration);
builder.Services.AddBizcoreVersioning();
builder.Services.AddBizcoreSwagger("BizCore File API", "File storage and management service.");

// Load Service Specific Module
builder.Services.AddBizcoreModule<FileModule>(builder);

// 3. App Pipeline
var app = builder.Build();

app.UseBizcorePipeline("BizCore File API v1");

app.Run();

public partial class Program { }
