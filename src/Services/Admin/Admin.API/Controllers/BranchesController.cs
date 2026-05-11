using Asp.Versioning;
using Admin.API.Application.DTOs;
using Admin.API.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Admin.API.Controllers
{
    /// <summary>
    /// Quản lý Chi nhánh (Branch).
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/org/branches")]
    [Authorize]
    public class BranchesController : ControllerBase
    {
        private readonly IOrganizationService _service;

        public BranchesController(IOrganizationService service)
            => _service = service;

        /// <summary>Lấy danh sách chi nhánh. Filter theo legalEntityId nếu cung cấp.</summary>
        [HttpGet]
        [Authorize(Policy = "Admin.OrgView")]
        [ProducesResponseType(typeof(IEnumerable<BranchResponse>), 200)]
        public async Task<IActionResult> GetAll([FromQuery] Guid? legalEntityId = null)
        {
            var result = await _service.GetBranchesAsync(legalEntityId);
            return Ok(result);
        }

        /// <summary>Lấy chi tiết một chi nhánh.</summary>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = "Admin.OrgView")]
        [ProducesResponseType(typeof(BranchResponse), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _service.GetBranchByIdAsync(id);
            return result is null ? NotFound() : Ok(result);
        }

        /// <summary>Tạo chi nhánh mới.</summary>
        [HttpPost]
        [Authorize(Policy = "Admin.SysAdmin")]
        [ProducesResponseType(typeof(BranchResponse), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(409)]
        public async Task<IActionResult> Create([FromBody] CreateBranchRequest request)
        {
            try
            {
                var result = await _service.CreateBranchAsync(request);
                return CreatedAtAction(nameof(GetById), new { id = result.Id, version = "1" }, result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        /// <summary>Cập nhật thông tin chi nhánh.</summary>
        [HttpPut("{id:guid}")]
        [Authorize(Policy = "Admin.SysAdmin")]
        [ProducesResponseType(typeof(BranchResponse), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBranchRequest request)
        {
            var result = await _service.UpdateBranchAsync(id, request);
            return result is null ? NotFound() : Ok(result);
        }
    }
}
