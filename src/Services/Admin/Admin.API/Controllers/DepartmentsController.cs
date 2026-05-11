using Asp.Versioning;
using Admin.API.Application.DTOs;
using Admin.API.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Admin.API.Controllers
{
    /// <summary>
    /// Quản lý Phòng ban (Department) dạng cây phân cấp.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/org/departments")]
    [Authorize]
    public class DepartmentsController : ControllerBase
    {
        private readonly IOrganizationService _service;

        public DepartmentsController(IOrganizationService service)
            => _service = service;

        /// <summary>
        /// Lấy sơ đồ phòng ban dạng Tree. Filter theo branchId nếu cung cấp.
        /// Response trả về các node gốc, mỗi node chứa Children đệ quy.
        /// </summary>
        [HttpGet]
        [Authorize(Policy = "Admin.OrgView")]
        [ProducesResponseType(typeof(IEnumerable<DepartmentResponse>), 200)]
        public async Task<IActionResult> GetTree([FromQuery] Guid? branchId = null)
        {
            var result = await _service.GetDepartmentTreeAsync(branchId);
            return Ok(result);
        }

        /// <summary>Tạo phòng ban mới.</summary>
        [HttpPost]
        [Authorize(Policy = "Admin.SysAdmin")]
        [ProducesResponseType(typeof(DepartmentResponse), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(409)]
        public async Task<IActionResult> Create([FromBody] CreateDepartmentRequest request)
        {
            try
            {
                var result = await _service.CreateDepartmentAsync(request);
                return StatusCode(201, result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        /// <summary>Cập nhật phòng ban.</summary>
        [HttpPut("{id:guid}")]
        [Authorize(Policy = "Admin.SysAdmin")]
        [ProducesResponseType(typeof(DepartmentResponse), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDepartmentRequest request)
        {
            var result = await _service.UpdateDepartmentAsync(id, request);
            return result is null ? NotFound() : Ok(result);
        }
    }
}
