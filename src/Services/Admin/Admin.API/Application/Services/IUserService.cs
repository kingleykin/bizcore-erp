using Admin.API.Application.DTOs;

namespace Admin.API.Application.Services
{
    public interface IUserService
    {
        Task<IEnumerable<UserDto>> GetAllAsync();
        Task<UserDto> GetByIdAsync(Guid id);
        Task<UserDto> CreateAsync(CreateUserRequest request);
        Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request);
        Task DeleteAsync(Guid id);
        Task AssignRolesAsync(Guid userId, AssignRolesRequest request);
        Task UnlockUserAsync(Guid userId);
        Task UpdateAvatarAsync(Guid userId, string? avatarUrl);
    }
}
