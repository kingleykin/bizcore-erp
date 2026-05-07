using Asp.Versioning;
using Bizcore.BuildingBlocks;
using Identity.API.Application.DTOs;
using Identity.API.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Identity.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>Đăng nhập và nhận JWT + Refresh Token.</summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _authService.LoginAsync(request, ip);
            return Ok(result);
        }

        /// <summary>Đổi Refresh Token lấy Access Token mới (Rotation).</summary>
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _authService.RefreshTokenAsync(request.RefreshToken, ip);
            return Ok(result);
        }

        /// <summary>Đăng xuất — thu hồi Refresh Token.</summary>
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
        {
            await _authService.LogoutAsync(request.RefreshToken);
            return NoContent();
        }

        /// <summary>Đổi mật khẩu (người dùng tự đổi).</summary>
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? throw new InvalidOperationException("User ID not found in token."));

            await _authService.ChangePasswordAsync(userId, request);
            return NoContent();
        }
    }
}
