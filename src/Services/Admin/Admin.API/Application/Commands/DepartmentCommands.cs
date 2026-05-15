using Admin.API.Application.DTOs;
using Admin.API.Domain.Entities.Organization;
using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Commands;

// 1. Create Department
public record CreateDepartmentCommand(CreateDepartmentRequest Request) : IRequest<DepartmentResponse>, ITransactionalCommand;

public class CreateDepartmentHandler : IRequestHandler<CreateDepartmentCommand, DepartmentResponse>
{
    private readonly AdminDbContext _db;

    public CreateDepartmentHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<DepartmentResponse> Handle(CreateDepartmentCommand command, CancellationToken ct)
    {
        var code = command.Request.Code.ToUpperInvariant();
        if (await _db.Departments.AnyAsync(d => d.Code == code, ct))
            throw new InvalidOperationException($"Department code '{code}' already exists.");

        var branch = await _db.Branches.FindAsync(new object[] { command.Request.BranchId }, ct);
        if (branch == null) throw new KeyNotFoundException($"Branch {command.Request.BranchId} not found");

        var department = Department.Create(
            command.Request.BranchId,
            code,
            command.Request.Name,
            command.Request.ParentId);

        _db.Departments.Add(department);

        string? parentName = null;
        if (department.ParentId.HasValue)
        {
            var parent = await _db.Departments.FindAsync(new object[] { department.ParentId.Value }, ct);
            parentName = parent?.Name;
        }

        return new DepartmentResponse(
            department.Id, department.BranchId, branch.Name, department.ParentId, parentName, department.Code, department.Name, department.CreatedAt, department.UpdatedAt, new List<DepartmentResponse>());
    }
}

// 2. Update Department
public record UpdateDepartmentCommand(Guid Id, UpdateDepartmentRequest Request) : IRequest<DepartmentResponse>, ITransactionalCommand;

public class UpdateDepartmentHandler : IRequestHandler<UpdateDepartmentCommand, DepartmentResponse>
{
    private readonly AdminDbContext _db;

    public UpdateDepartmentHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<DepartmentResponse> Handle(UpdateDepartmentCommand command, CancellationToken ct)
    {
        var d = await _db.Departments.Include(x => x.Branch).Include(x => x.Parent).FirstOrDefaultAsync(x => x.Id == command.Id, ct);
        if (d == null) throw new KeyNotFoundException($"Department {command.Id} not found");

        d.Update(command.Request.Name, command.Request.ParentId);

        return new DepartmentResponse(
            d.Id, d.BranchId, d.Branch.Name, d.ParentId, d.Parent?.Name, d.Code, d.Name, d.CreatedAt, d.UpdatedAt, new List<DepartmentResponse>());
    }
}

// 3. Delete Department
public record DeleteDepartmentCommand(Guid Id) : IRequest, ITransactionalCommand;

public class DeleteDepartmentHandler : IRequestHandler<DeleteDepartmentCommand>
{
    private readonly AdminDbContext _db;

    public DeleteDepartmentHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task Handle(DeleteDepartmentCommand command, CancellationToken ct)
    {
        var d = await _db.Departments.FindAsync(new object[] { command.Id }, ct);
        if (d != null)
        {
            _db.Departments.Remove(d);
        }
    }
}
