using Admin.API.Application.DTOs;
using Admin.API.Domain.Entities.Settings;
using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Commands;

// 1. Upsert Calendar Day
public record UpsertCalendarDayCommand(UpsertCalendarRequest Request) : IRequest<SystemCalendarResponse>, ITransactionalCommand;

public class UpsertCalendarDayHandler : IRequestHandler<UpsertCalendarDayCommand, SystemCalendarResponse>
{
    private readonly AdminDbContext _db;

    public UpsertCalendarDayHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<SystemCalendarResponse> Handle(UpsertCalendarDayCommand command, CancellationToken ct)
    {
        var date = command.Request.Date.Date;
        var day = await _db.SystemCalendars.FindAsync(new object[] { date }, ct);

        if (day == null)
        {
            day = SystemCalendar.Create(date, command.Request.IsWorkingDay, command.Request.HolidayName);
            _db.SystemCalendars.Add(day);
        }
        else
        {
            day.Update(command.Request.IsWorkingDay, command.Request.HolidayName);
        }
        return new SystemCalendarResponse(day.Date, day.IsWorkingDay, day.HolidayName);
    }
}
