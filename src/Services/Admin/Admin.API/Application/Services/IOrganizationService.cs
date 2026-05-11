using Admin.API.Application.DTOs;

namespace Admin.API.Application.Services
{
    public interface IOrganizationService
    {
        // LegalEntity
        Task<IEnumerable<LegalEntityResponse>> GetLegalEntitiesAsync();
        Task<LegalEntityResponse?>             GetLegalEntityByIdAsync(Guid id);
        Task<LegalEntityResponse>              CreateLegalEntityAsync(CreateLegalEntityRequest request);
        Task<LegalEntityResponse?>             UpdateLegalEntityAsync(Guid id, UpdateLegalEntityRequest request);
        Task<bool>                             DeactivateLegalEntityAsync(Guid id);

        // Branch
        Task<IEnumerable<BranchResponse>> GetBranchesAsync(Guid? legalEntityId = null);
        Task<BranchResponse?>             GetBranchByIdAsync(Guid id);
        Task<BranchResponse>              CreateBranchAsync(CreateBranchRequest request);
        Task<BranchResponse?>             UpdateBranchAsync(Guid id, UpdateBranchRequest request);

        // Department
        Task<IEnumerable<DepartmentResponse>> GetDepartmentTreeAsync(Guid? branchId = null);
        Task<DepartmentResponse>              CreateDepartmentAsync(CreateDepartmentRequest request);
        Task<DepartmentResponse?>             UpdateDepartmentAsync(Guid id, UpdateDepartmentRequest request);

        // CostCenter
        Task<IEnumerable<CostCenterResponse>> GetCostCentersAsync(Guid? legalEntityId = null);
        Task<CostCenterResponse>              CreateCostCenterAsync(CreateCostCenterRequest request);
    }
}
