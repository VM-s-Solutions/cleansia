namespace Cleansia.Tests.Functions;

/// <summary>
/// A queue consumer runs with no JWT, so the tenant its reads are filtered by and its writes are
/// stamped with is whichever one it sets itself. Every handler that unwraps a <c>QueueEnvelope&lt;T&gt;</c>
/// must therefore call <c>SetTenantOverride(</c> — from the envelope's tenant, or from the row it looked
/// up cross-tenant — before its first tenant-scoped read. One that forgets reads nothing through the
/// filter and fails the NOT NULL stamp on every redelivery; the pay-calc consumer shipped exactly that
/// way and only its own unit tests could have noticed. This walks the handler sources (the
/// <c>SendPushNotificationSeamTripwireTests</c> shape) so the next consumer trips here instead.
/// </summary>
public class QueueConsumerTenantOverrideGuardTests
{
    private const string EnvelopeMarker = "QueueEnvelope<";
    private const string OverrideMarker = "SetTenantOverride(";

    [Fact]
    public void Every_Envelope_Consumer_Sets_The_Tenant_Override()
    {
        var handlersDirectory = Path.Combine(RequireSolutionDirectory(), "Cleansia.Functions.Core", "Handlers");
        var envelopeConsumers = Directory.EnumerateFiles(handlersDirectory, "*Handler.cs", SearchOption.TopDirectoryOnly)
            .Select(file => (Name: Path.GetFileName(file), Text: File.ReadAllText(file)))
            .Where(f => f.Text.Contains(EnvelopeMarker, StringComparison.Ordinal))
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToList();

        var offenders = envelopeConsumers
            .Where(f => !f.Text.Contains(OverrideMarker, StringComparison.Ordinal))
            .Select(f => f.Name)
            .ToList();

        Assert.True(offenders.Count == 0,
            "A queue consumer unwraps a QueueEnvelope<T> but never sets the tenant override. Under the " +
            "Functions host there is no claim, so its tenant-filtered reads see nothing and its first " +
            "stamped write fails NOT NULL on every redelivery. Call tenantProvider.SetTenantOverride(...) " +
            "with the envelope's TenantId (or the tenant of the row looked up cross-tenant) before the " +
            "first tenant-scoped read. Offenders:\n  " + string.Join("\n  ", offenders));

        // Guard against the pin hollowing out: the consumer that shipped without the override must be
        // on the roster, and a rename of the markers must not silently empty it.
        Assert.Contains(envelopeConsumers, f => f.Name == "CalculateOrderPayHandler.cs");
        Assert.True(envelopeConsumers.Count >= 6,
            $"Expected every envelope consumer on the roster; the marker scan found only {envelopeConsumers.Count}.");
    }

    private static string RequireSolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }

        Assert.Fail("Could not locate the solution directory from the test base directory.");
        return string.Empty;
    }
}
