using Grpc.Core;
using Bizcore.BuildingBlocks.Exceptions;

namespace Bizcore.BuildingBlocks.Grpc
{
    public static class GrpcErrorMapper
    {
        /// <summary>
        /// Maps gRPC RpcException to standard Domain/Infrastructure exceptions.
        /// Use this in your gRPC Query Service implementations.
        /// </summary>
        public static Exception MapToDomainException(RpcException ex, string serviceName)
        {
            return ex.StatusCode switch
            {
                StatusCode.NotFound => new NotFoundException($"{serviceName} resource not found.", ex.Message),
                StatusCode.Unauthenticated => new UnauthorizedException("Session expired or invalid."),
                StatusCode.PermissionDenied => new UnauthorizedException("Permission denied for this operation."),
                StatusCode.InvalidArgument => new DomainException($"Invalid data sent to {serviceName}: {ex.Status.Detail}"),
                StatusCode.DeadlineExceeded => new DomainException($"{serviceName} call timed out."),
                StatusCode.Unavailable => new ServiceUnavailableException($"{serviceName} is currently unavailable (Circuit Breaker may be open)."),
                _ => new DomainException($"{serviceName} error: {ex.Status.Detail}", ex)
            };
        }
    }
}
