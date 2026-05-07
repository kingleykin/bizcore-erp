using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Bizcore.BuildingBlocks.Exceptions;

namespace Bizcore.BuildingBlocks.Middlewares
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var code = StatusCodes.Status500InternalServerError;
            var message = "An unexpected error occurred.";
            var type = "INTERNAL_SERVER_ERROR";

            if (exception is DomainException domainEx)
            {
                code = StatusCodes.Status400BadRequest;
                message = domainEx.Message;
                type = "DOMAIN_ERROR";
            }
            else if (exception is UnauthorizedException unauthorizedEx)
            {
                code = StatusCodes.Status401Unauthorized;
                message = unauthorizedEx.Message;
                type = "UNAUTHORIZED";
            }
            else if (exception is NotFoundException notFoundEx)
            {
                code = StatusCodes.Status404NotFound;
                message = notFoundEx.Message;
                type = "NOT_FOUND";
            }

            _logger.LogError(exception, "Error captured by middleware: {Message}", exception.Message);

            var traceId = context.Items["X-Correlation-ID"]?.ToString() ?? context.TraceIdentifier;

            var response = new
            {
                Code = type,
                Message = message,
                TraceId = traceId,
                Timestamp = DateTime.UtcNow
            };

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = code;

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
