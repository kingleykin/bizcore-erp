using System;

namespace Bizcore.BuildingBlocks.Abstractions
{
    /// <summary>
    /// Base class for all domain entities in the system.
    /// Provides common properties like Id, CreatedAt, and UpdatedAt.
    /// </summary>
    public abstract class BaseEntity
    {
        public Guid Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public long Version { get; set; }

        protected BaseEntity()
        {
            // Default initialization
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            Version = 1;
        }
        
        /// <summary>
        /// Updates the UpdatedAt timestamp and increments the Version.
        /// Call this method whenever the entity state changes.
        /// </summary>
        public virtual void UpdateState()
        {
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
