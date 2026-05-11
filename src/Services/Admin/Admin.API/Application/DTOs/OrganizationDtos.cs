using System.ComponentModel.DataAnnotations;

namespace Admin.API.Application.DTOs
{
    // ── LegalEntity ────────────────────────────────────────────────────────────

    public record LegalEntityResponse(
        Guid    Id,
        string  Code,
        string  Name,
        string? TaxCode,
        string? RegistrationNumber,
        string? Address,
        string? BaseCurrencyCode,
        int     Status,
        DateTime CreatedAt,
        DateTime UpdatedAt
    );

    public record CreateLegalEntityRequest
    {
        [Required, MaxLength(50)]
        public string Code { get; init; } = null!;

        [Required, MaxLength(255)]
        public string Name { get; init; } = null!;

        [MaxLength(50)]
        public string? TaxCode { get; init; }

        [MaxLength(50)]
        public string? RegistrationNumber { get; init; }

        [MaxLength(500)]
        public string? Address { get; init; }

        [MaxLength(3)]
        public string BaseCurrencyCode { get; init; } = "VND";
    }

    public record UpdateLegalEntityRequest
    {
        [Required, MaxLength(255)]
        public string Name { get; init; } = null!;

        [MaxLength(50)]
        public string? TaxCode { get; init; }

        [MaxLength(50)]
        public string? RegistrationNumber { get; init; }

        [MaxLength(500)]
        public string? Address { get; init; }

        [MaxLength(3)]
        public string? BaseCurrencyCode { get; init; }
    }

    // ── Branch ─────────────────────────────────────────────────────────────────

    public record BranchResponse(
        Guid    Id,
        Guid    LegalEntityId,
        string  LegalEntityName,
        string  Code,
        string  Name,
        string? Address,
        bool    IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt
    );

    public record CreateBranchRequest
    {
        [Required]
        public Guid LegalEntityId { get; init; }

        [Required, MaxLength(50)]
        public string Code { get; init; } = null!;

        [Required, MaxLength(255)]
        public string Name { get; init; } = null!;

        [MaxLength(500)]
        public string? Address { get; init; }
    }

    public record UpdateBranchRequest
    {
        [Required, MaxLength(255)]
        public string Name { get; init; } = null!;

        [MaxLength(500)]
        public string? Address { get; init; }
    }

    // ── Department ─────────────────────────────────────────────────────────────

    public record DepartmentResponse(
        Guid    Id,
        Guid    BranchId,
        string  BranchName,
        Guid?   ParentId,
        string? ParentName,
        string  Code,
        string  Name,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        List<DepartmentResponse> Children
    );

    public record CreateDepartmentRequest
    {
        [Required]
        public Guid BranchId { get; init; }

        public Guid? ParentId { get; init; }

        [Required, MaxLength(50)]
        public string Code { get; init; } = null!;

        [Required, MaxLength(255)]
        public string Name { get; init; } = null!;
    }

    public record UpdateDepartmentRequest
    {
        [Required, MaxLength(255)]
        public string Name { get; init; } = null!;

        public Guid? ParentId { get; init; }
    }

    // ── CostCenter ─────────────────────────────────────────────────────────────

    public record CostCenterResponse(
        Guid    Id,
        Guid    LegalEntityId,
        string  LegalEntityName,
        string  Code,
        string  Name,
        bool    IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt
    );

    public record CreateCostCenterRequest
    {
        [Required]
        public Guid LegalEntityId { get; init; }

        [Required, MaxLength(50)]
        public string Code { get; init; } = null!;

        [Required, MaxLength(255)]
        public string Name { get; init; } = null!;
    }
}
