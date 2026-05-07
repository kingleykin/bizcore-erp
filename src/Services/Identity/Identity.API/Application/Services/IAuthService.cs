using Identity.API.Application.DTOs;

namespace Identity.API.Application.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress = null);
        Task<LoginResponse> RefreshTokenAsync(string refreshToken, string? ipAddress = null);
        Task LogoutAsync(string refreshToken);
        Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    }
}
