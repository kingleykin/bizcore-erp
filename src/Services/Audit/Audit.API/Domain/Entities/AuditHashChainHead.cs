using System.ComponentModel.DataAnnotations;

namespace Audit.API.Domain.Entities
{
    public class AuditHashChainHead : Bizcore.BuildingBlocks.Abstractions.BaseEntity
    {

        [Required]
        [MaxLength(200)]
        public string PartitionKey { get; set; } = string.Empty;

        public long Sequence { get; set; }

        [Required]
        [MaxLength(64)]
        public string CurrentHash { get; set; } = string.Empty;

    }
}
