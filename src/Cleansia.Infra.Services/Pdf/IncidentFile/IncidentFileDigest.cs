using System.Security.Cryptography;
using System.Text;

namespace Cleansia.Infra.Services.Pdf.IncidentFile;

/// <summary>
/// The data section as one canonical UTF-8 text — LF line ends, no timestamps of the generation itself
/// — and its SHA-256. Two builds over unchanged data print the same hash however far apart they were
/// generated, which is what lets a printed copy be checked against a later re-generation and against
/// the hash the audit row carries.
/// </summary>
public static class IncidentFileDigest
{
    public static string CanonicalText(IReadOnlyList<IncidentFileSection> sections)
    {
        var text = new StringBuilder();
        foreach (var section in sections)
        {
            text.Append("# ").Append(section.Title).Append('\n');
            foreach (var block in section.Blocks)
            {
                Append(text, block);
            }
        }

        return text.ToString();
    }

    public static string Sha256Hex(IReadOnlyList<IncidentFileSection> sections) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalText(sections))));

    private static void Append(StringBuilder text, IncidentFileBlock block)
    {
        switch (block)
        {
            case IncidentFileSubheading subheading:
                text.Append("## ").Append(subheading.Text).Append('\n');
                break;
            case IncidentFileParagraph paragraph:
                text.Append(paragraph.Text).Append('\n');
                break;
            case IncidentFileFieldList fields:
                foreach (var field in fields.Fields)
                {
                    text.Append(field.Label).Append(": ").Append(field.Value).Append('\n');
                }

                break;
            case IncidentFileTable table:
                if (table.Caption is not null)
                {
                    text.Append('[').Append(table.Caption).Append("]\n");
                }

                text.Append(string.Join(" | ", table.Headers)).Append('\n');
                foreach (var row in table.Rows)
                {
                    text.Append(string.Join(" | ", row)).Append('\n');
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(block), block.GetType().Name, "an incident file block the digest does not know");
        }
    }
}
