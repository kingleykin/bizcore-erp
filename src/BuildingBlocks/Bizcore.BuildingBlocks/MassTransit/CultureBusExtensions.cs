using MassTransit;
using Bizcore.BuildingBlocks.MassTransit.Filters;

namespace Bizcore.BuildingBlocks.MassTransit
{
    public static class CultureBusExtensions
    {
        public static void UseCulture(
            this IBusFactoryConfigurator cfg,
            IBusRegistrationContext context)
        {
            // Publish filter: gắn Culture từ hiện tại vào message header
            cfg.UsePublishFilter(typeof(CulturePublishFilter<>), context);

            // Consume filter: đọc Culture từ message header → set CultureInfo.CurrentCulture
            cfg.UseConsumeFilter(typeof(CultureConsumeFilter<>), context);
        }
    }
}
