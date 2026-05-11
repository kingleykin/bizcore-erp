using Admin.API.Application.DTOs;
using Admin.API.Domain.Entities.Organization;
using Admin.API.Domain.Events;
using Admin.API.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Services
{
    public class OrganizationService : IOrganizationService
    {
        private readonly AdminDbContext _db;
        private readonly IPublishEndpoint  _bus;
        private readonly ILogger<OrganizationService> _logger;

        public OrganizationService(
            AdminDbContext            db,
            IPublishEndpoint             bus,
            ILogger<OrganizationService> logger)
        {
            _db     = db;
            _bus    = bus;
            _logger = logger;
        }

        // ── LegalEntity ────────────────────────────────────────────────────────

        public async Task<IEnumerable<LegalEntityResponse>> GetLegalEntitiesAsync()
        {
            var entities = await _db.LegalEntities
                .OrderBy(e => e.Code)
                .ToListAsync();
            return entities.Select(Map);
        }

        public async Task<LegalEntityResponse?> GetLegalEntityByIdAsync(Guid id)
        {
            var entity = await _db.LegalEntities.FindAsync(id);
            return entity is null ? null : Map(entity);
        }

        public async Task<LegalEntityResponse> CreateLegalEntityAsync(CreateLegalEntityRequest request)
        {
            if (await _db.LegalEntities.AnyAsync(e => e.Code == request.Code.ToUpperInvariant()))
                throw new InvalidOperationException($"LegalEntity with code '{request.Code}' already exists.");

            var entity = LegalEntity.Create(
                request.Code,
                request.Name,
                request.TaxCode,
                request.RegistrationNumber,
                request.Address,
                request.BaseCurrencyCode);

            _db.LegalEntities.Add(entity);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Created LegalEntity {Code} ({Id}).", entity.Code, entity.Id);
            return Map(entity);
        }

        public async Task<LegalEntityResponse?> UpdateLegalEntityAsync(Guid id, UpdateLegalEntityRequest request)
        {
            var entity = await _db.LegalEntities.FindAsync(id);
            if (entity is null) return null;

            entity.Update(
                request.Name,
                request.TaxCode,
                request.RegistrationNumber,
                request.Address,
                request.BaseCurrencyCode);

            await _db.SaveChangesAsync();

            // Publish integration event cho các service khác (ACC, HR)
            await _bus.Publish(new LegalEntityUpdatedEvent
            {
                LegalEntityId   = entity.Id,
                Code            = entity.Code,
                Name            = entity.Name,
                BaseCurrencyCode = entity.BaseCurrencyCode,
                Status          = entity.Status
            });

            _logger.LogInformation("Updated LegalEntity {Code} ({Id}) and published LegalEntityUpdatedEvent.", entity.Code, entity.Id);
            return Map(entity);
        }

        public async Task<bool> DeactivateLegalEntityAsync(Guid id)
        {
            var entity = await _db.LegalEntities.FindAsync(id);
            if (entity is null) return false;

            entity.Deactivate();
            await _db.SaveChangesAsync();
            return true;
        }

        // ── Branch ─────────────────────────────────────────────────────────────

        public async Task<IEnumerable<BranchResponse>> GetBranchesAsync(Guid? legalEntityId = null)
        {
            var query = _db.Branches
                .Include(b => b.LegalEntity)
                .AsQueryable();

            if (legalEntityId.HasValue)
                query = query.Where(b => b.LegalEntityId == legalEntityId.Value);

            var branches = await query.OrderBy(b => b.Code).ToListAsync();
            return branches.Select(MapBranch);
        }

        public async Task<BranchResponse?> GetBranchByIdAsync(Guid id)
        {
            var branch = await _db.Branches
                .Include(b => b.LegalEntity)
                .FirstOrDefaultAsync(b => b.Id == id);
            return branch is null ? null : MapBranch(branch);
        }

        public async Task<BranchResponse> CreateBranchAsync(CreateBranchRequest request)
        {
            if (!await _db.LegalEntities.AnyAsync(e => e.Id == request.LegalEntityId))
                throw new InvalidOperationException($"LegalEntity '{request.LegalEntityId}' not found.");

            if (await _db.Branches.AnyAsync(b => b.Code == request.Code.ToUpperInvariant()))
                throw new InvalidOperationException($"Branch with code '{request.Code}' already exists.");

            var branch = Branch.Create(request.LegalEntityId, request.Code, request.Name, request.Address);
            _db.Branches.Add(branch);
            await _db.SaveChangesAsync();

            // reload navigation
            await _db.Entry(branch).Reference(b => b.LegalEntity).LoadAsync();

            _logger.LogInformation("Created Branch {Code} ({Id}).", branch.Code, branch.Id);
            return MapBranch(branch);
        }

        public async Task<BranchResponse?> UpdateBranchAsync(Guid id, UpdateBranchRequest request)
        {
            var branch = await _db.Branches
                .Include(b => b.LegalEntity)
                .FirstOrDefaultAsync(b => b.Id == id);
            if (branch is null) return null;

            branch.Update(request.Name, request.Address);
            await _db.SaveChangesAsync();
            return MapBranch(branch);
        }

        // ── Department ─────────────────────────────────────────────────────────

        public async Task<IEnumerable<DepartmentResponse>> GetDepartmentTreeAsync(Guid? branchId = null)
        {
            var query = _db.Departments
                .Include(d => d.Branch)
                .Include(d => d.Parent)
                .AsQueryable();

            if (branchId.HasValue)
                query = query.Where(d => d.BranchId == branchId.Value);

            var all = await query.OrderBy(d => d.Code).ToListAsync();

            // Build tree — only root nodes; children are populated recursively
            var roots = all.Where(d => d.ParentId is null).ToList();
            return roots.Select(d => MapDeptTree(d, all));
        }

        public async Task<DepartmentResponse> CreateDepartmentAsync(CreateDepartmentRequest request)
        {
            if (!await _db.Branches.AnyAsync(b => b.Id == request.BranchId))
                throw new InvalidOperationException($"Branch '{request.BranchId}' not found.");

            if (request.ParentId.HasValue && !await _db.Departments.AnyAsync(d => d.Id == request.ParentId.Value))
                throw new InvalidOperationException($"Parent department '{request.ParentId}' not found.");

            if (await _db.Departments.AnyAsync(d => d.Code == request.Code.ToUpperInvariant()))
                throw new InvalidOperationException($"Department with code '{request.Code}' already exists.");

            var dept = Department.Create(request.BranchId, request.Code, request.Name, request.ParentId);
            _db.Departments.Add(dept);
            await _db.SaveChangesAsync();

            await _db.Entry(dept).Reference(d => d.Branch).LoadAsync();
            _logger.LogInformation("Created Department {Code} ({Id}).", dept.Code, dept.Id);

            return MapDeptTree(dept, new List<Department>());
        }

        public async Task<DepartmentResponse?> UpdateDepartmentAsync(Guid id, UpdateDepartmentRequest request)
        {
            var dept = await _db.Departments
                .Include(d => d.Branch)
                .Include(d => d.Parent)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (dept is null) return null;

            dept.Update(request.Name, request.ParentId);
            await _db.SaveChangesAsync();
            return MapDeptTree(dept, new List<Department>());
        }

        // ── CostCenter ─────────────────────────────────────────────────────────

        public async Task<IEnumerable<CostCenterResponse>> GetCostCentersAsync(Guid? legalEntityId = null)
        {
            var query = _db.CostCenters
                .Include(c => c.LegalEntity)
                .AsQueryable();

            if (legalEntityId.HasValue)
                query = query.Where(c => c.LegalEntityId == legalEntityId.Value);

            var list = await query.OrderBy(c => c.Code).ToListAsync();
            return list.Select(MapCostCenter);
        }

        public async Task<CostCenterResponse> CreateCostCenterAsync(CreateCostCenterRequest request)
        {
            if (!await _db.LegalEntities.AnyAsync(e => e.Id == request.LegalEntityId))
                throw new InvalidOperationException($"LegalEntity '{request.LegalEntityId}' not found.");

            if (await _db.CostCenters.AnyAsync(c => c.Code == request.Code.ToUpperInvariant()))
                throw new InvalidOperationException($"CostCenter with code '{request.Code}' already exists.");

            var cc = CostCenter.Create(request.LegalEntityId, request.Code, request.Name);
            _db.CostCenters.Add(cc);
            await _db.SaveChangesAsync();

            await _db.Entry(cc).Reference(c => c.LegalEntity).LoadAsync();
            return MapCostCenter(cc);
        }

        // ── Mapping helpers ────────────────────────────────────────────────────

        private static LegalEntityResponse Map(LegalEntity e) => new(
            e.Id, e.Code, e.Name, e.TaxCode, e.RegistrationNumber,
            e.Address, e.BaseCurrencyCode, e.Status, e.CreatedAt, e.UpdatedAt);

        private static BranchResponse MapBranch(Branch b) => new(
            b.Id, b.LegalEntityId, b.LegalEntity?.Name ?? string.Empty,
            b.Code, b.Name, b.Address, b.IsActive, b.CreatedAt, b.UpdatedAt);

        private static DepartmentResponse MapDeptTree(Department d, IList<Department> all) => new(
            d.Id, d.BranchId, d.Branch?.Name ?? string.Empty,
            d.ParentId, d.Parent?.Name,
            d.Code, d.Name, d.CreatedAt, d.UpdatedAt,
            all.Where(c => c.ParentId == d.Id).Select(c => MapDeptTree(c, all)).ToList());

        private static CostCenterResponse MapCostCenter(CostCenter c) => new(
            c.Id, c.LegalEntityId, c.LegalEntity?.Name ?? string.Empty,
            c.Code, c.Name, c.IsActive, c.CreatedAt, c.UpdatedAt);
    }
}
