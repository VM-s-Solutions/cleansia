using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.Domain.Users;

public class Employee : Auditable, ITenantEntity
{
    public EmployeeEntityType EntityType { get; private set; } = EmployeeEntityType.NaturalPerson;

    [MaxLength(50)]
    public string? RegistrationNumber { get; private set; }

    [MaxLength(50)]
    public string? VatNumber { get; private set; }

    [MaxLength(200)]
    public string? LegalEntityName { get; private set; }

    public string? IBAN { get; private set; }

    /// <summary>
    /// ADR-0034 D1.1 — the profile-completeness gate's payout term, as a scalar on the employee row
    /// rather than a test of <see cref="PayoutDetails"/>. There is no lazy loading in this repository and
    /// <c>GetByUserEmailAsync</c>'s include list is hand-written, so a gate reading the navigation would
    /// return 403 for every cleaner the moment the navigation was not loaded. A column is materialized by
    /// every loader that loads an <see cref="Employee"/> at all.
    ///
    /// <para>Invariant: <c>HasPayoutDetails == (an EmployeePayoutDetails row exists for this employee)</c>.
    /// It carries <i>presence</i>, never validity (D7) — real validation applies to writes and to payout
    /// issuance, and never retroactively invalidates a profile.</para>
    /// </summary>
    public bool HasPayoutDetails { get; private set; }

    /// <summary>
    /// The payout destination (ADR-0034). <b>Never <c>Include</c>d on a paged or list query</b> — the
    /// gate does not need it (see <see cref="HasPayoutDetails"/>), and materializing the unmasked record
    /// on the admin grid is the exposure D8's read contract exists to prevent.
    /// </summary>
    public EmployeePayoutDetails? PayoutDetails { get; private set; }

    /// <summary>Flips the gate scalar alongside the one payout write path that creates or replaces the record.</summary>
    public Employee MarkPayoutDetailsProvided()
    {
        HasPayoutDetails = true;
        return this;
    }

    /// <summary>Clears the gate scalar alongside the id-keyed delete of the payout record (erasure, D1.1.2).</summary>
    public Employee ClearPayoutDetails()
    {
        HasPayoutDetails = false;
        return this;
    }

    public decimal AverageRating { get; private set; }

    public int ComplaintsCount { get; private set; }

    /// <summary>
    /// Timestamp of the last "new jobs available" digest push delivered to
    /// this cleaner. Used by the digest sweep to find newly-eligible orders
    /// since the cleaner was last notified. Null = never notified
    /// (cleaner gets all currently-eligible orders on first digest).
    /// </summary>
    public DateTimeOffset? LastNewJobsDigestAt { get; private set; }

    /// <summary>
    /// Called by the digest service after enqueueing a push for this
    /// cleaner. Single source of truth for the watermark write — keeps the
    /// "we notified them" decision and the watermark update co-located.
    /// </summary>
    public Employee MarkNewJobsDigestSent(DateTimeOffset at)
    {
        LastNewJobsDigestAt = at;
        return this;
    }

    /// <summary>
    /// How far from their home address this cleaner wants to be told about work, in kilometres
    /// (Q-FEED-03). Read by the new-jobs digest through <see cref="Orders.JobProximity"/>.
    ///
    /// <para><b>NULL means "no preference expressed", and that is a meaningful value, not a missing
    /// one</b> — it keeps today's country-wide reach. There is deliberately no backfilled default: a
    /// number nobody chose would silently cut an existing cleaner's job notifications down to it, and a
    /// notification that stops arriving produces no complaint from the person it costs.</para>
    /// </summary>
    public int? JobRadiusKm { get; private set; }

    /// <summary>
    /// Sets or clears the digest radius. Null clears it back to country-wide; the range is the
    /// validator's business error first, so reaching the throw means a non-HTTP caller bypassed it.
    /// </summary>
    public Employee SetJobRadius(int? radiusKm)
    {
        if (radiusKm is { } value
            && (value < Orders.JobProximity.MinRadiusKm || value > Orders.JobProximity.MaxRadiusKm))
        {
            throw new ArgumentOutOfRangeException(nameof(radiusKm), value, "Job radius is out of range");
        }

        JobRadiusKm = radiusKm;
        return this;
    }

    public ContractStatus ContractStatus { get; private set; } = ContractStatus.Pending;

    [MaxLength(500)]
    public string? RejectionReason { get; private set; }

    [MaxLength(1000)]
    public string? ApprovalNotes { get; private set; }

    public string? ApprovedByUserId { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }

    public string? RejectedByUserId { get; private set; }
    public DateTimeOffset? RejectedAt { get; private set; }

    public string? PassportId { get; private set; }

    public string? NationalityId { get; private set; }
    public Country? Nationality { get; private set; }

    /// <summary>
    /// Country the cleaner is approved to take work in. Drives
    /// currency / language / VAT / pay-rule defaults via
    /// <see cref="Configuration.CountryConfiguration"/>. Distinct
    /// from <see cref="NationalityId"/> (passport) and
    /// <see cref="Address"/>.CountryId (residency) — an EU contractor
    /// could be CZ-national, SK-resident, and approved to work in CZ.
    ///
    /// Nullable until admin approves the cleaner for a country.
    /// Currency / language resolution falls back to global defaults
    /// while null.
    /// </summary>
    public string? WorkCountryId { get; private set; }
    public Country? WorkCountry { get; private set; }

