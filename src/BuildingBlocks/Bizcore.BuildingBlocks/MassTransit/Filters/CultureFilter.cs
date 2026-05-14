using MassTransit;
using System.Globalization;

namespace Bizcore.BuildingBlocks.MassTransit.Filters
{
    public class CulturePublishFilter<T> : IFilter<PublishContext<T>> where T : class
    {
        public void Probe(ProbeContext context) => context.CreateFilterScope("culture");

        public async Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
        {
            var culture = CultureInfo.CurrentCulture.Name;
            var uiCulture = CultureInfo.CurrentUICulture.Name;

            context.Headers.Set("X-Culture", culture);
            context.Headers.Set("X-UI-Culture", uiCulture);

            await next.Send(context);
        }
    }

    public class CultureConsumeFilter<T> : IFilter<ConsumeContext<T>> where T : class
    {
        public void Probe(ProbeContext context) => context.CreateFilterScope("culture");

        public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
        {
            var cultureName = context.Headers.Get<string>("X-Culture");
            var uiCultureName = context.Headers.Get<string>("X-UI-Culture");

            if (!string.IsNullOrEmpty(cultureName))
            {
                var culture = new CultureInfo(cultureName);
                CultureInfo.CurrentCulture = culture;
            }

            if (!string.IsNullOrEmpty(uiCultureName))
            {
                var uiCulture = new CultureInfo(uiCultureName);
                CultureInfo.CurrentUICulture = uiCulture;
            }

            await next.Send(context);
        }
    }
}
