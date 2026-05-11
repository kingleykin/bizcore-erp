using Asp.Versioning;
using Admin.API.Application.DTOs;
using Admin.API.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Admin.API.Controllers
{
    /// <summary>
    /// Quản lý Pháp nhân (LegalEntity) — cấu trúc doanh nghiệp cấp cao nhất.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/org/legal-entities")]
    [Authorize]
    public class LegalEntitiesController : ControllerBase
    {
        private readonly IOrganizationService _service;

        public LegalEntitiesController(IOrganizationService service)
            => _service = service;

        /// <summary>Lấy danh sách tất cả pháp nhân.</summary>
        [HttpGet]
        [Authorize(Policy = "Admin.OrgView")]
        [ProducesResponseType(typeof(IEnumerable<LegalEntityResponse>), 200)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetLegalEntitiesAsync();
            return Ok(result);
        }

        /// <summary>Lấy chi tiết một pháp nhân theo ID.</summary>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = "Admin.OrgView")]
        [ProducesResponseType(typeof(LegalEntityResponse), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _service.GetLegalEntityByIdAsync(id);
            return result is null ? NotFound() : Ok(result);
        }

        /// <summary>Tạo pháp nhân mới.</summary>
        [HttpPost]
        [Authorize(Policy = "Admin.SysAdmin")]
        [ProducesResponseType(typeof(LegalEntityResponse), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(409)]
        public async Task<IActionResult> Create([FromBody] CreateLegalEntityRequest request)
        {
            try
            {
                var result = await _service.CreateLegalEntityAsync(request);
                return CreatedAtAction(nameof(GetById), new { id = result.Id, version = "1" }, result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        /// <summary>Cập nhật thông tin pháp nhân và publish LegalEntityUpdatedEvent.</summary>
        [HttpPut("{id:guid}")]
        [Authorize(Policy = "Admin.SysAdmin")]
        [ProducesResponseType(typeof(LegalEntityResponse), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLegalEntityRequest request)
        {
            var result = await _service.UpdateLegalEntityAsync(id, request);
            return result is null ? NotFound() : Ok(result);
        }

        /// <summary>Vô hiệu hóa pháp nhân.</summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "Admin.SysAdmin")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            var ok = await _service.DeactivateLegalEntityAsync(id);
            return ok ? NoContent() : NotFound();
        }
    }
}
