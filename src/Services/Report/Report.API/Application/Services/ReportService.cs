using Report.API.DTOs;
using Report.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Report.API.Application.Services
{
    public interface IReportService
    {
        Task<DashboardStatsDto> GetDashboardStatsAsync();
    }

    public class ReportService : IReportService
    {
        private readonly AppDbContext _context;

        public ReportService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardStatsDto> GetDashboardStatsAsync()
        {
            var invoices = await _context.Invoices.ToListAsync();

            return new DashboardStatsDto
            {
                TotalInvoices = invoices.Count,
                TotalRevenue = invoices.Where(i => i.Status == 1).Sum(i => i.Amount),
                PaidInvoices = invoices.Count(i => i.Status == 1),
                PendingInvoices = invoices.Count(i => i.Status == 0)
            };
        }
    }
}
