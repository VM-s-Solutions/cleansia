using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Documents;

/// <summary>
/// Which document types a cleaner must supply before an admin may approve them, per country.
///
/// <para><b>Per country, because the answer genuinely differs.</b> The paperwork a Czech cleaner owes
/// is not the paperwork a Polish one owes, and hardcoding one list is how a second market becomes a
/// deploy instead of a row. This sits beside the other things `CountryConfiguration` already varies by
/// jurisdiction — the registration-number label and format, VAT, the fiscal mode.</para>
///
/// <para><b>Admin-managed rather than a constant</b>, on the owner's ruling: requirements change with
/// the law, and a change that needs a release is a change that waits for one.</para>
///
/// <para><b>Why this gates approval.</b> Before it, <c>ApproveEmployee</c> checked
/// <c>IsProfileComplete()</c>, which deliberately excludes documents — its own comment says so. An
/// admin could therefore approve a cleaner with no documents at all, or with every document rejected,
/// and "Approved" then meant only "somebody clicked approve". It now means the paperwork exists and
/// was accepted.</para>
/// </summary>
public class EmployeeDocumentRequirement : Auditable
{
    /// <summary>The jurisdiction this requirement belongs to — the cleaner's WORK country.</summary>
    public string CountryId { get; private set; } = default!;

    public Country? Country { get; private set; }

    public DocumentType DocumentType { get; private set; }

    /// <summary>
    /// Whether approval is BLOCKED without it.
    ///
    /// <para>A row that is not required is still meaningful: it is how a document type is offered to
    /// the cleaner as expected-but-optional, which is what the upload screen lists. Deleting the row
    /// removes it from the screen entirely; clearing this flag keeps the prompt and drops the gate.</para>
    /// </summary>
    public bool IsRequired { get; private set; } = true;

    /// <summary>
    /// Order the cleaner is asked for these, lowest first. The upload screen is a checklist and a
    /// checklist in arbitrary order reads as arbitrary.
    /// </summary>
    public int SortOrder { get; private set; }

    private EmployeeDocumentRequirement() { }

    public static EmployeeDocumentRequirement Create(
        string countryId,
        DocumentType documentType,
        bool isRequired,
        int sortOrder,
        string createdBy)
    {
        var requirement = new EmployeeDocumentRequirement
        {
            CountryId = countryId,
            DocumentType = documentType,
            IsRequired = isRequired,
            SortOrder = sortOrder,
        };

        requirement.Created(createdBy, DateTimeOffset.UtcNow);
        return requirement;
    }

    public EmployeeDocumentRequirement Update(bool isRequired, int sortOrder, string updatedBy)
    {
        IsRequired = isRequired;
        SortOrder = sortOrder;
        Updated(updatedBy, DateTimeOffset.UtcNow);
        return this;
    }
}
