using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Disputes;

namespace Cleansia.Tests.Features.Disputes;

/// <summary>
/// AC7 — the <see cref="ResolveDispute.Validator"/> rules, the gap the seam suite (which drives only the
/// handler) leaves open. Pins: a negative <c>RefundAmount</c> → <c>InvalidRefundAmount</c>; null and any
/// <c>≥ 0</c> amount (including exactly 0) pass; the required DisputeId/ResolutionNotes and the notes
/// length cap. Expected messages are the <see cref="BusinessErrorMessage"/> constants, never literals.
/// </summary>
public class ResolveDisputeValidatorTests
{
    private const string DisputeId = "dispute-1";
    private const string Notes = "Resolved after review.";

    private readonly ResolveDispute.Validator _validator = new();

    private static ResolveDispute.Command Command(
        decimal? refundAmount, string disputeId = DisputeId, string notes = Notes) =>
        new(disputeId, refundAmount, notes);

    [Fact]
    public async Task NegativeRefundAmount_FailsWith_InvalidRefundAmount()
    {
        var result = await _validator.ValidateAsync(Command(-0.01m));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidRefundAmount);
    }

    [Fact]
    public async Task LargeNegativeRefundAmount_FailsWith_InvalidRefundAmount()
    {
        var result = await _validator.ValidateAsync(Command(-1000m));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidRefundAmount);
    }

    [Fact]
    public async Task NullRefundAmount_Passes()
    {
        var result = await _validator.ValidateAsync(Command(null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ZeroRefundAmount_Passes_AtTheBoundary()
    {
        // Exactly 0 is the inclusive lower edge of GreaterThanOrEqualTo(0) — a > 0 slip would reject it.
        var result = await _validator.ValidateAsync(Command(0m));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task PositiveRefundAmount_Passes()
    {
        var result = await _validator.ValidateAsync(Command(250m));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task EmptyDisputeId_FailsWith_Required()
    {
        var result = await _validator.ValidateAsync(Command(250m, disputeId: ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(ResolveDispute.Command.DisputeId)
            && e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task EmptyResolutionNotes_FailsWith_Required()
    {
        var result = await _validator.ValidateAsync(Command(250m, notes: ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(ResolveDispute.Command.ResolutionNotes)
            && e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task ResolutionNotes_AtCap_Passes()
    {
        var result = await _validator.ValidateAsync(Command(250m, notes: new string('x', 2000)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ResolutionNotes_OverCap_FailsWith_MaxLengthExceeded()
    {
        var result = await _validator.ValidateAsync(Command(250m, notes: new string('x', 2001)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MaxLengthExceeded);
    }
}
