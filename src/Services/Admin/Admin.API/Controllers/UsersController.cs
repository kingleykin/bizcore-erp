using Asp.Versioning;
using Bizcore.BuildingBlocks;
using Admin.API.Application.DTOs;
using Admin.API.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Admin.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/users")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>Lấy danh sách tất cả người dùng.</summary>
        [HttpGet]
        [Authorize(Policy = "Identity.Users.View")]
        public async Task<IActionResult> GetAll()
        {
            var users = await _userService.GetAllAsync();
            return Ok(users);
        }

        /// <summary>Lấy chi tiết một người dùng theo ID.</summary>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = "Identity.Users.View")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var user = await _userService.GetByIdAsync(id);
            return Ok(user);
        }

        /// <summary>Tạo người dùng mới.</summary>
        [HttpPost]
        [Authorize(Policy = "Identity.Users.Create")]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
        {
            var user = await _userService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
        }

        /// <summary>Cập nhật thông tin người dùng.</summary>
        [HttpPut("{id:guid}")]
        [Authorize(Policy = "Identity.Users.Update")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request)
        {
            var user = await _userService.UpdateAsync(id, request);
            return Ok(user);
        }

        /// <summary>Vô hiệu hóa (soft-delete) người dùng.</summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "Identity.Users.Delete")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _userService.DeleteAsync(id);
            return NoContent();
        }

        /// <summary>Gán (thay thế) danh sách role cho người dùng.</summary>
        [HttpPut("{id:guid}/roles")]
        [Authorize(Policy = "Identity.Users.ManageRoles")]
        public async Task<IActionResult> AssignRoles(Guid id, [FromBody] AssignRolesRequest request)
        {
            await _userService.AssignRolesAsync(id, request);
            return NoContent();
        }

        /// <summary>Mở khóa tài khoản bị khóa do đăng nhập sai nhiều lần.</summary>
        [HttpPost("{id:guid}/unlock")]
        [Authorize(Policy = "Identity.Users.Update")]
        public async Task<IActionResult> Unlock(Guid id)
        {
            await _userService.UnlockUserAsync(id);
            return NoContent();
        }

        /// <summary>Cập nhật ảnh đại diện của người dùng.</summary>
        [HttpPut("{id:guid}/avatar")]
        [Authorize(Policy = "Identity.Users.Update")]
        public async Task<IActionResult> UpdateAvatar(Guid id, [FromBody] string? avatarUrl)
        {
            await _userService.UpdateAvatarAsync(id, avatarUrl);
            return NoContent();
        }

        /// <summary>Cập nhật ngôn ngữ ưu tiên của người dùng.</summary>
        [HttpPut("{id:guid}/language")]
        [Authorize] // Any authorized user can update their own language
        public async Task<IActionResult> UpdateLanguage(Guid id, [FromBody] string languageCode)
        {
            // Note: In production, verify that the logged-in user is updating their own language or has admin rights
            await _userService.UpdatePreferredLanguageAsync(id, languageCode);
            return NoContent();
        }
    }
}
