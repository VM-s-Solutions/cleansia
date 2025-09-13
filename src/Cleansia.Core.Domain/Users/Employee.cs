using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Users;

public class Employee : Auditable
{
    [Password]
    [MaxLength(255)]
    public string? Password { get; private set; }

    [Required]
    [MaxLength(50)]
    public string FirstName { get; private set; }

    [Required]
    [MaxLength(50)]
    public string LastName { get; private set; }

    [Required]
    [MaxLength(150)]
    [EmailAddress]
    public string Email { get; private set; }

    [PhoneNumber]
    [MaxLength(50)]
    public string? PhoneNumber { get; private set; }

    [MaxLength(512)]
    public string? GoogleId { get; private set; }

    public string? ResetPasswordCode { get; private set; }

    public DateTimeOffset? ResetPasswordCodeExpiresAt { get; private set; }

    [DateRangeControl(yearsRange: 100)]
    public DateOnly? BirthDate { get; private set; }

    public UserProfile Profile { get; private set; } = UserProfile.Customer;

    public AuthenticationType AuthenticationType { get; private set; } = AuthenticationType.Internal;

    public virtual Cart? Cart { get; private set; }

    public string? ProfilePhotoName { get; private set; }

    public string? ConfirmationCode { get; private set; }

    public DateTimeOffset? ConfirmationCodeExpiresAt { get; private set; }

    public bool IsEmailConfirmed { get; private set; }

    [Required]
    [MaxLength(50)]
    public string ICO { get; private set; }

    public string AddressId { get; private set; }
    public Address? Address { get; private set; }

    public decimal AverageRating { get; private set; }

    public int ComplaintsCount { get; private set; }

    public ContractStatus ContractStatus { get; private set; } = ContractStatus.Pending;

    private IDictionary<string, List<TimeRange>> _availability = new Dictionary<string, List<TimeRange>>();
    public IReadOnlyDictionary<string, List<TimeRange>> Availability => _availability.ToDictionary().AsReadOnly();

    private ICollection<OrderEmployee> _assignedOrders = [];
    public IReadOnlyCollection<OrderEmployee> AssignedOrders => _assignedOrders.ToList().AsReadOnly();

    private ICollection<Order> _orders = [];
    public virtual IReadOnlyCollection<Order> Orders => _orders.ToList().AsReadOnly();

    public static Employee CreateWithPassword(string email, string password, string firstName, string lastName)
        => new()
        {
            Email = email,
            Password = password,
            FirstName = firstName,
            LastName = lastName,
            ConfirmationCode = new Random().Next(100000, 999999).ToString(),
            ConfirmationCodeExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };

    public Employee UpdateConfirmationCode()
    {
        ConfirmationCode = new Random().Next(100000, 999999).ToString();
        ConfirmationCodeExpiresAt = DateTime.UtcNow.AddMinutes(15);

        return this;
    }

    public Employee UpdateEmployeeDetails(string ico, Address address, Dictionary<string, List<TimeRange>> availability, ContractStatus? contractStatus = null)
    {
        ICO = ico;
        Address = address;
        _availability = availability;
        if (contractStatus is not null)
        {
            ContractStatus = contractStatus.Value;
        }

        return this;
    }

    public Employee UpdateRating(decimal newRating, int newComplaints)
    {
        AverageRating = newRating;
        ComplaintsCount = newComplaints;
        return this;
    }
}