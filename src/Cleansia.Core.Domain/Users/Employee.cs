using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Documents;
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

    public decimal AverageRating { get; private set; }

    public int ComplaintsCount { get; private set; }

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
        string iban,
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
        IBAN = iban;
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

        // Clear rejection data if previously rejected
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

        // Clear approval data if previously approved
        ApprovalNotes = null;
        ApprovedByUserId = null;
        ApprovedAt = null;

        return this;
    }

    public Employee Anonymize()
    {
        RegistrationNumber = "[DELETED]";
        VatNumber = null;
        LegalEntityName = null;
        IBAN = "[DELETED]";
        PassportId = "[DELETED]";
        EmergencyContactName = null;
        EmergencyContactPhone = null;
        return this;
    }

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

        var hasEmployeeInfo = !string.IsNullOrEmpty(IBAN) &&
                             !string.IsNullOrEmpty(PassportId) &&
                             !string.IsNullOrEmpty(NationalityId) &&
                             !string.IsNullOrEmpty(RegistrationNumber);

        var hasEntityIdentity = EntityType != EmployeeEntityType.LegalEntity ||
                                !string.IsNullOrEmpty(LegalEntityName);

        var hasDocuments = Documents.Any(d => d.IsActive);

        var hasAvailability = Availability.Any();

        return hasBasicInfo && hasPersonalInfo && hasAddress &&
               hasEmployeeInfo && hasEntityIdentity && hasDocuments && hasAvailability;
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
        if (string.IsNullOrEmpty(IBAN)) missingFields.Add("profile.fields.iban");
        if (string.IsNullOrEmpty(PassportId)) missingFields.Add("profile.fields.passportId");
        if (string.IsNullOrEmpty(NationalityId)) missingFields.Add("profile.fields.nationality");
        if (!Documents.Any(d => d.IsActive)) missingFields.Add("profile.fields.documents");

        return missingFields;
    }
}
