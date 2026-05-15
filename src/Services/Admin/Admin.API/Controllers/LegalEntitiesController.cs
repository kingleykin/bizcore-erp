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
/// Quản lý Pháp nhân (LegalEntity) — cấu trúc doanh nghiệp cấp cao nhất.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/legal-entities")]
[Authorize]
public class LegalEntitiesController : ControllerBase
{
    private readonly IMediator _mediator;

    public LegalEntitiesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Lấy danh sách tất cả pháp nhân.</summary>
    [HttpGet]
    [RequirePermission(Permissions.Admin.OrgView)]
    [ProducesResponseType(typeof(IEnumerable<LegalEntityResponse>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _mediator.Send(new GetLegalEntitiesQuery());
        return Ok(result);
    }

    /// <summary>Lấy chi tiết một pháp nhân theo ID.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.Admin.OrgView)]
    [ProducesResponseType(typeof(LegalEntityResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetLegalEntityByIdQuery(id));
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Tạo pháp nhân mới.</summary>
    [HttpPost]
    [RequirePermission(Permissions.Admin.SysAdmin)]
    [ProducesResponseType(typeof(LegalEntityResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Create([FromBody] CreateLegalEntityRequest request)
    {
        try
        {
            var result = await _mediator.Send(new CreateLegalEntityCommand(request));
            return CreatedAtAction(nameof(GetById), new { id = result.Id, version = "1.0" }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Cập nhật thông tin pháp nhân.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.Admin.SysAdmin)]
    [ProducesResponseType(typeof(LegalEntityResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLegalEntityRequest request)
    {
        try
        {
            var result = await _mediator.Send(new UpdateLegalEntityCommand(id, request));
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Vô hiệu hóa pháp nhân.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.Admin.SysAdmin)]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var ok = await _mediator.Send(new DeactivateLegalEntityCommand(id));
        return ok ? NoContent() : NotFound();
    }
}
