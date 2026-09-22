using System.Text.Json;
using Cleansia.Infra.Services.Pdf.Models;

namespace Cleansia.Core.AppServices.Features.Gdpr;

/// <summary>
/// An audit row's payload as the two-column table the incident file prints: nested objects flatten to
/// dotted keys in declaration order, arrays join with a comma, and a currency id becomes its code —
/// the row stores the id the order carried, and a lawyer reads "CZK", not a ULID.
/// </summary>
public static class IncidentFileEvidenceFields
{
    private const string CurrencyIdSuffix = "currencyid";

    public static IReadOnlyList<IncidentFileEvidenceField> Flatten(
        string? json,
        IReadOnlyDictionary<string, string> currencyCodesById,
        string? prefix = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        using var document = JsonDocument.Parse(json);
        var fields = new List<IncidentFileEvidenceField>();
        Walk(document.RootElement, prefix, fields, currencyCodesById);
        return fields;
    }

    private static void Walk(JsonElement element, string? path, List<IncidentFileEvidenceField> fields, IReadOnlyDictionary<string, string> currencyCodesById)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Walk(property.Value, path is null ? property.Name : $"{path}.{property.Name}", fields, currencyCodesById);
                }

                break;
            case JsonValueKind.Array:
                fields.Add(new IncidentFileEvidenceField(path ?? string.Empty,
                    string.Join(", ", element.EnumerateArray().Select(item => Scalar(item, path, currencyCodesById)))));
                break;
            default:
                fields.Add(new IncidentFileEvidenceField(path ?? string.Empty, Scalar(element, path, currencyCodesById)));
                break;
        }
    }

    private static string Scalar(JsonElement element, string? path, IReadOnlyDictionary<string, string> currencyCodesById)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return "—";
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            case JsonValueKind.Number:
                return element.GetRawText();
            case JsonValueKind.String:
                var value = element.GetString() ?? string.Empty;
                return path is not null
                       && path.EndsWith(CurrencyIdSuffix, StringComparison.OrdinalIgnoreCase)
                       && currencyCodesById.TryGetValue(value, out var code)
                    ? code
                    : value;
            default:
                return element.GetRawText();
        }
    }
}
