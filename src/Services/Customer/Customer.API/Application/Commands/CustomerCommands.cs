using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Exceptions;
using Customer.API.Application.DTOs;
using Customer.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Customer.API.Application.Commands;

// 1. Create Customer
public record CreateCustomerCommand(CreateCustomerRequest Request) : IRequest<CustomerResponseDto>, ITransactionalCommand;

public class CreateCustomerHandler : IRequestHandler<CreateCustomerCommand, CustomerResponseDto>
{
    private readonly AppDbContext _db;
    private readonly ILogger<CreateCustomerHandler> _logger;

    public CreateCustomerHandler(AppDbContext db, ILogger<CreateCustomerHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CustomerResponseDto> Handle(CreateCustomerCommand command, CancellationToken ct)
    {
        var code = command.Request.Code.Trim().ToUpperInvariant();
        if (await _db.Customers.AnyAsync(c => c.Code == code, ct))
            throw new DomainException(
                ErrorCodes.Customer.CodeAlreadyExists,
                $"Mã khách hàng '{code}' đã tồn tại.",
                new { code });

        var group = await _db.CustomerGroups.FindAsync(new object[] { command.Request.CustomerGroupId }, ct);
        if (group == null)
            throw new NotFoundException(
                ErrorCodes.CustomerGroup.NotFound,
                "Không tìm thấy nhóm khách hàng.",
                new { customerGroupId = command.Request.CustomerGroupId });

        var customer = Domain.Entities.Customer.Create(
            code,
            command.Request.Name,
            command.Request.CustomerGroupId,
            command.Request.TaxCode,
            command.Request.Email,
            command.Request.Phone,
            command.Request.Address);

        _db.Customers.Add(customer);

        _logger.LogInformation("CustomerCreated CustomerId={CustomerId}, Code={Code}", customer.Id, customer.Code);

        return new CustomerResponseDto(
            customer.Id, customer.Code, customer.Name, customer.TaxCode, customer.Email, customer.Phone,
            customer.Address, customer.CustomerGroupId, group.Name, customer.IsActive, customer.CreatedAt, customer.UpdatedAt);
    }
}

// 2. Update Customer
public record UpdateCustomerCommand(Guid Id, UpdateCustomerRequest Request) : IRequest<CustomerResponseDto>, ITransactionalCommand;

public class UpdateCustomerHandler : IRequestHandler<UpdateCustomerCommand, CustomerResponseDto>
{
    private readonly AppDbContext _db;
    private readonly ILogger<UpdateCustomerHandler> _logger;

    public UpdateCustomerHandler(AppDbContext db, ILogger<UpdateCustomerHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CustomerResponseDto> Handle(UpdateCustomerCommand command, CancellationToken ct)
    {
        var customer = await _db.Customers.Include(c => c.CustomerGroup).FirstOrDefaultAsync(c => c.Id == command.Id, ct);
        if (customer == null)
            throw new NotFoundException(ErrorCodes.Customer.NotFound, "Không tìm thấy khách hàng.", new { id = command.Id });

        customer.Update(command.Request.Name, command.Request.TaxCode, command.Request.Email, command.Request.Phone, command.Request.Address);

        _logger.LogInformation("CustomerUpdated CustomerId={CustomerId}", customer.Id);

        return customer.ToDto();
    }
}

// 3. Change Customer Group
public record ChangeCustomerGroupCommand(Guid Id, Guid CustomerGroupId) : IRequest<CustomerResponseDto>, ITransactionalCommand;

public class ChangeCustomerGroupHandler : IRequestHandler<ChangeCustomerGroupCommand, CustomerResponseDto>
{
    private readonly AppDbContext _db;
    private readonly ILogger<ChangeCustomerGroupHandler> _logger;

    public ChangeCustomerGroupHandler(AppDbContext db, ILogger<ChangeCustomerGroupHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CustomerResponseDto> Handle(ChangeCustomerGroupCommand command, CancellationToken ct)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == command.Id, ct);
        if (customer == null)
            throw new NotFoundException(ErrorCodes.Customer.NotFound, "Không tìm thấy khách hàng.", new { id = command.Id });

        var group = await _db.CustomerGroups.FindAsync(new object[] { command.CustomerGroupId }, ct);
        if (group == null)
            throw new NotFoundException(
                ErrorCodes.CustomerGroup.NotFound,
                "Không tìm thấy nhóm khách hàng.",
                new { customerGroupId = command.CustomerGroupId });

        customer.ChangeGroup(command.CustomerGroupId);

        _logger.LogInformation("CustomerGroupChanged CustomerId={CustomerId}, CustomerGroupId={CustomerGroupId}", customer.Id, group.Id);

        return new CustomerResponseDto(
            customer.Id, customer.Code, customer.Name, customer.TaxCode, customer.Email, customer.Phone,
            customer.Address, customer.CustomerGroupId, group.Name, customer.IsActive, customer.CreatedAt, customer.UpdatedAt);
    }
}

// 4. Deactivate Customer
public record DeactivateCustomerCommand(Guid Id) : IRequest<bool>, ITransactionalCommand;

public class DeactivateCustomerHandler : IRequestHandler<DeactivateCustomerCommand, bool>
{
    private readonly AppDbContext _db;
    private readonly ILogger<DeactivateCustomerHandler> _logger;

    public DeactivateCustomerHandler(AppDbContext db, ILogger<DeactivateCustomerHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> Handle(DeactivateCustomerCommand command, CancellationToken ct)
    {
        var customer = await _db.Customers.FindAsync(new object[] { command.Id }, ct);
        if (customer == null) return false;

        customer.Deactivate();
        _logger.LogInformation("CustomerDeactivated CustomerId={CustomerId}", customer.Id);
        return true;
    }
}

// 5. Activate Customer
public record ActivateCustomerCommand(Guid Id) : IRequest<bool>, ITransactionalCommand;

public class ActivateCustomerHandler : IRequestHandler<ActivateCustomerCommand, bool>
{
    private readonly AppDbContext _db;
    private readonly ILogger<ActivateCustomerHandler> _logger;

    public ActivateCustomerHandler(AppDbContext db, ILogger<ActivateCustomerHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> Handle(ActivateCustomerCommand command, CancellationToken ct)
    {
        var customer = await _db.Customers.FindAsync(new object[] { command.Id }, ct);
        if (customer == null) return false;

        customer.Activate();
        _logger.LogInformation("CustomerActivated CustomerId={CustomerId}", customer.Id);
        return true;
    }
}
