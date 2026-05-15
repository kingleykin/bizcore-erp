using System.ComponentModel.DataAnnotations.Schema;

namespace Bizcore.BuildingBlocks.Abstractions
{
    /// <summary>
    /// Marker interface for Aggregate Roots.
    /// </summary>
    public interface IAggregateRoot
    {
    }

    /// <summary>
    /// Base class for Aggregate Roots.
    /// Encapsulates state change tracking for optimistic concurrency.
    /// </summary>
    public abstract class AggregateRoot : BaseEntity, IAggregateRoot
    {
        /// <summary>
        /// Indicates if the aggregate state has changed in a way that requires a version increment.
        /// This flag is NOT mapped to the database.
        /// </summary>
        [NotMapped]
        public bool IsStateChanged { get; private set; }

        /// <summary>
        /// Marks the aggregate as changed. Call this in business methods that mutate the aggregate state.
        /// </summary>
        protected void MarkStateChanged()
        {
            IsStateChanged = true;
        }

        public void ClearStateChanged()
        {
            IsStateChanged = false;
        }

        /// <summary>
        /// Consumes the state change flag. Used by the infrastructure (Interceptor) to determine if 
        /// the version should be incremented.
        /// </summary>
        internal bool ConsumeStateChanged()
        {
            var changed = IsStateChanged;
            IsStateChanged = false;
            return changed;
        }
    }
}
