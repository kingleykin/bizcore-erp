using Customer.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Bizcore.BuildingBlocks;
using Customer.API.Domain.Entities;

namespace Customer.API.Application.Commands.CustomerGroup;

public class DeleteCustomerGroupCommandHandler : IRequestHandler<DeleteCustomerGroupCommand, bool>
{
    private readonly CustomerDbContext _context;

    public DeleteCustomerGroupCommandHandler(CustomerDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(DeleteCustomerGroupCommand request, CancellationToken cancellationToken)
    {
        var customerGroup = await _context.CustomerGroups.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (customerGroup == null) return false;

        customerGroup.UpdateStatus(CustomerGroupStatus.Blocked);

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
