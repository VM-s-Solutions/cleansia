#nullable enable
namespace Cleansia.Core.AppServices.Features.AdminUsers.Filters;

public record AdminUserFilter(
    string? SearchTerm,
    bool? IsActive);