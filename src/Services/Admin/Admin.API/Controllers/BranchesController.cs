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
/// Quản lý Chi nhánh (Branch).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/branches")]
[Authorize]
public class BranchesController : ControllerBase
{
    private readonly IMediator _mediator;

    public BranchesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Lấy danh sách chi nhánh. Filter theo legalEntityId nếu cung cấp.</summary>
    [HttpGet]
    [RequirePermission(Permissions.Admin.OrgView)]
    [ProducesResponseType(typeof(IEnumerable<BranchResponse>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? legalEntityId = null)
    {
        var result = await _mediator.Send(new GetBranchesQuery(legalEntityId));
        return Ok(result);
    }

    /// <summary>Lấy chi tiết một chi nhánh.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.Admin.OrgView)]
    [ProducesResponseType(typeof(BranchResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetBranchByIdQuery(id));
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Tạo chi nhánh mới.</summary>
    [HttpPost]
    [RequirePermission(Permissions.Admin.SysAdmin)]
    [ProducesResponseType(typeof(BranchResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Create([FromBody] CreateBranchRequest request)
    {
        try
        {
            var result = await _mediator.Send(new CreateBranchCommand(request));
            return CreatedAtAction(nameof(GetById), new { id = result.Id, version = "1.0" }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Cập nhật thông tin chi nhánh.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.Admin.SysAdmin)]
    [ProducesResponseType(typeof(BranchResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBranchRequest request)
    {
        try
        {
            var result = await _mediator.Send(new UpdateBranchCommand(id, request));
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
