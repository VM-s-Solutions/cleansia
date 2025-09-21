using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
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

    public string? PassportId { get; private set; }

    public string? NationalityId { get; private set; }
    public Country? Nationality { get; private set; }

    public string? EmergencyContactName { get; private set; }
    public string? EmergencyContactPhone { get; private set; }

    public string? AddressId { get; private set; }
    public Address? Address { get; private set; }

    public string UserId { get; private set; }
    public User? User { get; private set; }

    private IDictionary<string, List<TimeRange>> _availability = new Dictionary<string, List<TimeRange>>();
    public IReadOnlyDictionary<string, List<TimeRange>> Availability => _availability.ToDictionary().AsReadOnly();

    private ICollection<string> _documentFileNames = [];
    public virtual IReadOnlyCollection<string> DocumentFileNames => _documentFileNames.ToList().AsReadOnly();

    private ICollection<OrderEmployee> _assignedOrders = [];
    public IReadOnlyCollection<OrderEmployee> AssignedOrders => _assignedOrders.ToList().AsReadOnly();

    private ICollection<Order> _orders = [];
    public virtual IReadOnlyCollection<Order> Orders => _orders.ToList().AsReadOnly();

    public static Employee CreateWithUser(User user) => new()
    {
        User = user ?? throw new ArgumentNullException(nameof(user)),
        UserId = user.Id
    };

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

    public Employee AddDocumentFileName(string fileName)
    {
        _documentFileNames.Add(fileName);
        return this;
    }

    public Employee AddDocumentFileNames(IEnumerable<string> fileNames)
    {
        foreach (var fileName in fileNames)
        {
            _documentFileNames.Add(fileName);
        }
        return this;
    }
}