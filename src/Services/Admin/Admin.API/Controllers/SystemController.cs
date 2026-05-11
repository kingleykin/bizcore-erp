using Asp.Versioning;
using Admin.API.Application.DTOs;
using Admin.API.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Admin.API.Controllers
{
    /// <summary>
    /// Quản lý cấu hình hệ thống (GlobalSetting) và Lịch làm việc (SystemCalendar).
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/system")]
    [Authorize]
    public class SystemController : ControllerBase
    {
        private readonly ISystemSettingsService _service;

        public SystemController(ISystemSettingsService service)
            => _service = service;

        // ── Global Settings ────────────────────────────────────────────────────

        /// <summary>Lấy toàn bộ danh sách cấu hình hệ thống.</summary>
        [HttpGet("settings")]
        [Authorize(Policy = "Admin.SystemView")]
        [ProducesResponseType(typeof(IEnumerable<GlobalSettingResponse>), 200)]
        public async Task<IActionResult> GetSettings()
        {
            var result = await _service.GetAllSettingsAsync();
            return Ok(result);
        }

        /// <summary>Lấy giá trị của một setting theo key.</summary>
        [HttpGet("settings/{key}")]
        [Authorize(Policy = "Admin.SystemView")]
        [ProducesResponseType(typeof(GlobalSettingResponse), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetSetting(string key)
        {
            var result = await _service.GetSettingAsync(key);
            return result is null ? NotFound() : Ok(result);
        }

        /// <summary>Cập nhật giá trị một setting (chỉ áp dụng cho setting không phải IsReadOnly).</summary>
        [HttpPut("settings/{key}")]
        [Authorize(Policy = "Admin.SysAdmin")]
        [ProducesResponseType(typeof(GlobalSettingResponse), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> UpdateSetting(string key, [FromBody] UpdateSettingRequest request)
        {
            try
            {
                var result = await _service.UpdateSettingAsync(key, request);
                return result is null ? NotFound() : Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ── System Calendar ────────────────────────────────────────────────────

        /// <summary>Lấy lịch làm việc theo năm.</summary>
        [HttpGet("calendar/{year:int}")]
        [Authorize(Policy = "Admin.SystemView")]
        [ProducesResponseType(typeof(IEnumerable<SystemCalendarResponse>), 200)]
        public async Task<IActionResult> GetCalendar(int year)
        {
            var result = await _service.GetCalendarAsync(year);
            return Ok(result);
        }

        /// <summary>Tạo hoặc cập nhật ngày trong lịch làm việc (upsert).</summary>
        [HttpPost("calendar")]
        [Authorize(Policy = "Admin.SysAdmin")]
        [ProducesResponseType(typeof(SystemCalendarResponse), 200)]
        public async Task<IActionResult> UpsertCalendarDay([FromBody] UpsertCalendarRequest request)
        {
            var result = await _service.UpsertCalendarDayAsync(request);
            return Ok(result);
        }
    }
}
