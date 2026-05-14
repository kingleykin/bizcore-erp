using Bizcore.BuildingBlocks.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Serilog.Context;
using System.Security.Claims;

namespace Bizcore.BuildingBlocks.Middlewares
{
    public class TenantMiddleware
    {
        private readonly RequestDelegate _next;
        private const string TenantHeader = "X-Tenant-ID";
        private const string TenantClaim = "tenant_id";

        public TenantMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
        {
            string? tenantId = context.Request.Headers[TenantHeader].FirstOrDefault();

            if (string.IsNullOrEmpty(tenantId))
            {
                // Fallback to JWT claim if available
                tenantId = context.User.FindFirstValue(TenantClaim);
            }

            if (!string.IsNullOrEmpty(tenantId))
            {
                if (tenantContext is TenantContext tc)
                {
                    tc.TenantId = tenantId;
                }

                // Push to Serilog LogContext for all logs in this request
                using (LogContext.PushProperty("TenantId", tenantId))
                {
                    await _next(context);
                }
            }
            else
            {
                await _next(context);
            }
        }
    }
}
