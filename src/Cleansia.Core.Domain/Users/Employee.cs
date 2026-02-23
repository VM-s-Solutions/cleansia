using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.Domain.Users;

public class Employee : Auditable
{
    [MaxLength(50)]
    public string? ICO { get; private set; }

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

    public Employee UpdateEmployeeDetails(string ico, string nationalityId, string passportId, string iban,
        Address address, Dictionary<string, List<TimeRange>> availability, string? emergencyContactName,
        string? emergencyContactPhone, ContractStatus? contractStatus = null)
    {
        ICO = ico;
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

    public Employee UpdateIdentification(string nationalityId, string passportId, string? taxId)
    {
        NationalityId = nationalityId;
        PassportId = passportId;
        ICO = taxId ?? string.Empty;
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

        var hasEmployeeInfo = !string.IsNullOrEmpty(ICO) &&
                             !string.IsNullOrEmpty(IBAN) &&
                             !string.IsNullOrEmpty(PassportId) &&
                             !string.IsNullOrEmpty(NationalityId);

        var hasEmergencyContact = !string.IsNullOrEmpty(EmergencyContactName) &&
                                 !string.IsNullOrEmpty(EmergencyContactPhone);

        var hasDocuments = Documents.Any(d => d.IsActive);

        var hasAvailability = Availability.Any();

        return hasBasicInfo && hasPersonalInfo && hasAddress &&
               hasEmployeeInfo && hasEmergencyContact && hasDocuments &&
               hasAvailability;
    }

    public List<string> GetMissingProfileFields()
    {
        var missingFields = new List<string>();

        if (string.IsNullOrEmpty(User?.FirstName)) missingFields.Add("First Name");
        if (string.IsNullOrEmpty(User?.LastName)) missingFields.Add("Last Name");
        if (string.IsNullOrEmpty(User?.Email)) missingFields.Add("Email");
        if (string.IsNullOrEmpty(User?.PhoneNumber)) missingFields.Add("Phone Number");
        if (User?.BirthDate == null) missingFields.Add("Birth Date");
        if (string.IsNullOrEmpty(Address?.Street)) missingFields.Add("Street");
        if (string.IsNullOrEmpty(Address?.City)) missingFields.Add("City");
        if (string.IsNullOrEmpty(Address?.ZipCode)) missingFields.Add("Zip Code");
        if (string.IsNullOrEmpty(Address?.CountryId)) missingFields.Add("Country");
        if (string.IsNullOrEmpty(ICO)) missingFields.Add("Tax ID (ICO)");
        if (string.IsNullOrEmpty(IBAN)) missingFields.Add("IBAN");
        if (string.IsNullOrEmpty(PassportId)) missingFields.Add("Passport ID");
        if (string.IsNullOrEmpty(NationalityId)) missingFields.Add("Nationality");
        if (string.IsNullOrEmpty(EmergencyContactName)) missingFields.Add("Emergency Contact Name");
        if (string.IsNullOrEmpty(EmergencyContactPhone)) missingFields.Add("Emergency Contact Phone");
        if (!Documents.Any(d => d.IsActive)) missingFields.Add("Documents");
        if (!Availability.Any()) missingFields.Add("Availability");

        return missingFields;
    }
}
