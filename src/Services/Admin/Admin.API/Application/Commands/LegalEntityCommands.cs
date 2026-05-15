using Admin.API.Application.DTOs;
using Admin.API.Domain.Entities.Organization;
using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Commands;

// 1. Create Legal Entity
public record CreateLegalEntityCommand(CreateLegalEntityRequest Request) : IRequest<LegalEntityResponse>, ITransactionalCommand;

public class CreateLegalEntityHandler : IRequestHandler<CreateLegalEntityCommand, LegalEntityResponse>
{
    private readonly AdminDbContext _db;

    public CreateLegalEntityHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<LegalEntityResponse> Handle(CreateLegalEntityCommand command, CancellationToken ct)
    {
        var code = command.Request.Code.ToUpperInvariant();
        if (await _db.LegalEntities.AnyAsync(e => e.Code == code, ct))
            throw new InvalidOperationException($"LegalEntity code '{code}' already exists.");

        var entity = LegalEntity.Create(
            code,
            command.Request.Name,
            command.Request.TaxCode,
            command.Request.RegistrationNumber,
            command.Request.Address,
            command.Request.BaseCurrencyCode);

        _db.LegalEntities.Add(entity);

        return new LegalEntityResponse(
            entity.Id, entity.Code, entity.Name, entity.TaxCode, entity.RegistrationNumber, entity.Address, entity.BaseCurrencyCode, (int)entity.Status, entity.CreatedAt, entity.UpdatedAt);
    }
}

// 2. Update Legal Entity
public record UpdateLegalEntityCommand(Guid Id, UpdateLegalEntityRequest Request) : IRequest<LegalEntityResponse>, ITransactionalCommand;

public class UpdateLegalEntityHandler : IRequestHandler<UpdateLegalEntityCommand, LegalEntityResponse>
{
    private readonly AdminDbContext _db;

    public UpdateLegalEntityHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<LegalEntityResponse> Handle(UpdateLegalEntityCommand command, CancellationToken ct)
    {
        var entity = await _db.LegalEntities.FindAsync(new object[] { command.Id }, ct);
        if (entity == null) throw new KeyNotFoundException($"LegalEntity {command.Id} not found");

        entity.Update(
            command.Request.Name,
            command.Request.TaxCode,
            command.Request.RegistrationNumber,
            command.Request.Address,
            command.Request.BaseCurrencyCode ?? entity.BaseCurrencyCode);

        return new LegalEntityResponse(
            entity.Id, entity.Code, entity.Name, entity.TaxCode, entity.RegistrationNumber, entity.Address, entity.BaseCurrencyCode, (int)entity.Status, entity.CreatedAt, entity.UpdatedAt);
    }
}

// 3. Deactivate Legal Entity
public record DeactivateLegalEntityCommand(Guid Id) : IRequest<bool>, ITransactionalCommand;

public class DeactivateLegalEntityHandler : IRequestHandler<DeactivateLegalEntityCommand, bool>
{
    private readonly AdminDbContext _db;

    public DeactivateLegalEntityHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<bool> Handle(DeactivateLegalEntityCommand command, CancellationToken ct)
    {
        var entity = await _db.LegalEntities.FindAsync(new object[] { command.Id }, ct);
        if (entity == null) return false;

        entity.Deactivate();
        return true;
    }
}
