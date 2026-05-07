using Identity.API.Application.DTOs;

namespace Identity.API.Application.Services
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
    }
}
