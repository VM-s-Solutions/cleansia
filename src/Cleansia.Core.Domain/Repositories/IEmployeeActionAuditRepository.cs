using Cleansia.Core.Domain.Auditing;

namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// Adds, and the base reads. The one reader is the admin timeline (ADR-0062 D6), which lists a
/// cleaner's acts beside the customer's and the admin's for one order through <c>GetQueryable()</c>;
/// no threshold, score or consequence has been ruled (owner ruling 2026-09-06 is "record it"), so no
/// member beyond that exists. → <see cref="EmployeeActionAudit"/>
/// </summary>
public interface IEmployeeActionAuditRepository : IRepository<EmployeeActionAudit, string>;
