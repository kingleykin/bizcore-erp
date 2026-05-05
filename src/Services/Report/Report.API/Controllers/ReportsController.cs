using Report.API.Application.Services;
using Report.API.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Report.API.Controllers
{
    [ApiController]
    [Route("report")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<DashboardStatsDto>> GetDashboardStats()
        {
            var stats = await _reportService.GetDashboardStatsAsync();
            return Ok(stats);
        }
    }
}