    public string? EmergencyContactName { get; private set; }
    public string? EmergencyContactPhone { get; private set; }

    public string? AddressId { get; private set; }
    public Address? Address { get; private set; }

    public string UserId { get; private set; }
    public User? User { get; private set; }

    [MaxLength(10)]
    public string? PreferredCurrencyCode { get; private set; }

    private IDictionary<string, List<TimeRange>> _availability = new Dictionary<string, List<TimeRange>>();
    public IReadOnlyDictionary<string, List<TimeRange>> Availability => _availability.ToDictionary().AsReadOnly();

    private ICollection<EmployeeDocument> _documents = [];
    public IReadOnlyCollection<EmployeeDocument> Documents => _documents.ToList().AsReadOnly();

    private ICollection<OrderEmployee> _assignedOrders = [];
    public IReadOnlyCollection<OrderEmployee> AssignedOrders => _assignedOrders.ToList().AsReadOnly();

    private ICollection<OrderEmployeePay> _orderPays = [];
    public IReadOnlyCollection<OrderEmployeePay> OrderPays => _orderPays.ToList().AsReadOnly();

    private ICollection<EmployeeInvoice> _invoices = [];
    public IReadOnlyCollection<EmployeeInvoice> Invoices => _invoices.ToList().AsReadOnly();

    public static Employee CreateWithUser(User user) => new()
    {
        User = user ?? throw new ArgumentNullException(nameof(user)),
        UserId = user.Id
    };

    public Employee UpdateEmployeeDetails(
        EmployeeEntityType entityType,
        string? registrationNumber,
        string? vatNumber,
        string? legalEntityName,
        string nationalityId,
        string passportId,
        Address address,
        Dictionary<string, List<TimeRange>> availability,
        string? emergencyContactName,
        string? emergencyContactPhone,
        ContractStatus? contractStatus = null)
    {
        EntityType = entityType;
        RegistrationNumber = registrationNumber;
        VatNumber = vatNumber;
        LegalEntityName = entityType == EmployeeEntityType.LegalEntity ? legalEntityName : null;
        NationalityId = nationalityId;
        PassportId = passportId;
        Address = address;
        EmergencyContactName = emergencyContactName;
        EmergencyContactPhone = emergencyContactPhone;
        _availability = availability;
        if (contractStatus is not null)
        {
            ContractStatus = contractStatus.Value;
        }

        return this;
    }

    public Employee UpdateIdentification(string nationalityId, string passportId)
    {
        NationalityId = nationalityId;
        PassportId = passportId;
        return this;
    }

    public Employee UpdateBusinessIdentity(
        EmployeeEntityType entityType,
        string? registrationNumber,
        string? vatNumber,
        string? legalEntityName)
    {
        EntityType = entityType;
        RegistrationNumber = registrationNumber;
        VatNumber = vatNumber;
        LegalEntityName = entityType == EmployeeEntityType.LegalEntity ? legalEntityName : null;
        return this;
    }

    public Employee UpdateAddress(Address address)
    {
        Address = address;
        return this;
    }

    /// <summary>
    /// The legacy payout column. Written only by the payout write path, which mirrors the derived IBAN
    /// here so the invoice's supplier block keeps printing the current account until T-0522 moves that
    /// read onto <see cref="PayoutDetails"/>. <see cref="EmployeePayoutDetails"/> is the source of truth.
    /// </summary>
    public Employee UpdateBankDetails(string iban)
    {
        IBAN = iban;
        return this;
    }

    public Employee UpdateEmergencyContact(string? emergencyName, string? emergencyPhone)
    {
        EmergencyContactName = emergencyName;
        EmergencyContactPhone = emergencyPhone;
        return this;
    }

    public Employee UpdateAvailability(Dictionary<string, List<TimeRange>> availability)
    {
        _availability = availability;
        return this;
    }

    public Employee UpdateRating(decimal newRating, int newComplaints)
    {
        AverageRating = newRating;
        ComplaintsCount = newComplaints;
        return this;
    }

    public Employee UpdatePreferredCurrency(string? preferredCurrencyCode)
    {
        PreferredCurrencyCode = preferredCurrencyCode;
        return this;
    }

    /// <summary>
    /// Set the country this cleaner is approved to take work in.
    /// Called from the admin approval flow when the operator picks
    /// the work jurisdiction. Currency / language / VAT defaults all
    /// derive from this via CountryConfiguration.
    /// </summary>
    public Employee AssignWorkCountry(string countryId)
    {
        if (string.IsNullOrWhiteSpace(countryId))
        {
            throw new ArgumentException("Work country id is required", nameof(countryId));
        }
        WorkCountryId = countryId;
        return this;
    }

    public Employee UpdateContractStatus(ContractStatus contractStatus)
    {
        ContractStatus = contractStatus;
        return this;
    }

