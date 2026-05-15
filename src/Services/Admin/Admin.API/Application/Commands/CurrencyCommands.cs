using Admin.API.Application.DTOs;
using Admin.API.Domain.Entities;
using Admin.API.Domain.Entities.Settings;
using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Commands;

// 1. Create Currency
public record CreateCurrencyCommand(CreateCurrencyRequest Request) : IRequest<CurrencyResponse>, ITransactionalCommand;

public class CreateCurrencyHandler : IRequestHandler<CreateCurrencyCommand, CurrencyResponse>
{
    private readonly AdminDbContext _db;

    public CreateCurrencyHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<CurrencyResponse> Handle(CreateCurrencyCommand command, CancellationToken ct)
    {
        var code = command.Request.Code.ToUpperInvariant();
        if (await _db.Currencies.AnyAsync(c => c.Code == code, ct))
            throw new InvalidOperationException($"Currency '{code}' already exists.");

        var currency = Currency.Create(code, command.Request.Name, command.Request.Symbol, command.Request.DecimalPlaces);
        _db.Currencies.Add(currency);

        return new CurrencyResponse(currency.Code, currency.Name, currency.Symbol, currency.DecimalPlaces, currency.IsActive);
    }
}

// 2. Deactivate Currency
public record DeactivateCurrencyCommand(string Code) : IRequest<bool>, ITransactionalCommand;

public class DeactivateCurrencyHandler : IRequestHandler<DeactivateCurrencyCommand, bool>
{
    private readonly AdminDbContext _db;

    public DeactivateCurrencyHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<bool> Handle(DeactivateCurrencyCommand command, CancellationToken ct)
    {
        var currency = await _db.Currencies.FindAsync(new object[] { command.Code.ToUpperInvariant() }, ct);
        if (currency == null) return false;

        currency.Deactivate();
        return true;
    }
}
