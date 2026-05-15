using Admin.API.Application.DTOs;
using Admin.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Queries;

// 1. Get All Currencies
public record GetCurrenciesQuery(bool ActiveOnly = true) : IRequest<IEnumerable<CurrencyResponse>>;

public class GetCurrenciesHandler : IRequestHandler<GetCurrenciesQuery, IEnumerable<CurrencyResponse>>
{
    private readonly AdminDbContext _db;

    public GetCurrenciesHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<CurrencyResponse>> Handle(GetCurrenciesQuery request, CancellationToken ct)
    {
        var query = _db.Currencies.AsNoTracking();
        
        if (request.ActiveOnly)
            query = query.Where(c => c.IsActive);

        return await query
            .Select(c => new CurrencyResponse(c.Code, c.Name, c.Symbol, c.DecimalPlaces, c.IsActive))
            .ToListAsync(ct);
    }
}

// 2. Get Currency By Code
public record GetCurrencyByCodeQuery(string Code) : IRequest<CurrencyResponse?>;

public class GetCurrencyByCodeHandler : IRequestHandler<GetCurrencyByCodeQuery, CurrencyResponse?>
{
    private readonly AdminDbContext _db;

    public GetCurrencyByCodeHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<CurrencyResponse?> Handle(GetCurrencyByCodeQuery request, CancellationToken ct)
    {
        var c = await _db.Currencies
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == request.Code.ToUpperInvariant(), ct);

        if (c == null) return null;

        return new CurrencyResponse(c.Code, c.Name, c.Symbol, c.DecimalPlaces, c.IsActive);
    }
}
