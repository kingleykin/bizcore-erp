namespace Admin.API.Application.DTOs
{
    public record NavigationMenuDto(
        Guid    Id,
        Guid?   ParentId,
        string  Name,
        string  Route,
        string? Icon,
        int     SortOrder
    );

    public record UserPermissionsDto(
        Guid     UserId,
        string   Username,
        string[] Roles,
        string[] Permissions
    );
}
