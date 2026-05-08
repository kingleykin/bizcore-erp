using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Bizcore.BuildingBlocks.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs request execution time and details.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var typeName = request.GetType().Name;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Handling {CommandName}", typeName);

        try
        {
            var response = await next();

            stopwatch.Stop();
            _logger.LogInformation(
                "Handled {CommandName} in {ElapsedMilliseconds}ms",
                typeName,
                stopwatch.ElapsedMilliseconds
            );

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Error handling {CommandName} after {ElapsedMilliseconds}ms",
                typeName,
                stopwatch.ElapsedMilliseconds
            );

            throw;
        }
    }
}
