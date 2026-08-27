using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using MediatR;
using Audit.API.Application.Grpc; // To load the assembly
using Invoice.API.Application.Commands; // To load the assembly

namespace Bizcore.UnitTests.Architecture
{
    public class GrpcArchitectureTests
    {
        private static readonly ArchUnitNET.Domain.Architecture Architecture = new ArchLoader()
            .LoadAssemblies(
                typeof(AuditGrpcService).Assembly,
                typeof(UpdateInvoiceStatusCommandHandler).Assembly,
                typeof(Bizcore.BuildingBlocks.Infrastructure.ServiceDefaults).Assembly
            ).Build();

        [Fact]
        public void GrpcServices_ShouldNotDependOn_CommandHandlers()
        {
            var grpcServices = Classes().That().HaveNameEndingWith("GrpcService");
            var commandHandlers = Classes().That().ImplementInterface(typeof(IRequestHandler<,>));
            var commandNamespace = "Application.Commands";

            IArchRule rule = grpcServices
                .Should()
                .NotDependOnAny(commandHandlers)
                .AndShould()
                .NotDependOnAny(Classes().That().ResideInNamespace(commandNamespace))
                .Because("gRPC services should only be used for internal queries (Read-side), not for executing commands.");

            rule.Check(Architecture);
        }

        [Fact]
        public void GrpcServices_ShouldResideIn_GrpcNamespace()
        {
            IArchRule rule = Classes()
                .That().HaveNameEndingWith("GrpcService")
                .Should().FollowCustomCondition(c => c.Namespace.FullName.Contains("Grpc"), "reside in a namespace containing 'Grpc'", "does not reside in a namespace containing 'Grpc'")
                .Because("gRPC service implementations should be organized within a Grpc namespace for clarity.");

            rule.Check(Architecture);
        }
    }
}
