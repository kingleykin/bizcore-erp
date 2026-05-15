using Bizcore.BuildingBlocks.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bizcore.BuildingBlocks.Interceptors;

public class EntityVersionInterceptor : SaveChangesInterceptor
{
    private static readonly HashSet<string> IgnoredFields = new()
    {
        nameof(BaseEntity.Version),
        nameof(BaseEntity.UpdatedAt)
    };

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateVersions(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateVersions(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void UpdateVersions(DbContext? context)
    {
        if (context == null)
            return;

        var entries = context.ChangeTracker
            .Entries<AggregateRoot>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(BaseEntity.Version)).CurrentValue = 1L;
                continue;
            }

            if (entry.State != EntityState.Modified)
                continue;

            var hasBusinessChanges = entry.Properties.Any(p =>
                p.IsModified &&
                !IgnoredFields.Contains(p.Metadata.Name));

            var aggregate = (AggregateRoot)entry.Entity;

            if (!hasBusinessChanges && !aggregate.IsStateChanged)
                continue;

            var versionProperty = entry.Property(nameof(BaseEntity.Version));

            var originalVersion = (long)(versionProperty.OriginalValue ?? 0L);

            // IMPORTANT: Use OriginalValue + 1 (not CurrentValue++)
            // EF generates: WHERE Version = OriginalValue, SET Version = CurrentValue
            versionProperty.CurrentValue = originalVersion + 1;

            aggregate.ClearStateChanged();
        }
    }
}