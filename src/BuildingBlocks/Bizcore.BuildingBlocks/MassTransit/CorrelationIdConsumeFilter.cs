using MassTransit;

namespace Bizcore.BuildingBlocks.MassTransit
{
    public class CorrelationIdConsumeFilter<T> : IFilter<ConsumeContext<T>>
        where T : class
    {
        private const string CorrelationIdHeader = "X-Correlation-ID";

        public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
        {
            var correlationId = context.Headers.Get<string>(CorrelationIdHeader);

            if (string.IsNullOrEmpty(correlationId))
            {
                correlationId = Guid.NewGuid().ToString();
            }

            using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
            {
                await next.Send(context);
            }
        }

        public void Probe(ProbeContext context) =>
            context.CreateFilterScope("correlationIdConsume");
    }
}