    public Employee Approve(string approvedByUserId, string? notes = null)
    {
        ContractStatus = ContractStatus.Approved;
        ApprovedByUserId = approvedByUserId;
        ApprovedAt = DateTimeOffset.UtcNow;
        ApprovalNotes = notes;

        RejectionReason = null;
        RejectedByUserId = null;
        RejectedAt = null;

        return this;
    }

    public Employee Reject(string rejectedByUserId, string? reason = null)
    {
        ContractStatus = ContractStatus.Rejected;
        RejectedByUserId = rejectedByUserId;
        RejectedAt = DateTimeOffset.UtcNow;
        RejectionReason = reason;

        ApprovalNotes = null;
        ApprovedByUserId = null;
        ApprovedAt = null;

        return this;
    }

    public Employee Anonymize()
    {
        RegistrationNumber = AnonymizationMarker.Value;
        VatNumber = null;
        LegalEntityName = null;
        IBAN = AnonymizationMarker.Value;
        PassportId = AnonymizationMarker.Value;
        EmergencyContactName = null;
        EmergencyContactPhone = null;
        // The child payout record is deleted by GdprDeletionService with an id-keyed write, because a
        // navigation-walking clear here would be a silent no-op whenever the caller did not Include it
        // (ADR-0034 D1.1.2). This only drops the gate scalar, which lives on the row already loaded.
        HasPayoutDetails = false;
        return this;
    }

    /// <summary>
    /// The profile gate's payout term: PRESENCE of a payout destination, never its validity (ADR-0034 D7).
    ///
    /// <para>It reads two scalars on this row and no navigation, so it is materialized by every loader and
    /// cannot depend on a hand-written include list. <see cref="IBAN"/> is the second term because the
    /// launch and DEV databases carry cleaners whose destination predates
    /// <see cref="EmployeePayoutDetails"/> and there is no backfill: reading only
    /// <see cref="HasPayoutDetails"/> would mark every one of them incomplete on deploy day and 403 them
    /// off the whole partner surface, which is the outage D7 exists to prevent. The term retires when the
    /// legacy column does.</para>
    /// </summary>
    private bool HasPayoutDestination() => HasPayoutDetails || !string.IsNullOrEmpty(IBAN);

    public bool IsProfileComplete()
    {
        var hasBasicInfo = User?.FirstName != null &&
                           User?.LastName != null &&
                           User?.Email != null &&
                           User?.PhoneNumber != null;

        var hasPersonalInfo = User?.BirthDate != null;

        var hasAddress = Address?.Street != null &&
                        Address?.City != null &&
                        Address?.ZipCode != null &&
                        Address?.CountryId != null;

        var hasEmployeeInfo = HasPayoutDestination() &&
                             !string.IsNullOrEmpty(PassportId) &&
                             !string.IsNullOrEmpty(NationalityId) &&
                             !string.IsNullOrEmpty(RegistrationNumber);

        var hasEntityIdentity = EntityType != EmployeeEntityType.LegalEntity ||
                                !string.IsNullOrEmpty(LegalEntityName);

        // Documents and emergency contacts are handled separately by the registration lock.
        // Emergency contacts are optional. Documents have their own category.
        return hasBasicInfo && hasPersonalInfo && hasAddress &&
               hasEmployeeInfo && hasEntityIdentity;
    }

    public List<string> GetMissingProfileFields()
    {
        var missingFields = new List<string>();

        if (string.IsNullOrEmpty(User?.FirstName)) missingFields.Add("profile.fields.firstName");
        if (string.IsNullOrEmpty(User?.LastName)) missingFields.Add("profile.fields.lastName");
        if (string.IsNullOrEmpty(User?.Email)) missingFields.Add("profile.fields.email");
        if (string.IsNullOrEmpty(User?.PhoneNumber)) missingFields.Add("profile.fields.phoneNumber");
        if (User?.BirthDate == null) missingFields.Add("profile.fields.birthDate");
        if (string.IsNullOrEmpty(Address?.Street)) missingFields.Add("profile.fields.street");
        if (string.IsNullOrEmpty(Address?.City)) missingFields.Add("profile.fields.city");
        if (string.IsNullOrEmpty(Address?.ZipCode)) missingFields.Add("profile.fields.zipCode");
        if (string.IsNullOrEmpty(Address?.CountryId)) missingFields.Add("profile.fields.country");
        if (string.IsNullOrEmpty(RegistrationNumber)) missingFields.Add("profile.fields.registrationNumber");
        if (EntityType == EmployeeEntityType.LegalEntity && string.IsNullOrEmpty(LegalEntityName))
            missingFields.Add("profile.fields.legalEntityName");
        // The key still says "iban" because five shipped clients translate it, two of them app-store
        // gated; renaming it to "payoutDetails" would show a raw key on every un-updated device. Rename
        // on a coordinated mobile release (ADR-0034 D7).
        if (!HasPayoutDestination()) missingFields.Add("profile.fields.iban");
        if (string.IsNullOrEmpty(PassportId)) missingFields.Add("profile.fields.passportId");
        if (string.IsNullOrEmpty(NationalityId)) missingFields.Add("profile.fields.nationality");

        return missingFields;
    }
}
