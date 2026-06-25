using Bizcore.BuildingBlocks.Contracts;
using Bizcore.BuildingBlocks.Grpc;
using Bizcore.BuildingBlocks.Infrastructure;
using Bizcore.BuildingBlocks.MassTransit;
using Bizcore.BuildingBlocks.Messaging;
using Bizcore.BuildingBlocks.Behaviors;
using Bizcore.BuildingBlocks.Abstractions;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Orchestration.API.Application.Sagas;
using Orchestration.API.Application.Services;
using Orchestration.API.Domain.Entities;
using Orchestration.API.Infrastructure.Data;

namespace Orchestration.API;

public class OrchestrationModule : IServiceModule
{
    public void RegisterServices(IServiceCollection services, WebApplicationBuilder builder)
    {
        // 1. Database
        var connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
        DatabaseExtensions.PreCreateDatabase(connStr);
        services.AddBizcoreDbContext<AppDbContext>(connStr);
        services.AddScoped<IUnitOfWork, OrchestrationUnitOfWork>();

        // 2. MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(OrchestrationModule).Assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
        });

        // 3. Application Services
        services.AddScoped<IAuditClientService, AuditClientService>();

        // 4. gRPC Client
        services.AddBizcoreGrpcClient<Bizcore.BuildingBlocks.Grpc.Protos.AuditGrpc.AuditGrpcClient>(
            builder.Configuration, "Audit");

        // 5. MassTransit with Saga
        services.AddBizcoreMassTransit<AppDbContext>(
            builder.Configuration,
            QueueNames.OrchestrationService,
            x =>
            {
                x.AddSagaStateMachine<PaymentSaga, PaymentSagaState>()
                    .EntityFrameworkRepository(r =>
                    {
                        r.ConcurrencyMode = ConcurrencyMode.Pessimistic;
                        r.ExistingDbContext<AppDbContext>();
                    });

                x.MapBusinessCommand<IValidateInvoiceCommand>(QueueNames.InvoiceService);
                x.MapBusinessCommand<IConfirmPaymentCommand>(QueueNames.PaymentService);
                x.MapBusinessCommand<IRejectPaymentCommand>(QueueNames.PaymentService);
                x.MapBusinessCommand<IAddCustomerPointCommand>(QueueNames.CustomerService);
                x.MapBusinessCommand<IDeductCustomerBalanceCommand>(QueueNames.CustomerService);
                x.MapBusinessCommand<IRefundCustomerBalanceCommand>(QueueNames.CustomerService);
                x.MapBusinessCommand<IRefundPaymentCommand>(QueueNames.PaymentService);
                x.MapBusinessCommand<IRevertInvoicePaymentCommand>(QueueNames.InvoiceService);
            });
    }
}
