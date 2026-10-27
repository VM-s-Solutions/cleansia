using System.Linq.Expressions;

namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// The ONE definition of "which orders are this data subject's" for the erasure and the subject export:
/// the orders booked on the account, and the GUEST bookings placed with the account's e-mail address
/// (owner ruling 2026-09-15, ADR-0062 D5 — a guest booking is never attached to an account afterwards,
/// so the e-mail is the only link there is). Both readers ask this, past the tenant filter — a guest
/// booking is stamped with its market's operator, not the subject's — so the orders the export lists
/// are the orders the erasure reaches; the walk leaves out the ones still live, the export does not.
///
/// <para>The <c>UserId == null</c> term is load-bearing: another ACCOUNT's order that merely carries the
/// subject's address in its contact field is that account's, not the subject's. The e-mail term is plain
/// equality because <c>Orders.CustomerEmail</c> is citext: Postgres folds the case a guest typed and can
/// seek <c>IX_Orders_CustomerEmail</c>, as the first-booking read does. Compiled in memory, or run over
/// SQLite, the comparison is exact.</para>
///
/// <para>Ask it with the subject's LIVE e-mail — after <c>User.Anonymize()</c> the address is a
/// placeholder and the guest term matches nothing.</para>
/// </summary>
public static class SubjectOrders
{
    public static Expression<Func<Order, bool>> Of(string userId, string email)
        => o => o.UserId == userId
             || (o.UserId == null && o.CustomerEmail == email);
}
