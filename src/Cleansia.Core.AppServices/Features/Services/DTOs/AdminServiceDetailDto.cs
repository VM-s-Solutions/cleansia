using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Features.Services.DTOs;

/// <param name="CategoryId">
/// The category the service belongs to. Missing from this DTO until 2026-09-09, which made the admin
/// EDIT form unable to preselect it: the form has a required category dropdown, the detail read that
/// populates it carried no category, so opening an existing service showed an empty control and saving
/// re-sent whatever the admin happened to pick. The list DTO has carried a whole Category object all
/// along; only the detail — the one the form actually reads — did not.
/// </param>
public record AdminServiceDetailDto(
    string Id,
    string Name,
    string Description,
    string CategoryId,
    // Price per currency CODE. Empty for an entry nobody has priced yet -- absent rather than zero,
    // so the edit form can tell "not priced in this currency" from "priced at nothing".
    Dictionary<string, AdminServicePriceDto> Prices,
    int EstimatedTime,
    Dictionary<string, Translation> Translations,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn);

public record AdminServicePriceDto(decimal BasePrice, decimal PerRoomPrice);
