using Asp.Versioning;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Authorization;
using Admin.API.Application.DTOs;
using Admin.API.Application.Commands;
using Admin.API.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;

namespace Admin.API.Controllers;

/// <summary>
/// Quản lý Trung tâm chi phí (CostCenter).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/cost-centers")]
[Authorize]
public class CostCentersController : ControllerBase
{
    private readonly IMediator _mediator;

    public CostCentersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Lấy danh sách cost center. Filter theo legalEntityId nếu cung cấp.</summary>
    [HttpGet]
    [RequirePermission(Permissions.Admin.OrgView)]
    [ProducesResponseType(typeof(IEnumerable<CostCenterResponse>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? legalEntityId = null)
    {
        var result = await _mediator.Send(new GetCostCentersQuery(legalEntityId));
        return Ok(result);
    }

    /// <summary>Tạo cost center mới.</summary>
    [HttpPost]
    [RequirePermission(Permissions.Admin.SysAdmin)]
    [ProducesResponseType(typeof(CostCenterResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Create([FromBody] CreateCostCenterRequest request)
    {
        try
        {
            var result = await _mediator.Send(new CreateCostCenterCommand(request));
            return StatusCode(201, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
