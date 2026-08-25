using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Exceptions;
using Customer.API.Application.DTOs;
using Customer.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Customer.API.Application.Commands;

// 1. Create CustomerGroup
public record CreateCustomerGroupCommand(CreateCustomerGroupRequest Request) : IRequest<CustomerGroupResponseDto>, ITransactionalCommand;

public class CreateCustomerGroupHandler : IRequestHandler<CreateCustomerGroupCommand, CustomerGroupResponseDto>
{
    private readonly AppDbContext _db;
    private readonly ILogger<CreateCustomerGroupHandler> _logger;

    public CreateCustomerGroupHandler(AppDbContext db, ILogger<CreateCustomerGroupHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CustomerGroupResponseDto> Handle(CreateCustomerGroupCommand command, CancellationToken ct)
    {
        var code = command.Request.Code.Trim().ToUpperInvariant();
        if (await _db.CustomerGroups.AnyAsync(g => g.Code == code, ct))
            throw new DomainException(
                ErrorCodes.CustomerGroup.CodeAlreadyExists,
                $"Mã nhóm khách hàng '{code}' đã tồn tại.",
                new { code });

        var group = Domain.Entities.CustomerGroup.Create(code, command.Request.Name, command.Request.Description);
        _db.CustomerGroups.Add(group);

        _logger.LogInformation("CustomerGroupCreated CustomerGroupId={CustomerGroupId}, Code={Code}", group.Id, group.Code);

        return group.ToDto();
    }
}

// 2. Update CustomerGroup
public record UpdateCustomerGroupCommand(Guid Id, UpdateCustomerGroupRequest Request) : IRequest<CustomerGroupResponseDto>, ITransactionalCommand;

public class UpdateCustomerGroupHandler : IRequestHandler<UpdateCustomerGroupCommand, CustomerGroupResponseDto>
{
    private readonly AppDbContext _db;
    private readonly ILogger<UpdateCustomerGroupHandler> _logger;

    public UpdateCustomerGroupHandler(AppDbContext db, ILogger<UpdateCustomerGroupHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CustomerGroupResponseDto> Handle(UpdateCustomerGroupCommand command, CancellationToken ct)
    {
        var group = await _db.CustomerGroups.FirstOrDefaultAsync(g => g.Id == command.Id, ct);
        if (group == null)
            throw new NotFoundException(ErrorCodes.CustomerGroup.NotFound, "Không tìm thấy nhóm khách hàng.", new { id = command.Id });

        group.Update(command.Request.Name, command.Request.Description);

        _logger.LogInformation("CustomerGroupUpdated CustomerGroupId={CustomerGroupId}", group.Id);

        return group.ToDto();
    }
}

// 3. Deactivate CustomerGroup
public record DeactivateCustomerGroupCommand(Guid Id) : IRequest<bool>, ITransactionalCommand;

public class DeactivateCustomerGroupHandler : IRequestHandler<DeactivateCustomerGroupCommand, bool>
{
    private readonly AppDbContext _db;
    private readonly ILogger<DeactivateCustomerGroupHandler> _logger;

    public DeactivateCustomerGroupHandler(AppDbContext db, ILogger<DeactivateCustomerGroupHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> Handle(DeactivateCustomerGroupCommand command, CancellationToken ct)
    {
        var group = await _db.CustomerGroups.FindAsync(new object[] { command.Id }, ct);
        if (group == null) return false;

        group.Deactivate();
        _logger.LogInformation("CustomerGroupDeactivated CustomerGroupId={CustomerGroupId}", group.Id);
        return true;
    }
}

// 4. Activate CustomerGroup
public record ActivateCustomerGroupCommand(Guid Id) : IRequest<bool>, ITransactionalCommand;

public class ActivateCustomerGroupHandler : IRequestHandler<ActivateCustomerGroupCommand, bool>
{
    private readonly AppDbContext _db;
    private readonly ILogger<ActivateCustomerGroupHandler> _logger;

    public ActivateCustomerGroupHandler(AppDbContext db, ILogger<ActivateCustomerGroupHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> Handle(ActivateCustomerGroupCommand command, CancellationToken ct)
    {
        var group = await _db.CustomerGroups.FindAsync(new object[] { command.Id }, ct);
        if (group == null) return false;

        group.Activate();
        _logger.LogInformation("CustomerGroupActivated CustomerGroupId={CustomerGroupId}", group.Id);
        return true;
    }
}
