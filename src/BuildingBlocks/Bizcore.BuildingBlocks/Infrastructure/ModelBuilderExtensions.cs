using Bizcore.BuildingBlocks.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Bizcore.BuildingBlocks.Infrastructure
{
    public static class ModelBuilderExtensions
    {
        /// <summary>
        /// Configures all entities inheriting from BaseEntity to use Version as a concurrency token.
        /// </summary>
        public static void ApplyBaseEntityConfiguration(this ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(AggregateRoot).IsAssignableFrom(entityType.ClrType))
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .Property(nameof(BaseEntity.Version))
                        .IsConcurrencyToken();
                }
            }
        }
    }
}
