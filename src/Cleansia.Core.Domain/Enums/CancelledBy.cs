namespace Cleansia.Core.Domain.Enums;

/// <summary>
/// Who initiated an order cancellation. Persisted as the legacy lowercase string
/// ("customer"/"cleaner"/"admin"/"system") so already-cancelled rows stay readable
/// (see the value converter in OrderEntityConfiguration).
/// </summary>
public enum CancelledBy
{
    Customer = 0,
    Cleaner = 1,
    Admin = 2,
    System = 3,
}
