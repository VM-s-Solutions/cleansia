using Cleansia.Core.Domain.Tenancy;
using Cleansia.Infra.Database;

namespace Cleansia.Tests.Infrastructure;

/// <summary>
/// ADR-0064 D3 (TC-LC-ARCH-5) — the archived-company write guard has exactly two ways through for
/// the law's writes: the retention sweeps and an erasure. A third caller of
/// <see cref="IArchiveWriteGate.OpenForLegalObligation"/> would be a hole in the freeze, so the two
/// files are pinned by reading the sources — a grep in a review is advisory, this fails the build.
/// </summary>
public sealed class LegalObligationGateCallSiteTests
{
    private static readonly string[] Callers =
    [
        Path.Combine("Features", "DataRetention", "DataRetentionBackgroundService.cs"),
        Path.Combine("Services", "GdprDeletionService.cs"),
    ];

    [Fact]
    public void Exactly_The_Retention_Sweep_And_The_Erasure_Open_The_Gate()
    {
        var appServices = Path.Combine(SourceRoot(), "Cleansia.Core.AppServices");
        var callers = Directory.EnumerateFiles(appServices, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(file => File.ReadAllText(file).Contains($"{nameof(IArchiveWriteGate.OpenForLegalObligation)}(", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(appServices, file))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(Callers.Order(StringComparer.Ordinal), callers);
    }

    [Fact]
    public void Nothing_Outside_AppServices_Opens_The_Gate_But_Its_Own_Implementation()
    {
        var callersElsewhere = Directory.EnumerateFiles(SourceRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}Cleansia.Core.AppServices{Path.DirectorySeparatorChar}")
                && !file.EndsWith("Tests.cs", StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file).Contains($"{nameof(IArchiveWriteGate.OpenForLegalObligation)}(", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(SourceRoot(), file))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            new[]
            {
                Path.Combine("Cleansia.Core.Domain", "Tenancy", $"{nameof(IArchiveWriteGate)}.cs"),
                Path.Combine("Cleansia.Infra.Database", $"{nameof(ArchiveWriteGate)}.cs"),
            },
            callersElsewhere);
    }

    [Fact]
    public void The_Gate_Is_Closed_Until_Opened_And_Closes_Again_When_The_Last_Opening_Is_Disposed()
    {
        var gate = new ArchiveWriteGate();
        Assert.False(gate.IsOpen);

        var outer = gate.OpenForLegalObligation("retention");
        var inner = gate.OpenForLegalObligation("erasure");
        Assert.True(gate.IsOpen);

        inner.Dispose();
        Assert.True(gate.IsOpen);
        inner.Dispose();
        Assert.True(gate.IsOpen);

        outer.Dispose();
        Assert.False(gate.IsOpen);
    }

    private static string SourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.EnumerateFiles("Cleansia.Api.sln").Any())
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, $"Could not find Cleansia.Api.sln walking up from {AppContext.BaseDirectory}.");
        return dir!.FullName;
    }
}
