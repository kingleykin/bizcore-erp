using Admin.API.Application.DTOs;
using Admin.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Queries;

// 1. Get All Settings
public record GetSettingsQuery : IRequest<IEnumerable<GlobalSettingResponse>>;

public class GetSettingsHandler : IRequestHandler<GetSettingsQuery, IEnumerable<GlobalSettingResponse>>
{
    private readonly AdminDbContext _db;

    public GetSettingsHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<GlobalSettingResponse>> Handle(GetSettingsQuery request, CancellationToken ct)
    {
        return await _db.GlobalSettings
            .AsNoTracking()
            .OrderBy(s => s.SettingKey)
            .Select(s => new GlobalSettingResponse(s.SettingKey, s.SettingValue, s.Description, s.IsReadOnly, s.UpdatedAt))
            .ToListAsync(ct);
    }
}

// 2. Get Setting By Key
public record GetSettingByKeyQuery(string Key) : IRequest<GlobalSettingResponse?>;

public class GetSettingByKeyHandler : IRequestHandler<GetSettingByKeyQuery, GlobalSettingResponse?>
{
    private readonly AdminDbContext _db;

    public GetSettingByKeyHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<GlobalSettingResponse?> Handle(GetSettingByKeyQuery request, CancellationToken ct)
    {
        var s = await _db.GlobalSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SettingKey == request.Key, ct);

        if (s == null) return null;

        return new GlobalSettingResponse(s.SettingKey, s.SettingValue, s.Description, s.IsReadOnly, s.UpdatedAt);
    }
}
