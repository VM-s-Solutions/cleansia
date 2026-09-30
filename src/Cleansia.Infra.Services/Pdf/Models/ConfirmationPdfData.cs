namespace Cleansia.Infra.Services.Pdf.Models;

/// <summary>
/// A contract confirmation on a durable medium — the customer's booking confirmation and the cleaner's
/// copy of a contract for work. The caller words every label and value in the reader's language; the
/// layout prints them as given, a section with no fields left out, then the contract text if there is one.
/// </summary>
public sealed record ConfirmationPdfData(
    string Issuer,
    string Title,
    string ReferenceLabel,
    string Reference,
    IReadOnlyList<ConfirmationPdfSection> Sections,
    IReadOnlyList<(string Text, bool IsHeading)> Text);

public sealed record ConfirmationPdfSection(string Title, IReadOnlyList<(string Label, string Value)> Fields);
