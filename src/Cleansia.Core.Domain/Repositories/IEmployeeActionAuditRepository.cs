using Cleansia.Core.Domain.Auditing;

namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// Write-only today, deliberately. Nothing reads a cleaner's action history yet: owner ruling
/// 2026-09-06 is "record it", and no threshold, score or consequence has been ruled. A read member
/// arrives with the screen that needs it. → <see cref="EmployeeActionAudit"/>
/// </summary>
public interface IEmployeeActionAuditRepository : IRepository<EmployeeActionAudit, string>;
