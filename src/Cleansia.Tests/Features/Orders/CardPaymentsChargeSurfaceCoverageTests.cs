using System.Text.RegularExpressions;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Every Stripe consumer either honours the card-payments kill switch or is on a reviewed exemption
/// list. This is the guard that the switch itself failed on its first draft.
///
/// <para><b>The failure this exists to stop.</b> <c>Stripe:Enabled</c> was introduced gating three
/// surfaces — web checkout, resume checkout and the mobile PaymentSheet — and its own documentation
/// claimed those were "the three surfaces that CREATE a charge". They were not. Four more existed:
/// <c>ConfirmRecurringOrder</c> (whose own comment called itself "the third charge surface"),
/// <c>CreateMembershipSubscription</c>, <c>CreateMembershipCheckoutSession</c> and
/// <c>SwapMembershipPlan</c>. An operator flipping the switch during an incident would have watched
/// order revenue stop and reasonably concluded card capture had stopped platform-wide, while
/// memberships and every recurring occurrence kept charging. A half-closed kill switch is worse than
/// none, because it is believed.</para>
///
/// <para><b>Why a source scan rather than reflection.</b> The thing under test is a constructor
/// DEPENDENCY, and the failure mode is a developer adding a Stripe call to a handler that does not take
/// the config. Scanning source catches that at the seam where it is introduced, in a message that names
/// the file. Reflection over the built assembly would work equally well for the positive case but reads
/// far less clearly when it fails.</para>
///
/// <para><b>Adding a Stripe call?</b> If it creates a charge, take <c>IStripeConfig</c> and refuse with
/// <c>BusinessErrorMessage.PaymentGatewayUnavailable</c> when it is off. If it returns or releases money
/// — a refund, a cancellation, an erasure — add it to <see cref="Exempt"/> with the reason, and that
/// reason is reviewed rather than assumed.</para>
/// </summary>
public class CardPaymentsChargeSurfaceCoverageTests
{
    private const string ScannedProject = "Cleansia.Core.AppServices";

    /// <summary>
    /// Consumers of Stripe that must keep working while card payments are OFF. Every one of these
    /// RETURNS or RELEASES money, or discharges a legal obligation. Switching payments off is precisely
    /// when they are needed, so gating them would trap customer money behind the incident.
    /// </summary>
    private static readonly (string File, string Why)[] Exempt =
    {
        ("Services/RefundService.cs",
            "Refunds. The whole point of an exemption list — a switch that froze refunds would strand "
            + "money taken before it was flipped."),
        ("Features/Orders/MarkCashCollected.cs",
            "Reads a payment snapshot and CANCELS an uncaptured intent. Releasing an authorisation, "
            + "never creating one."),
        ("Features/Memberships/CancelMembershipSubscription.cs",
            "Cancellation. A customer must always be able to stop being billed."),
        ("Services/GdprDeletionService.cs",
            "Deletes the Stripe customer as part of an erasure request. A legal obligation that cannot "
            + "wait on an ops toggle."),
    };

    private static readonly Regex StripeDependency =
        new(@"\bIStripeClient\b|\bIStripeClientFactory\b", RegexOptions.Compiled);

    private static readonly Regex ConfigDependency = new(@"\bIStripeConfig\b", RegexOptions.Compiled);

    [Fact]
    public void Every_Stripe_Consumer_Is_Gated_Or_Explicitly_Exempt()
    {
        var exempt = Exempt.Select(e => Normalize(e.File)).ToHashSet();

        var ungated = StripeConsumers()
            .Where(f => !exempt.Contains(f.RelativePath))
            .Where(f => !ConfigDependency.IsMatch(f.Text))
            .Select(f => f.RelativePath)
            .OrderBy(p => p)
            .ToList();

        Assert.True(ungated.Count == 0,
            "These files consume Stripe but do not take IStripeConfig, so the card-payments kill switch "
            + "does not close them. If the call CREATES a charge, gate it and refuse with "
            + "BusinessErrorMessage.PaymentGatewayUnavailable. If it RETURNS or RELEASES money, add it to "
            + "CardPaymentsChargeSurfaceCoverageTests.Exempt with the reason:\n  "
            + string.Join("\n  ", ungated));
    }

