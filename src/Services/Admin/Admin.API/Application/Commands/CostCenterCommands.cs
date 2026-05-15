using Admin.API.Application.DTOs;
using Admin.API.Domain.Entities.Organization;
using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Commands;

// 1. Create Cost Center
public record CreateCostCenterCommand(CreateCostCenterRequest Request) : IRequest<CostCenterResponse>, ITransactionalCommand;

public class CreateCostCenterHandler : IRequestHandler<CreateCostCenterCommand, CostCenterResponse>
{
    private readonly AdminDbContext _db;

    public CreateCostCenterHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<CostCenterResponse> Handle(CreateCostCenterCommand command, CancellationToken ct)
    {
        var code = command.Request.Code.ToUpperInvariant();
        if (await _db.CostCenters.AnyAsync(c => c.Code == code, ct))
            throw new InvalidOperationException($"CostCenter code '{code}' already exists.");

        var legalEntity = await _db.LegalEntities.FindAsync(new object[] { command.Request.LegalEntityId }, ct);
        if (legalEntity == null) throw new KeyNotFoundException($"LegalEntity {command.Request.LegalEntityId} not found");

        var costCenter = CostCenter.Create(
            command.Request.LegalEntityId,
            code,
            command.Request.Name);

        _db.CostCenters.Add(costCenter);

        return new CostCenterResponse(
            costCenter.Id, costCenter.LegalEntityId, legalEntity.Name, costCenter.Code, costCenter.Name, costCenter.IsActive, costCenter.CreatedAt, costCenter.UpdatedAt);
    }
}

// 2. Deactivate Cost Center
public record DeactivateCostCenterCommand(Guid Id) : IRequest<bool>, ITransactionalCommand;

public class DeactivateCostCenterHandler : IRequestHandler<DeactivateCostCenterCommand, bool>
{
    private readonly AdminDbContext _db;

    public DeactivateCostCenterHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<bool> Handle(DeactivateCostCenterCommand command, CancellationToken ct)
    {
        var c = await _db.CostCenters.FindAsync(new object[] { command.Id }, ct);
        if (c == null) return false;

        c.Deactivate();
        return true;
    }
}
