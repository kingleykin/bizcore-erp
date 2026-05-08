using FluentValidation;

namespace Identity.API.Application.DTOs
{
    // ── Auth ──────────────────────────────────────────────────────────────────
    public record LoginRequest(string Username, string Password);

    public record LoginResponse(
        string AccessToken,
        string RefreshToken,
        DateTime AccessTokenExpiry,
        string Username,
        string[] Roles,
        string[] Permissions
    );

    public record RefreshTokenRequest(string RefreshToken);

    public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    // ── User ──────────────────────────────────────────────────────────────────
    public record CreateUserRequest(string Username, string Email, string Password);

    public record UpdateUserRequest(string Email);

    public record AssignRolesRequest(IEnumerable<Guid> RoleIds);

    public record UserDto(
        Guid Id,
        string Username,
        string Email,
        bool IsActive,
        int FailedLoginAttempts,
        DateTime? LockoutEnd,
        DateTime CreatedAt,
        IEnumerable<string> Roles
    );

    // ── Role ──────────────────────────────────────────────────────────────────
    public record CreateRoleRequest(string Name, string? Description);

    public record UpdateRoleRequest(string Name, string? Description);

    public record AssignPermissionsRequest(IEnumerable<Guid> PermissionIds);

    public record RoleDto(
        Guid Id,
        string Name,
        string? Description,
        bool IsSystem,
        IEnumerable<PermissionDto> Permissions
    );

    // ── Permission ────────────────────────────────────────────────────────────
    public record PermissionDto(Guid Id, string Code, string Name, string Scope, string? Description);


    // ── Validators ────────────────────────────────────────────────────────────
    public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
    {
        public CreateUserRequestValidator()
        {
            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("Username is required.")
                .MinimumLength(3).WithMessage("Username must be at least 3 characters.")
                .MaximumLength(50).WithMessage("Username must not exceed 50 characters.")
                .Matches("^[a-zA-Z0-9_]+$").WithMessage("Username can only contain letters, numbers and underscores.");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Invalid email format.");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required.")
                .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
                .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
                .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
                .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
                .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");
        }
    }

    public class LoginRequestValidator : AbstractValidator<LoginRequest>
    {
        public LoginRequestValidator()
        {
            RuleFor(x => x.Username).NotEmpty().WithMessage("Username is required.");
            RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required.");
        }
    }

    public class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
    {
        public CreateRoleRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Role name is required.")
                .MaximumLength(100).WithMessage("Role name must not exceed 100 characters.");
        }
    }
}
