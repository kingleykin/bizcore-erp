using Microsoft.AspNetCore.Http;
using Serilog.Context;
using System.Threading.Tasks;

namespace Bizcore.BuildingBlocks.Middlewares
{
    /// <summary>
    /// Dùng tại API Gateway:
    /// - Sinh ID mới nếu client không gửi lên
    /// - Gắn ID vào Response.Headers để trả về client
    /// - Lưu vào Items và Serilog LogContext để trace log
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;
        private const string CorrelationIdHeader = "X-Correlation-ID";

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = context.Request.Headers[CorrelationIdHeader].ToString();

            if (string.IsNullOrEmpty(correlationId))
            {
                correlationId = Guid.NewGuid().ToString();
            }

            context.Items[CorrelationIdHeader] = correlationId;
            context.Response.Headers[CorrelationIdHeader] = correlationId;

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await _next(context);
            }
        }
    }

    /// <summary>
    /// Dùng tại các downstream services (Invoice, Payment, Report, Orchestration):
    /// - Đọc ID do Gateway inject vào request header
    /// - Lưu vào Items và Serilog LogContext để trace log
    /// - KHÔNG set Response.Headers vì Gateway mới là nơi trả header về client
    /// </summary>
    public class CorrelationIdPropagationMiddleware
    {
        private readonly RequestDelegate _next;
        private const string CorrelationIdHeader = "X-Correlation-ID";

        public CorrelationIdPropagationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = context.Request.Headers[CorrelationIdHeader].ToString();

            if (string.IsNullOrEmpty(correlationId))
            {
                correlationId = Guid.NewGuid().ToString();
            }

            context.Items[CorrelationIdHeader] = correlationId;

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await _next(context);
            }
        }
    }
}
