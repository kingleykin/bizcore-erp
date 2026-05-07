namespace Bizcore.BuildingBlocks.Interfaces
{
    /// <summary>
    /// Marker interface. Entities implementing this will have their changes
    /// automatically captured by the AuditSaveChangesInterceptor (field-level audit).
    /// </summary>
    public interface IAuditable { }
}
