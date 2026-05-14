using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Audit.API.Infrastructure.Data.Extensions
{
    public static class ModelBuilderExtensions
    {
        public static void ConfigureMassTransitOutbox(this ModelBuilder modelBuilder)
        {
            modelBuilder.AddInboxStateEntity();
            modelBuilder.AddOutboxMessageEntity();
            modelBuilder.AddOutboxStateEntity();
        }
    }
}
