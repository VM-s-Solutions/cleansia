using System.Linq.Expressions;

namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// The ONE definition of "which orders are this data subject's" for the erasure and the subject export:
/// the orders booked on the account, and the GUEST bookings placed with the account's e-mail address
/// (owner ruling 2026-09-15, ADR-0062 D5 — a guest booking is never attached to an account afterwards,
/// so the e-mail is the only link there is). Both readers ask this, so what is erased is what is
/// exported.
///
/// <para>The <c>UserId == null</c> term is load-bearing: another ACCOUNT's order that merely carries the
/// subject's address in its contact field is that account's, not the subject's. The comparison folds
/// case the way every other read of an order by its contact e-mail does (<c>LookupOrder</c>), because
/// the column is plain text and a guest types whatever they type.</para>
///
/// <para>Ask it with the subject's LIVE e-mail — after <c>User.Anonymize()</c> the address is a
/// placeholder and the guest term matches nothing.</para>
/// </summary>
public static class SubjectOrders
{
    public static Expression<Func<Order, bool>> Of(string userId, string email)
        => o => o.UserId == userId
             || (o.UserId == null && o.CustomerEmail.ToLower() == email.ToLower());
}
