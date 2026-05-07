using Asp.Versioning;
using Bizcore.BuildingBlocks;
using Identity.API.Application.DTOs;
using Identity.API.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/roles")]
    [Authorize]
    public class RolesController : ControllerBase
    {
        private readonly IRoleService _roleService;

        public RolesController(IRoleService roleService)
        {
            _roleService = roleService;
        }

        /// <summary>Lấy danh sách tất cả roles.</summary>
        [HttpGet]
        [Authorize(Policy = "Identity.Roles.View")]
        public async Task<IActionResult> GetAll()
        {
            var roles = await _roleService.GetAllAsync();
            return Ok(roles);
        }

        /// <summary>Lấy chi tiết một role theo ID.</summary>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = "Identity.Roles.View")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var role = await _roleService.GetByIdAsync(id);
            return Ok(role);
        }

        /// <summary>Tạo role mới.</summary>
        [HttpPost]
        [Authorize(Policy = "Identity.Roles.Create")]
        public async Task<IActionResult> Create([FromBody] CreateRoleRequest request)
        {
            var role = await _roleService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = role.Id }, role);
        }

        /// <summary>Cập nhật role (không cho phép với System Roles).</summary>
        [HttpPut("{id:guid}")]
        [Authorize(Policy = "Identity.Roles.Update")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoleRequest request)
        {
            var role = await _roleService.UpdateAsync(id, request);
            return Ok(role);
        }

        /// <summary>Xóa role (không cho phép với System Roles).</summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "Identity.Roles.Delete")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _roleService.DeleteAsync(id);
            return NoContent();
        }

        /// <summary>Gán (thay thế) danh sách permission cho role.</summary>
        [HttpPut("{id:guid}/permissions")]
        [Authorize(Policy = "Identity.Roles.ManagePermissions")]
        public async Task<IActionResult> AssignPermissions(Guid id, [FromBody] AssignPermissionsRequest request)
        {
            await _roleService.AssignPermissionsAsync(id, request);
            return NoContent();
        }

        /// <summary>Lấy danh sách tất cả permissions có trong hệ thống.</summary>
        [HttpGet("permissions")]
        [Authorize(Policy = "Identity.Roles.View")]
        public async Task<IActionResult> GetAllPermissions()
        {
            var permissions = await _roleService.GetAllPermissionsAsync();
            return Ok(permissions);
        }
    }
}
