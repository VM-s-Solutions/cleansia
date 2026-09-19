using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Features.Extras.DTOs;

/// <param name="Prices">Price per currency CODE. Absent rather than zero for a currency nobody has priced
/// yet, so the edit form can tell "not priced here" from "priced at nothing". See AdminServiceDetailDto.</param>
public record AdminExtraDetailDto(
    string Id,
    string Slug,
    string Name,
    string? Description,
    int DisplayOrder,
    Dictionary<string, decimal> Prices,
    Dictionary<string, Translation> Translations,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn);
