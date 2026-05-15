using Microsoft.AspNetCore.Mvc;
using Report.API.Application.Queries;
using Report.API.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Asp.Versioning;
using MediatR;

namespace Report.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/report")]
[ApiVersion("1.0")]
[Authorize(Policy = "Report.View")]
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReportsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardStats(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDashboardStatsQuery(), ct);
        return Ok(result);
    }
}