    /// <summary>
    /// Taking the dependency is not the same as honouring it. A handler could inject
    /// <c>IStripeConfig</c>, satisfy the fact above, and never read it — which would pass a coverage
    /// check while leaving the charge live. So every gated file must also CHECK the switch and refuse
    /// with the shared key.
    ///
    /// <para>This is deliberately a source assertion rather than seven behavioural tests. It pins the
    /// one property that matters across all of them — "the gate is read, and refusing says the right
    /// thing" — at a fraction of the mock-wiring, and it cannot go stale as handlers gain constructor
    /// parameters. <c>CardPaymentsKillSwitchTests</c> and <c>ResumeOrderCheckoutTests</c> carry the
    /// behavioural proof for the two surfaces whose harnesses already existed.</para>
    /// </summary>
    [Fact]
    public void Every_Gated_Consumer_Actually_Reads_The_Switch_And_Refuses_With_The_Shared_Key()
    {
        var exempt = Exempt.Select(e => Normalize(e.File)).ToHashSet();

        var offenders = StripeConsumers()
            .Where(f => !exempt.Contains(f.RelativePath))
            .Where(f => !f.Text.Contains("!stripeConfig.Enabled", StringComparison.Ordinal)
                     || !f.Text.Contains("PaymentGatewayUnavailable", StringComparison.Ordinal))
            .Select(f => f.RelativePath)
            .OrderBy(p => p)
            .ToList();

        Assert.True(offenders.Count == 0,
            "These files take IStripeConfig but do not both check `!stripeConfig.Enabled` and refuse "
            + "with BusinessErrorMessage.PaymentGatewayUnavailable. Injecting the config without "
            + "reading it passes the coverage fact while leaving the charge live:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// Anti-vacuity. The fact above passes trivially on a scanner that reaches no files, which is exactly
    /// how a guard rots into decoration. Pins that the scan finds real files, and that it finds the two
    /// specific shapes it must be able to see: a gated consumer and an exempt one.
    /// </summary>
    [Fact]
    public void The_Scanner_Reaches_Real_Stripe_Consumers()
    {
        var consumers = StripeConsumers();

        Assert.True(consumers.Count >= 10,
            $"Expected the {ScannedProject} scan to find the Stripe consumers but found "
            + $"{consumers.Count} — the scanner, not the codebase, is what regressed.");

        Assert.Contains(consumers, f =>
            f.RelativePath == Normalize("Features/Orders/OrderPaymentDispatcher.cs")
            && ConfigDependency.IsMatch(f.Text));

        Assert.Contains(consumers, f => f.RelativePath == Normalize("Services/RefundService.cs"));
    }

    /// <summary>
    /// An exemption that names a file which no longer consumes Stripe is a stale licence: it would keep
    /// a future file of the same name permanently unguarded. Every entry must still be real.
    /// </summary>
    [Fact]
    public void Every_Exemption_Is_Still_A_Real_Stripe_Consumer()
    {
        var consumers = StripeConsumers().Select(f => f.RelativePath).ToHashSet();

        var stale = Exempt
            .Where(e => !consumers.Contains(Normalize(e.File)))
            .Select(e => e.File)
            .OrderBy(p => p)
            .ToList();

        Assert.True(stale.Count == 0,
            "These exemptions name files that no longer consume Stripe. Remove them — a stale exemption "
            + "silently licenses a future file at the same path:\n  " + string.Join("\n  ", stale));
    }

    private sealed record SourceFile(string RelativePath, string Text);

    private static List<SourceFile> StripeConsumers()
    {
        var root = ProjectRoot();
        return Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(p => new SourceFile(
                Normalize(Path.GetRelativePath(root, p)),
                File.ReadAllText(p)))
            .Where(f => StripeDependency.IsMatch(f.Text))
            .ToList();
    }

    private static string Normalize(string path) =>
        path.Replace('\\', '/');

    // Mirrors the other source-scanning guards — walk up to the *.sln, then across to the project.
    private static string ProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && dir.GetFiles("*.sln").Length == 0)
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "Could not locate the solution directory from the test base directory.");
        return Path.Combine(dir!.FullName, ScannedProject);
    }
}
