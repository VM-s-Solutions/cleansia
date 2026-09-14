namespace Cleansia.Infra.Services.Pdf.IncidentFile;

/// <summary>
/// The document model between the data and the page. The PDF layout and the SHA-256 digest both walk
/// THIS, never the data directly, so the hash printed on the last page covers exactly what the pages
/// show and a field cannot reach one without the other. Tests assert on it too: QuestPDF subsets its
/// fonts, so nothing can be read back out of the bytes.
/// </summary>
public sealed record IncidentFileSection(string Title, IReadOnlyList<IncidentFileBlock> Blocks);

public abstract record IncidentFileBlock;

public sealed record IncidentFileSubheading(string Text) : IncidentFileBlock;

public sealed record IncidentFileParagraph(string Text) : IncidentFileBlock;

public sealed record IncidentFileField(string Label, string Value);

public sealed record IncidentFileFieldList(IReadOnlyList<IncidentFileField> Fields) : IncidentFileBlock;

public sealed record IncidentFileTable(
    string? Caption,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows) : IncidentFileBlock;
