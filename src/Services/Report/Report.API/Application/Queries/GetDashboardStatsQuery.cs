using Bizcore.BuildingBlocks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Report.API.Application.DTOs;
using Report.API.Infrastructure.Data;

namespace Report.API.Application.Queries;

public record GetDashboardStatsQuery : IRequest<DashboardStatsDto>;

public class GetDashboardStatsHandler : IRequestHandler<GetDashboardStatsQuery, DashboardStatsDto>
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "DashboardStats";

    public GetDashboardStatsHandler(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<DashboardStatsDto> Handle(GetDashboardStatsQuery request, CancellationToken cancellationToken)
    {
        if (!_cache.TryGetValue(CacheKey, out DashboardStatsDto? summary))
        {
            var totalInvoices = await _context.Invoices.CountAsync(cancellationToken);
            var totalAmount = await _context.Invoices.SumAsync(x => x.Amount, cancellationToken);
            var paidInvoices = await _context.Invoices.CountAsync(x => x.Status == InvoiceStatus.Paid, cancellationToken);

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
