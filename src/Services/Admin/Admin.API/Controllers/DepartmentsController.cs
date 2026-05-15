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
/// Quản lý Phòng ban (Department) dạng cây phân cấp.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/departments")]
[Authorize]
public class DepartmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public DepartmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lấy sơ đồ phòng ban dạng Tree. Filter theo branchId nếu cung cấp.
    /// Response trả về các node gốc, mỗi node chứa Children đệ quy.
    /// </summary>
    [HttpGet]
    [RequirePermission(Permissions.Admin.OrgView)]
    [ProducesResponseType(typeof(IEnumerable<DepartmentResponse>), 200)]
    public async Task<IActionResult> GetTree([FromQuery] Guid? branchId = null)
    {
        var result = await _mediator.Send(new GetDepartmentTreeQuery(branchId));
        return Ok(result);
    }

    /// <summary>Tạo phòng ban mới.</summary>
    [HttpPost]
    [RequirePermission(Permissions.Admin.SysAdmin)]
    [ProducesResponseType(typeof(DepartmentResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Create([FromBody] CreateDepartmentRequest request)
    {
        try
        {
            var result = await _mediator.Send(new CreateDepartmentCommand(request));
            return StatusCode(201, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Cập nhật phòng ban.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.Admin.SysAdmin)]
    [ProducesResponseType(typeof(DepartmentResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDepartmentRequest request)
    {
        try
        {
            var result = await _mediator.Send(new UpdateDepartmentCommand(id, request));
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
