using Report.API.Application.Services;
using Report.API.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace Report.API.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/report")]
    [ApiVersion("1.0")]
    [Authorize(Policy = "Report.View")] // Require specific permission
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
