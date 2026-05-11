using Asp.Versioning;
using Admin.API.Application.DTOs;
using Admin.API.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Admin.API.Controllers
{
    /// <summary>
    /// Quản lý Trung tâm chi phí (CostCenter).
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/org/cost-centers")]
    [Authorize]
    public class CostCentersController : ControllerBase
    {
        private readonly IOrganizationService _service;

        public CostCentersController(IOrganizationService service)
            => _service = service;

        /// <summary>Lấy danh sách cost center. Filter theo legalEntityId nếu cung cấp.</summary>
        [HttpGet]
        [Authorize(Policy = "Admin.OrgView")]
        [ProducesResponseType(typeof(IEnumerable<CostCenterResponse>), 200)]
        public async Task<IActionResult> GetAll([FromQuery] Guid? legalEntityId = null)
        {
            var result = await _service.GetCostCentersAsync(legalEntityId);
            return Ok(result);
        }

        /// <summary>Tạo cost center mới.</summary>
        [HttpPost]
        [Authorize(Policy = "Admin.SysAdmin")]
        [ProducesResponseType(typeof(CostCenterResponse), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(409)]
        public async Task<IActionResult> Create([FromBody] CreateCostCenterRequest request)
        {
            try
            {
                var result = await _service.CreateCostCenterAsync(request);
                return StatusCode(201, result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }
    }
}
