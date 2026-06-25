using Bizcore.BuildingBlocks.Contracts;
using Customer.API.Infrastructure.Data;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Customer.API.Application.Commands;

public class AddCustomerPointCommandHandler : IRequestHandler<AddCustomerPointCommand, bool>
{
    private readonly CustomerDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly Bizcore.BuildingBlocks.Audit.IAuditPublisher _audit;
    private readonly ILogger<AddCustomerPointCommandHandler> _logger;

    public AddCustomerPointCommandHandler(
        CustomerDbContext context,
        IPublishEndpoint publishEndpoint,
        Bizcore.BuildingBlocks.Audit.IAuditPublisher audit,
        ILogger<AddCustomerPointCommandHandler> logger)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
        _audit = audit;
        _logger = logger;
    }

    public async Task<bool> Handle(AddCustomerPointCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing customer point addition. PaymentId={PaymentId}, CustomerId={CustomerId}, Amount={Amount}",
                request.PaymentId, request.CustomerId, request.Amount);

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);

            if (customer == null)
            {
                _logger.LogWarning("Customer not found for point addition. CustomerId={CustomerId}", request.CustomerId);
                return false;
            }

            var beforeState = new { customer.CustomerPoint };

            // Calculate points based on Amount (1 point for every 10 units of currency, minimum 1 point)
            int pointsAdded = request.Amount > 0 ? Math.Max(1, (int)(request.Amount / 10)) : 0;

            if (pointsAdded < 10)
            {
                throw new ArgumentException("Points to add must be at least 1.");
            }


            customer.AddPoints(pointsAdded);

            // Publish CustomerPointAddedEvent to notify Saga Orchestrator
            await _publishEndpoint.Publish<ICustomerPointAddedEvent>(new
            {
                PaymentId = request.PaymentId,
                CustomerId = request.CustomerId,
                Points = pointsAdded
            }, cancellationToken);

            var afterState = new { customer.CustomerPoint };
            
            await _audit.PublishAsync(
                "CustomerPointAdded",
                entityType: "Customer",
                entityId: customer.Id.ToString(),
                before: beforeState,
                after: afterState,
                category: Bizcore.BuildingBlocks.Audit.AuditCategory.System,
                classification: Bizcore.BuildingBlocks.Audit.DataClassification.Financial,
                ct: cancellationToken);

            _logger.LogInformation("Successfully added {Points} points to customer. CustomerId={CustomerId}, PaymentId={PaymentId}",
                pointsAdded, request.CustomerId, request.PaymentId);

            return true;
        }

        catch (Exception ex)
        {
            await _publishEndpoint.Publish<ICustomerPointAdditionFailedEvent>(new
            {
                PaymentId = request.PaymentId,
                CustomerId = request.CustomerId,
                Reason = ex.Message
            }, cancellationToken);

            _logger.LogError(ex, "Error adding customer points. PaymentId={PaymentId}, CustomerId={CustomerId}, Amount={Amount}",
                request.PaymentId, request.CustomerId, request.Amount);
            return false;
        }
    }
}