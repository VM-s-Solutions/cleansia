using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Infra.Database.Repositories;

public class AdminActionAuditRepository(CleansiaDbContext context)
    : BaseRepository<AdminActionAudit>(context), IAdminActionAuditRepository;
