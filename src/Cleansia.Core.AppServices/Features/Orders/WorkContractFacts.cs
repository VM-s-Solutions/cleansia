using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Marker for the record frozen on a <c>WorkContractAcceptance</c> row. <c>WorkContractFactsPiiGuardTests</c>
/// walks every member name of every implementation against the archive guard's token list, so a
/// street or a customer name cannot reach a row that outlives the person.
/// </summary>
public interface IWorkContractFacts;

/// <summary>
/// What the cleaner was shown when they accepted: the job as identified by the contract text — number,
/// window, price, coarse location, scope — never the street or the customer's name, which the cleaner
/// has not been shown at that instant either. Built by one projection for the preview and the row so
/// the screen and the record cannot differ.
/// </summary>
public sealed record WorkContractFacts(
    string OrderNumber,
    DateTime CleaningDateTimeUtc,
    int EstimatedMinutes,
    decimal TotalPrice,
    string CurrencyCode,
    string LocationApproximate,
    string CountryId,
    int Rooms,
    int Bathrooms,
    IReadOnlyList<WorkContractFactsLine> Services,
    IReadOnlyList<WorkContractFactsLine> Packages,
    IReadOnlyList<string> ExtraSlugs) : IWorkContractFacts
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static WorkContractFacts FromJson(string json) =>
        JsonSerializer.Deserialize<WorkContractFacts>(json, JsonOptions)
        ?? throw new InvalidOperationException("A work-contract acceptance carries no facts.");
}

public sealed record WorkContractFactsLine(string Id, string Name);
