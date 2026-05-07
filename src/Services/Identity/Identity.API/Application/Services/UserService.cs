using Bizcore.BuildingBlocks.Exceptions;
using Identity.API.Application.DTOs;
using Identity.API.Domain.Entities;
using Identity.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IdentityDbContext _db;
        private readonly ILogger<UserService> _logger;

        public UserService(IdentityDbContext db, ILogger<UserService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<IEnumerable<UserDto>> GetAllAsync()
        {
            var users = await _db.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .AsNoTracking()
                .ToListAsync();

            return users.Select(MapToDto);
        }

        public async Task<UserDto> GetByIdAsync(Guid id)
        {
            var user = await _db.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id)
                ?? throw new NotFoundException("User", id);

            return MapToDto(user);
        }

        public async Task<UserDto> CreateAsync(CreateUserRequest request)
        {
            // Kiểm tra username unique
            if (await _db.Users.AnyAsync(u => u.Username == request.Username.ToLowerInvariant()))
                throw new DomainException($"Username '{request.Username}' is already taken.");

            if (await _db.Users.AnyAsync(u => u.Email == request.Email.ToLowerInvariant()))
                throw new DomainException($"Email '{request.Email}' is already registered.");

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            var user = User.Create(request.Username, request.Email, passwordHash);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Created user '{Username}' (Id: {Id}).", user.Username, user.Id);
            return await GetByIdAsync(user.Id);
        }

        public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request)
        {
            var user = await _db.Users.FindAsync(id)
                ?? throw new NotFoundException("User", id);

            if (await _db.Users.AnyAsync(u => u.Email == request.Email.ToLowerInvariant() && u.Id != id))
                throw new DomainException($"Email '{request.Email}' is already registered by another user.");

            user.UpdateProfile(request.Email);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Updated user '{Id}'.", id);
            return await GetByIdAsync(id);
        }

        public async Task DeleteAsync(Guid id)
        {
            var user = await _db.Users.FindAsync(id)
                ?? throw new NotFoundException("User", id);

            // Soft delete: deactivate rather than hard delete (preserves audit trail)
            user.Deactivate();
            await _db.SaveChangesAsync();

            _logger.LogInformation("Deactivated user '{Username}' (Id: {Id}).", user.Username, id);
        }

        public async Task AssignRolesAsync(Guid userId, AssignRolesRequest request)
        {
            var user = await _db.Users.FindAsync(userId)
                ?? throw new NotFoundException("User", userId);

            // Validate roles exist
            var roleIds = request.RoleIds.Distinct().ToList();
            var existingRoles = await _db.Roles
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => r.Id)
                .ToListAsync();

            if (existingRoles.Count != roleIds.Count)
                throw new DomainException("One or more role IDs are invalid.");

            // Remove current roles
            var currentRoles = await _db.UserRoles.Where(ur => ur.UserId == userId).ToListAsync();
            _db.UserRoles.RemoveRange(currentRoles);

            // Assign new roles
            var newUserRoles = roleIds.Select(rid => new UserRole
            {
                UserId = userId,
                RoleId = rid,
                AssignedAt = DateTime.UtcNow
            });
            _db.UserRoles.AddRange(newUserRoles);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Assigned {Count} role(s) to user '{Id}'.", roleIds.Count, userId);
        }

        public async Task UnlockUserAsync(Guid userId)
        {
            var user = await _db.Users.FindAsync(userId)
                ?? throw new NotFoundException("User", userId);

            user.ResetFailedLogins();
            user.Activate();
            await _db.SaveChangesAsync();

            _logger.LogInformation("Unlocked user '{Username}' (Id: {Id}).", user.Username, userId);
        }

        private static UserDto MapToDto(User u) => new(
            u.Id,
            u.Username,
            u.Email,
            u.IsActive,
            u.FailedLoginAttempts,
            u.LockoutEnd,
            u.CreatedAt,
            u.UserRoles.Select(ur => ur.Role.Name)
        );
    }
}
