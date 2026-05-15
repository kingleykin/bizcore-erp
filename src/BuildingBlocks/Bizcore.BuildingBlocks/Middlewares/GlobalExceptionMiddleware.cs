using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
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
            var statusCode = StatusCodes.Status500InternalServerError;
            var message = "An unexpected error occurred.";
            var errorCode = ErrorCodes.Common.InternalError;
            object? parameters = null;

            if (exception is DomainException domainEx)
            {
                statusCode = StatusCodes.Status400BadRequest;
                message = domainEx.Message;
                errorCode = domainEx.Code;
                parameters = domainEx.Parameters;
            }
            else if (exception is UnauthorizedException unauthorizedEx)
            {
                statusCode = StatusCodes.Status401Unauthorized;
                message = unauthorizedEx.Message;
                errorCode = ErrorCodes.Common.Unauthorized;
            }
            else if (exception is Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                statusCode = StatusCodes.Status409Conflict;
                message = "Dữ liệu đã bị thay đổi bởi người dùng khác. Vui lòng làm mới trang và thử lại.";
                errorCode = ErrorCodes.Common.ConcurrencyError;
            }
            else if (exception is NotFoundException notFoundEx)
            {
                statusCode = StatusCodes.Status404NotFound;
                message = notFoundEx.Message;
                errorCode = notFoundEx.Code;
                parameters = notFoundEx.Parameters;
            }

            _logger.LogError(exception, "Error captured by middleware: {ErrorCode} - {Message}", errorCode, exception.Message);

            var activity = Activity.Current;
            var traceId = activity?.TraceId.ToString() ?? context.TraceIdentifier;
            var traceParent = activity?.Id ?? context.TraceIdentifier;
            var correlationId = context.Items["X-Correlation-ID"]?.ToString();

            var response = new
            {
                Code = errorCode,
                Message = message,
                Params = parameters,
                TraceId = traceId,
                TraceParent = traceParent,
                CorrelationId = correlationId,
                Timestamp = DateTime.UtcNow
            };

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            }));
        }
    }
}
