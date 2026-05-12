using Bizcore.BuildingBlocks.Contracts;
using Bizcore.BuildingBlocks.Grpc;
using Bizcore.BuildingBlocks.Infrastructure;
using Bizcore.BuildingBlocks.MassTransit;
using Bizcore.BuildingBlocks.Messaging;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Orchestration.API.Application.Sagas;
using Orchestration.API.Application.Services;
using Orchestration.API.Infrastructure.Data;

namespace Orchestration.API
{
    public class OrchestrationModule : IServiceModule
    {
        public void RegisterServices(IServiceCollection services, WebApplicationBuilder builder)
        {
            // 1. Database
            var connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
            DatabaseExtensions.PreCreateDatabase(connStr);
            services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connStr));

            // 2. Application Services
            services.AddScoped<IProcessOrchestrationService, ProcessOrchestrationService>();
            services.AddScoped<IAuditClientService, AuditClientService>();

            // 3. gRPC Client (Centralized registration)
            services.AddBizcoreGrpcClient<Bizcore.BuildingBlocks.Grpc.Protos.AuditGrpc.AuditGrpcClient>(
                builder.Configuration, "Audit");

            // 4. MassTransit with Saga and Command Mappings
            services.AddBizcoreMassTransit<AppDbContext>(
                builder.Configuration,
                QueueNames.OrchestrationService,
                x =>
                {
                    x.AddQuartz();
                    x.AddQuartzConsumers();

                    // Saga orchestrator
                    x.AddSagaStateMachine<PaymentSaga, PaymentSagaState>()
                        .EntityFrameworkRepository(r =>
                        {
                            r.ConcurrencyMode = ConcurrencyMode.Pessimistic;
                            r.ExistingDbContext<AppDbContext>();
                        });

                    // Command Mappings (Sender Topology)
                    x.MapBusinessCommand<IValidateInvoiceCommand>(QueueNames.InvoiceService);
                    x.MapBusinessCommand<IConfirmPaymentCommand>(QueueNames.PaymentService);
                    x.MapBusinessCommand<IRejectPaymentCommand>(QueueNames.PaymentService);
                });
        }
    }
}
