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
/// Danh mục Tiền tệ hệ thống (Global).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/system/currencies")]
[Authorize]
public class CurrenciesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CurrenciesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Lấy danh sách tiền tệ.</summary>
    [HttpGet]
    [RequirePermission(Permissions.Admin.SystemView)]
    [ProducesResponseType(typeof(IEnumerable<CurrencyResponse>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = true)
    {
        var result = await _mediator.Send(new GetCurrenciesQuery(activeOnly));
        return Ok(result);
    }

    /// <summary>Lấy thông tin một loại tiền tệ theo mã ISO 4217.</summary>
    [HttpGet("{code}")]
    [RequirePermission(Permissions.Admin.SystemView)]
    [ProducesResponseType(typeof(CurrencyResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetByCode(string code)
    {
        var result = await _mediator.Send(new GetCurrencyByCodeQuery(code));
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Thêm tiền tệ mới vào hệ thống.</summary>
    [HttpPost]
    [RequirePermission(Permissions.Admin.SysAdmin)]
    [ProducesResponseType(typeof(CurrencyResponse), 201)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Create([FromBody] CreateCurrencyRequest request)
    {
        try
        {
            var result = await _mediator.Send(new CreateCurrencyCommand(request));
            return CreatedAtAction(nameof(GetByCode), new { code = result.Code, version = "1.0" }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Vô hiệu hóa một loại tiền tệ.</summary>
    [HttpDelete("{code}")]
    [RequirePermission(Permissions.Admin.SysAdmin)]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Deactivate(string code)
    {
        var ok = await _mediator.Send(new DeactivateCurrencyCommand(code));
        return ok ? NoContent() : NotFound();
    }
}
