#nullable enable
namespace Cleansia.Core.AppServices.Shared.DTOs.Common;

public record Auditable<T>(
    DateTimeOffset CreatedOn,
    string CreatedBy,
    DateTimeOffset? UpdatedOn,
    string? UpdatedBy,
    DateTimeOffset? DeactivatedOn,
    string? DeactivatedBy,
    T Id,
    bool IsActive)
    : BaseRecord<T>(Id, IsActive);
