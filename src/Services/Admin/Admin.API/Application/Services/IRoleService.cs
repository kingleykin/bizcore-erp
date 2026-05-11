using Admin.API.Application.DTOs;

namespace Admin.API.Application.Services
{
    public interface IRoleService
    {
        Task<IEnumerable<RoleDto>> GetAllAsync();
        Task<RoleDto> GetByIdAsync(Guid id);
        Task<RoleDto> CreateAsync(CreateRoleRequest request);
        Task<RoleDto> UpdateAsync(Guid id, UpdateRoleRequest request);
        Task DeleteAsync(Guid id);
        Task AssignPermissionsAsync(Guid roleId, AssignPermissionsRequest request);
        Task<IEnumerable<PermissionDto>> GetAllPermissionsAsync();
    }
}
