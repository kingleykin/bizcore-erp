using Asp.Versioning;
using Admin.API.Application.DTOs;
using Admin.API.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Admin.API.Controllers
{
    /// <summary>
    /// Danh mục Tiền tệ hệ thống (Global).
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/system/currencies")]
    [Authorize]
    public class CurrenciesController : ControllerBase
    {
        private readonly ISystemSettingsService _service;

        public CurrenciesController(ISystemSettingsService service)
            => _service = service;

        /// <summary>Lấy danh sách tiền tệ.</summary>
        [HttpGet]
        [Authorize(Policy = "Admin.SystemView")]
        [ProducesResponseType(typeof(IEnumerable<CurrencyResponse>), 200)]
        public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = true)
        {
            var result = await _service.GetCurrenciesAsync(activeOnly);
            return Ok(result);
        }

        /// <summary>Lấy thông tin một loại tiền tệ theo mã ISO 4217.</summary>
        [HttpGet("{code}")]
        [Authorize(Policy = "Admin.SystemView")]
        [ProducesResponseType(typeof(CurrencyResponse), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetByCode(string code)
        {
            var result = await _service.GetCurrencyByCodeAsync(code);
            return result is null ? NotFound() : Ok(result);
        }

        /// <summary>Thêm tiền tệ mới vào hệ thống.</summary>
        [HttpPost]
        [Authorize(Policy = "Admin.SysAdmin")]
        [ProducesResponseType(typeof(CurrencyResponse), 201)]
        [ProducesResponseType(409)]
        public async Task<IActionResult> Create([FromBody] CreateCurrencyRequest request)
        {
            try
            {
                var result = await _service.CreateCurrencyAsync(request);
                return CreatedAtAction(nameof(GetByCode), new { code = result.Code, version = "1" }, result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        /// <summary>Vô hiệu hóa một loại tiền tệ.</summary>
        [HttpDelete("{code}")]
        [Authorize(Policy = "Admin.SysAdmin")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Deactivate(string code)
        {
            var ok = await _service.DeactivateCurrencyAsync(code);
            return ok ? NoContent() : NotFound();
        }
    }
}
