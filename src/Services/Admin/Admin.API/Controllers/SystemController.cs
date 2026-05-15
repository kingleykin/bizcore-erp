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
/// Quản lý cấu hình hệ thống (GlobalSetting) và Lịch làm việc (SystemCalendar).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/system")]
[Authorize]
public class SystemController : ControllerBase
{
    private readonly IMediator _mediator;

    public SystemController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ── Global Settings ────────────────────────────────────────────────────

    /// <summary>Lấy toàn bộ danh sách cấu hình hệ thống.</summary>
    [HttpGet("settings")]
    [RequirePermission(Permissions.Admin.SystemView)]
    [ProducesResponseType(typeof(IEnumerable<GlobalSettingResponse>), 200)]
    public async Task<IActionResult> GetSettings()
    {
        var result = await _mediator.Send(new GetSettingsQuery());
        return Ok(result);
    }

    /// <summary>Lấy giá trị của một setting theo key.</summary>
    [HttpGet("settings/{key}")]
    [RequirePermission(Permissions.Admin.SystemView)]
    [ProducesResponseType(typeof(GlobalSettingResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetSetting(string key)
    {
        var result = await _mediator.Send(new GetSettingByKeyQuery(key));
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Cập nhật giá trị một setting (chỉ áp dụng cho setting không phải IsReadOnly).</summary>
    [HttpPut("settings/{key}")]
    [RequirePermission(Permissions.Admin.SysAdmin)]
    [ProducesResponseType(typeof(GlobalSettingResponse), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UpdateSetting(string key, [FromBody] UpdateSettingRequest request)
    {
        try
        {
            var result = await _mediator.Send(new UpdateSettingCommand(key, request));
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── System Calendar ────────────────────────────────────────────────────

    /// <summary>Lấy lịch làm việc theo năm.</summary>
    [HttpGet("calendar/{year:int}")]
    [RequirePermission(Permissions.Admin.SystemView)]
    [ProducesResponseType(typeof(IEnumerable<SystemCalendarResponse>), 200)]
    public async Task<IActionResult> GetCalendar(int year)
    {
        var result = await _mediator.Send(new GetCalendarQuery(year));
        return Ok(result);
    }

    /// <summary>Tạo hoặc cập nhật ngày trong lịch làm việc (upsert).</summary>
    [HttpPost("calendar")]
    [RequirePermission(Permissions.Admin.SysAdmin)]
    [ProducesResponseType(typeof(SystemCalendarResponse), 200)]
    public async Task<IActionResult> UpsertCalendarDay([FromBody] UpsertCalendarRequest request)
    {
        var result = await _mediator.Send(new UpsertCalendarDayCommand(request));
        return Ok(result);
    }
}
