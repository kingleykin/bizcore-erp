using System;

namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Event publish khi permission của một role thay đổi.
    /// </summary>
    public interface IRolePermissionsChangedEvent
    {
        Guid RoleId { get; }
        string RoleName { get; }
        DateTime ChangedAt { get; }
    }

    /// <summary>
    /// Event publish khi permission của một user cụ thể bị thay đổi (ví dụ: gán role mới).
    /// </summary>
    public interface IUserPermissionsChangedEvent
    {
        Guid UserId { get; }
        DateTime ChangedAt { get; }
    }
}
