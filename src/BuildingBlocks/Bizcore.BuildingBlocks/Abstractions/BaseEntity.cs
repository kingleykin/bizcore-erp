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
        public byte[] RowVersion { get; set; } = null!;

        protected BaseEntity()
        {
            // Default initialization
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            RowVersion = Array.Empty<byte>();
        }
        
        /// <summary>
        /// Updates the UpdatedAt timestamp to the current UTC time.
        /// Call this method whenever the entity state changes.
        /// </summary>
        public virtual void UpdateTimestamp()
        {
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
