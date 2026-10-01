#nullable enable
namespace Cleansia.Core.AppServices.Features.Users.Filters;

public record CustomerFilter(
    string? SearchTerm,
    bool? IsActive);
