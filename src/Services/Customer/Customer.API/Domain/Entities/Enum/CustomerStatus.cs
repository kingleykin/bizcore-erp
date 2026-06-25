namespace Customer.API.Domain.Entities;

public enum CustomerStatus
{
    /// <summary>Khách hàng hoạt động bình thường</summary>
    Active = 0,
    /// <summary>Khách hàng đã tạo tài khoản user</summary>
    CreatedUser = 1,
    /// <summary>Khách hàng đã bị khóa tài khoản</summary>
    Blocked = 2
}