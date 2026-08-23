namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// The dedup subject for every push raised by an assignment changing hands.
///
/// <para><b>Why not just the order id.</b> <c>MessageKeys.Push</c> is
/// <c>push:{userId}:{eventKey}:{subject}</c> and the outbox enforces that as UNIQUE, so the subject is
/// what decides whether two sends are "the same message". An order id says which BOOKING; it does not
/// say which assignment — and every one of these events is about an assignment, of which one order has
/// several over its life. With the order id alone, the second cleaner taking a two-seat job minted a key
/// the first cleaner's take had already written, the insert hit the unique index at the pipeline's
/// commit, and the whole take rolled back: a 500, and a seat that could never be filled. Crew is
/// <c>ceil(EstimatedTime / 120)</c>, so that is every 180-minute service in the catalogue, not an edge
/// case.</para>
///
/// <para><b>Why the assignment row's id and not the cleaner's.</b> An employee id fixes the two-seat
/// case and the plain reassign, and still collides when an admin undoes their own reassign — A, then B,
/// then back to A mints A's key twice. <c>Order.UnassignEmployee</c> hard-deletes the <c>OrderEmployee</c>
/// row and <c>AddAssignedEmployee</c> creates a fresh one, whose <c>Id</c> is a client-generated ULID
/// available before commit, so the assignment row's own id is unique per assignment EVENT. That is the
/// thing these messages are actually about.</para>
///
/// <para><b>What this deliberately does not do.</b> It does not make every enqueue unique. Two enqueues
/// of the same assignment still collapse onto one row, which is the guarantee the unique index exists
/// for and the reason a random or timestamped discriminator would have been the wrong fix. The order id
/// stays in the subject so a key remains readable in the outbox table during an incident.</para>
///
/// <para><c>MessageKeys.Push</c>'s formula is untouched — it is frozen by ADR-0002 D2.1 and remains a
/// pure function of its inputs. Only what a caller passes as the subject changed.</para>
/// </summary>
public static class AssignmentNotificationSubject
{
    public static string For(string orderId, string assignmentId) => $"{orderId}:{assignmentId}";
}
