#nullable enable
namespace Cleansia.Core.AppServices.Features.PromoCodes.Admin.Filters;

public record PromoCodeFilter(
    bool? Active = null,
    bool? Expired = null,
    string? SearchCode = null);
