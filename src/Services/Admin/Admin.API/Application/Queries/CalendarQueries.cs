using Admin.API.Application.DTOs;
using Admin.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Queries;

// 1. Get Calendar By Year
public record GetCalendarQuery(int Year) : IRequest<IEnumerable<SystemCalendarResponse>>;

public class GetCalendarHandler : IRequestHandler<GetCalendarQuery, IEnumerable<SystemCalendarResponse>>
{
    private readonly AdminDbContext _db;

    public GetCalendarHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<SystemCalendarResponse>> Handle(GetCalendarQuery request, CancellationToken ct)
    {
        return await _db.SystemCalendars
            .AsNoTracking()
            .Where(c => c.Date.Year == request.Year)
            .OrderBy(c => c.Date)
            .Select(c => new SystemCalendarResponse(c.Date, c.IsWorkingDay, c.HolidayName))
            .ToListAsync(ct);
    }
}
