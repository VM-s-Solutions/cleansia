using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Infra.Database.Repositories;

public class EmployeeActionAuditRepository(CleansiaDbContext context)
    : BaseRepository<EmployeeActionAudit>(context), IEmployeeActionAuditRepository;
