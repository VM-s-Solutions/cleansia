using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Tests.Features.Refunds;

/// <summary>
/// The deterministic idempotency key every refund is issued under.
///
/// <para>It carried two live defects at once, and both were invisible because the one test double
/// that produced a key RESTATED the algorithm instead of calling it — so two of its three branches
/// were never exercised by anything.</para>
///
/// <para><b>The silent no-op.</b> The caller-supplied distinguishing id appeared only in the
/// <c>admin</c> branch. <c>IssuePartialRefund</c> passes its line selection there, and the admin
/// refund screen offers <c>CustomerCancellation</c> and <c>DisputeResolution</c> as reasons — under
/// which the selection was dropped, so a second partial refund on one order built the SAME key,
/// resolved to the first refund's succeeded row, and returned success while no money moved.</para>
///
/// <para><b>The overflow.</b> <c>Refund.RefundKey</c> is <c>[MaxLength(120)]</c>. The raw selection
/// identity was the order id plus every "packageId|serviceId" pair — 94 characters for one
/// standalone line, exactly 120 for one bundled line, and 122+ for any two. Selecting a second line
/// died on the insert with a Postgres 22001 before Stripe was called.</para>
/// </summary>
public class RefundKeyTests
{
    private const int RefundKeyColumnLength = 120;

    // 26-character ULIDs, the real width.
    private const string OrderId = "01M1EAJ8DK0T3V3D7W1E1X5W3M";
    private const string DisputeId = "01M1EAJ8DK0T3V3D7W1E1X5W44";

    private static RefundRequest Request(
        RefundReason reason, string? disputeId = null, string? refundRequestId = null) =>
        new(OrderId, 100m, reason, "admin-1", DisputeId: disputeId, RefundRequestId: refundRequestId);

    // ── every shipped caller's key is unchanged ──────────────────────────────

    [Fact]
    public void CancelOrder_Key_Is_Unchanged()
    {
        // CancelOrder and AdminCancelOrder pass neither optional id. One cancellation per order is
        // the point — this key SHOULD collapse a retry.
        Assert.Equal(
            $"refund:{OrderId}:cancel",
            RefundService.BuildRefundKey(Request(RefundReason.CustomerCancellation)));
    }

    [Fact]
    public void ResolveDispute_Key_Is_Unchanged()
    {
        Assert.Equal(
            $"refund:{OrderId}:dispute:{DisputeId}",
            RefundService.BuildRefundKey(Request(RefundReason.DisputeResolution, disputeId: DisputeId)));
    }

    [Fact]
    public void AdminRefundOrder_Key_Is_Unchanged()
    {
        Assert.Equal(
            $"refund:{OrderId}:admin:full",
            RefundService.BuildRefundKey(Request(RefundReason.AdminDiscretion, refundRequestId: "full")));
    }

    // ── the defect: the distinguishing id must survive EVERY reason ──────────

    [Theory]
    [InlineData(RefundReason.CustomerCancellation)]
    [InlineData(RefundReason.DisputeResolution)]
    [InlineData(RefundReason.AdminDiscretion)]
    [InlineData(RefundReason.ServiceNotRendered)]
    public void Two_Different_Selections_Never_Share_A_Key(RefundReason reason)
    {
        // This is the whole bug. Under CustomerCancellation and DisputeResolution these used to be
        // byte-identical, so the second refund resolved to the first and paid nothing.
        var first = RefundService.BuildRefundKey(Request(reason, refundRequestId: "aaaaaaaaaaaaaaaa"));
        var second = RefundService.BuildRefundKey(Request(reason, refundRequestId: "bbbbbbbbbbbbbbbb"));

        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData(RefundReason.CustomerCancellation)]
    [InlineData(RefundReason.DisputeResolution)]
    [InlineData(RefundReason.AdminDiscretion)]
    public void The_Same_Selection_Always_Collapses_Onto_One_Key(RefundReason reason)
    {
        // The other half of the contract: a retry or a double-submit of the SAME selection must not
        // issue a second refund. Deterministic on the domain inputs, never a Guid or a timestamp.
        Assert.Equal(
            RefundService.BuildRefundKey(Request(reason, refundRequestId: "same-selection")),
            RefundService.BuildRefundKey(Request(reason, refundRequestId: "same-selection")));
    }

    // ── the defect: it has to fit the column ────────────────────────────────

    [Theory]
    [InlineData(RefundReason.CustomerCancellation)]
    [InlineData(RefundReason.DisputeResolution)]
    [InlineData(RefundReason.AdminDiscretion)]
    public void Every_Key_Fits_The_Column(RefundReason reason)
    {
        // IssuePartialRefund now hands over a 16-character fingerprint however many lines were
        // picked, so the longest key this can build is bounded and well inside varchar(120).
        var key = RefundService.BuildRefundKey(
            Request(reason, disputeId: DisputeId, refundRequestId: new string('f', 16)));

        Assert.True(
            key.Length <= RefundKeyColumnLength,
            $"RefundKey is {key.Length} characters; the column holds {RefundKeyColumnLength}: {key}");
    }
}
