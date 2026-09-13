using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Tenancy;

/// <summary>
/// An operating company under the holding — the legal entity that contracts the customer, employs
/// the cleaner, issues the receipt and pays the payout (ADR-0061 D1). Tenantless by construction: it
/// is the thing every <c>TenantId</c> column names. Seed-only; there is no admin writer until a second
/// company exists. The id is assigned, not generated ("cleansia-cz"), and capped at 26 because every
/// <c>TenantId</c> column is varchar(26).
/// </summary>
public class Tenant : BaseEntity
{
    [Required]
    [MaxLength(26)]
    public override string Id { get; set; } = default!;

    [Required]
    [MaxLength(200)]
    public string Name { get; private set; } = default!;

    private Tenant() { }

    public static Tenant Create(string id, string name) => new() { Id = id, Name = name };
}
