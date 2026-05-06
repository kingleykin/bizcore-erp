using Microsoft.AspNetCore.Mvc;
using Report.API.Application.Services;
using Report.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Asp.Versioning;

namespace Report.API.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/report")]
    [ApiVersion("1.0")]
    [Authorize(Policy = "Report.View")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(IReportService reportService, ILogger<ReportsController> logger)
        {
            _reportService = reportService;
            _logger = logger;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<DashboardStatsDto>> GetDashboardStats()
        {
            _logger.LogInformation("Retrieving dashboard summary");
            var stats = await _reportService.GetDashboardStatsAsync();
            _logger.LogInformation("Dashboard summary retrieved: TotalInvoices={TotalInvoices}, TotalRevenue={TotalRevenue}",
                stats.TotalInvoices, stats.TotalRevenue);
            return Ok(stats);
        }
    }
}
