using Admin.API.Application.DTOs;
using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Commands;

// 1. Update Setting
public record UpdateSettingCommand(string Key, UpdateSettingRequest Request) : IRequest<GlobalSettingResponse>, ITransactionalCommand;

public class UpdateSettingHandler : IRequestHandler<UpdateSettingCommand, GlobalSettingResponse>
{
    private readonly AdminDbContext _db;

    public UpdateSettingHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<GlobalSettingResponse> Handle(UpdateSettingCommand command, CancellationToken ct)
    {
        var setting = await _db.GlobalSettings.FindAsync(new object[] { command.Key }, ct);
        if (setting == null) throw new KeyNotFoundException($"Setting '{command.Key}' not found.");

        setting.UpdateValue(command.Request.SettingValue);

        return new GlobalSettingResponse(setting.SettingKey, setting.SettingValue, setting.Description, setting.IsReadOnly, setting.UpdatedAt);
    }
}
