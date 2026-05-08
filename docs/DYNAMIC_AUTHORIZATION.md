# Giải pháp Dynamic Authorization cho BizCore ERP

Giải pháp này nâng cấp hệ thống phân quyền từ RBAC tĩnh sang **Dynamic Authorization** đầy đủ, hỗ trợ điều khiển UI động và kiểm soát truy cập mức độ sâu.

---

## 1. Trạng thái triển khai (Implementation Status)

| Phase | Mục tiêu | Trạng thái |
| --- | --- | --- |
| **Phase 1** | Mở rộng Schema, Navigation Entity, Seeder metadata | ✅ Hoàn thành |
| **Phase 2** | Dynamic Policy Provider, Redis Cache, Me Endpoints | ✅ Hoàn thành |
| **Phase 3** | Invalidate Cache qua Event, Audit Permission changes | ✅ Hoàn thành |
| **Phase 4** | Field-level masking, Data-level query filters | 📅 Kế hoạch |

---

## 2. Mô hình phân quyền (Permission Model)

### 2.1 Cấu trúc Permission Code
Sử dụng convention PascalCase dot-notation để đồng bộ với namespace và dễ đọc trên Frontend:
- `{Resource}.{Action}` (Ví dụ: `Invoice.Create`, `Payment.View`)
- `{Resource}.{Field}.{Action}` (Ví dụ: `Invoice.Amount.Edit`)
- `Menu.{Name}` (Ví dụ: `Menu.Invoice`)

### 2.2 Các loại Scope
- **Menu**: Kiểm soát hiển thị mục lục trên SideBar.
- **Page**: Quyền truy cập vào toàn bộ một trang/màn hình.
- **Action**: Quyền thực hiện một hành động (Button click, API call).
- **Field**: Quyền xem hoặc sửa một trường dữ liệu cụ thể.

---

## 3. Kiến trúc kỹ thuật (Technical Architecture)

### 3.1 Dynamic Policy Provider
Sử dụng `IAuthorizationPolicyProvider` để tự động tạo Policy từ chuỗi Permission Code. 
- **Cách dùng**: Chỉ cần đặt `[RequirePermission(Permissions.Invoice.View)]` lên Controller. Hệ thống sẽ tự tạo policy runtime mà không cần khai báo tĩnh trong `Program.cs`.

### 3.2 Permission Caching (Redis)
Để tránh truy vấn Database Identity liên tục mỗi khi kiểm tra quyền (mỗi API call), hệ thống sử dụng Redis làm cache layer:
- **Key**: `user_permissions:{userId}`
- **TTL**: 5 phút.
- **Fallback**: Nếu Redis chết, hệ thống tự động fallback về Database SQL Server để đảm bảo tính sẵn sàng cao.

### 3.3 Dynamic Navigation
Bảng `NavigationMenus` chứa cấu trúc cây menu.
- Endpoint `GET /me/navigation` sẽ lọc các menu item mà user có quyền dựa trên `PermissionCode` của menu đó.
- Frontend render menu hoàn toàn dựa trên API này, không hardcode routes.

### 3.4 Real-time Invalidation (Phase 3)
Để đảm bảo tính nhất quán ngay lập tức khi Admin thay đổi quyền hạn:
- **Event-driven**: Identity.API publish `IRolePermissionsChangedEvent` khi thay đổi quyền của Role.
- **Immediate lookup**: `PermissionAuthorizationHandler` ưu tiên kiểm tra quyền trong Redis. Nếu Role bị xóa cache, request tiếp theo sẽ tự động re-fetch quyền mới nhất từ DB.
- **User Tracking**: Hệ thống track danh sách user trong từng role (Redis Set) để hỗ trợ invalidation hàng loạt khi Role thay đổi.

### 3.5 Security Audit Logging (Phase 3)
Mọi thay đổi nhạy cảm về phân quyền đều được ghi Audit Trail:
- `Identity.Role.PermissionsAssigned`
- `Identity.User.RolesAssigned`
- Log bao gồm: Actor, EntityId, và Payload đã được mask thông tin nhạy cảm.

---

## 4. Database Schema

### 4.1 Permissions
- `Code`: Định danh duy nhất (PascalCase).
- `Resource`, `Action`, `Scope`: Metadata để phân loại và quản lý.

### 4.2 NavigationMenus
- `ParentId`: Hỗ trợ menu đa cấp.
- `PermissionCode`: Liên kết với bảng Permissions.

---

## 5. Hướng dẫn sử dụng cho Developer

### 5.1 Kiểm tra quyền trên API
Sử dụng attribute `RequirePermission` từ BuildingBlocks:
```csharp
[ApiController]
[Route("api/v1/invoices")]
[Authorize]
public class InvoicesController : ControllerBase
{
    [HttpPost]
    [RequirePermission(Permissions.Invoice.Create)]
    public async Task<IActionResult> Create(...) { ... }
}
```

### 5.2 Lấy thông tin cho Frontend
Frontend sau khi login nên gọi:
1. `GET /api/v1/me/permissions`: Lưu vào local state để toggle visibility của các button.
2. `GET /api/v1/me/navigation`: Render sidebar menu.

---
*Tài liệu cập nhật ngày: 08/05/2026.*
