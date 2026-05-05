using Report.API.DTOs;
using Report.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Report.API.Application.Services
{
    public interface IReportService
    {
        Task<DashboardStatsDto> GetDashboardStatsAsync();
    }

    public class ReportService : IReportService
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private const string CacheKey = "DashboardStats";

        public ReportService(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<DashboardStatsDto> GetDashboardStatsAsync()
        {
            if (!_cache.TryGetValue(CacheKey, out DashboardStatsDto? summary))
            {
                var totalInvoices = await _context.Invoices.CountAsync();
                var totalAmount = await _context.Invoices.SumAsync(x => x.Amount);
                var paidInvoices = await _context.Invoices.CountAsync(x => x.Status == InvoiceStatus.Paid);

                summary = new DashboardStatsDto
                {
                    TotalInvoices = totalInvoices,
                    TotalRevenue = totalAmount,
                    PaidInvoices = paidInvoices,
                    PendingInvoices = totalInvoices - paidInvoices
                };

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

                _cache.Set(CacheKey, summary, cacheOptions);
            }

            return summary!;
        }
    }
}
