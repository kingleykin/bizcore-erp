using Admin.API.Application.DTOs;
using Admin.API.Domain.Entities.Organization;
using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Commands;

// 1. Create Branch
public record CreateBranchCommand(CreateBranchRequest Request) : IRequest<BranchResponse>, ITransactionalCommand;

public class CreateBranchHandler : IRequestHandler<CreateBranchCommand, BranchResponse>
{
    private readonly AdminDbContext _db;

    public CreateBranchHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<BranchResponse> Handle(CreateBranchCommand command, CancellationToken ct)
    {
        var code = command.Request.Code.ToUpperInvariant();
        if (await _db.Branches.AnyAsync(b => b.Code == code, ct))
            throw new InvalidOperationException($"Branch code '{code}' already exists.");

        var legalEntity = await _db.LegalEntities.FindAsync(new object[] { command.Request.LegalEntityId }, ct);
        if (legalEntity == null) throw new KeyNotFoundException($"LegalEntity {command.Request.LegalEntityId} not found");

        var branch = Branch.Create(
            command.Request.LegalEntityId,
            code,
            command.Request.Name,
            command.Request.Address);

        _db.Branches.Add(branch);

        return new BranchResponse(
            branch.Id, branch.LegalEntityId, legalEntity.Name, branch.Code, branch.Name, branch.Address, branch.IsActive, branch.CreatedAt, branch.UpdatedAt);
    }
}

// 2. Update Branch
public record UpdateBranchCommand(Guid Id, UpdateBranchRequest Request) : IRequest<BranchResponse>, ITransactionalCommand;

public class UpdateBranchHandler : IRequestHandler<UpdateBranchCommand, BranchResponse>
{
    private readonly AdminDbContext _db;

    public UpdateBranchHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<BranchResponse> Handle(UpdateBranchCommand command, CancellationToken ct)
    {
        var branch = await _db.Branches.Include(b => b.LegalEntity).FirstOrDefaultAsync(b => b.Id == command.Id, ct);
        if (branch == null) throw new KeyNotFoundException($"Branch {command.Id} not found");

        branch.Update(command.Request.Name, command.Request.Address);

        return new BranchResponse(
            branch.Id, branch.LegalEntityId, branch.LegalEntity.Name, branch.Code, branch.Name, branch.Address, branch.IsActive, branch.CreatedAt, branch.UpdatedAt);
    }
}

// 3. Deactivate Branch
public record DeactivateBranchCommand(Guid Id) : IRequest<bool>, ITransactionalCommand;

public class DeactivateBranchHandler : IRequestHandler<DeactivateBranchCommand, bool>
{
    private readonly AdminDbContext _db;

    public DeactivateBranchHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<bool> Handle(DeactivateBranchCommand command, CancellationToken ct)
    {
        var branch = await _db.Branches.FindAsync(new object[] { command.Id }, ct);
        if (branch == null) return false;

        branch.Deactivate();
        return true;
    }
}
