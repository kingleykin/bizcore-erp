namespace Bizcore.BuildingBlocks.Authorization
{
    /// <summary>
    /// Abstraction cho permission cache — cho phép swap giữa MemoryCache và Redis.
    /// </summary>
    public interface IPermissionCache
    {
        /// <summary>
        /// Lấy danh sách permission codes của user. Trả về null nếu cache miss.
        /// </summary>
        Task<string[]?> GetAsync(Guid userId, CancellationToken ct = default);

        /// <summary>
        /// Lưu danh sách permissions vào cache với TTL mặc định.
        /// </summary>
        Task SetAsync(Guid userId, string[] permissions, CancellationToken ct = default);

        /// <summary>
        /// Xóa cache của một user cụ thể (khi role/permission thay đổi).
        /// </summary>
        Task InvalidateAsync(Guid userId, CancellationToken ct = default);

        /// <summary>
        /// Xóa cache của tất cả users thuộc một role (khi role permission thay đổi).
        /// </summary>
        Task InvalidateRoleAsync(Guid roleId, CancellationToken ct = default);

        /// <summary>
        /// Đăng ký userId vào role tracking set để hỗ trợ InvalidateRoleAsync.
        /// </summary>
        Task TrackUserInRoleAsync(Guid userId, Guid roleId);
    }
}
