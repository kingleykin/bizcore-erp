namespace Bizcore.BuildingBlocks.Abstractions;

/// <summary>
/// Marker interface for commands that require transaction management.
/// Commands implementing this interface will automatically be wrapped in a transaction
/// by the TransactionBehavior pipeline.
/// </summary>
public interface ITransactionalCommand
{
}
