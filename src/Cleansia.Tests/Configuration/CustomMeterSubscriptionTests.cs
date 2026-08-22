using System.Text.RegularExpressions;
using Cleansia.Config.RateLimiting;
using Cleansia.Core.Clients.Abstractions;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// That the two custom meters are subscribed by the OpenTelemetry pipeline, in BOTH
/// <c>AddServiceDefaults</c> overloads, under the names their declarations actually use.
///
/// <para><b>Why this is a source-text assertion.</b> A <c>MeterProvider</c> emits only the meters
/// explicitly added to it, and the Azure Monitor distro registers no wildcard. Until 2026-08-22
/// <c>AddMeter</c> appeared nowhere in the repository, so every measurement written by
/// <see cref="IntegrationFailureMetrics"/> and <see cref="RateLimitMetrics"/> was recorded in-process
/// and exported nowhere — while both classes' doc comments described them as feeding App Insights.
/// The failure is silent by construction: nothing throws, no counter reads zero, the data simply is
/// not there when someone goes looking during an incident.</para>
///
/// <para><b>Why literals, and why this test.</b> <c>Cleansia.Config</c> references
/// <c>Cleansia.ServiceDefaults</c>, so ServiceDefaults cannot reference back to use
/// <c>RateLimitMetrics.MeterName</c> — the registration has to spell the name out. This project can
/// see both sides, so it is the only place the literal and the constant can be compared. Renaming
/// either constant without editing Extensions.cs fails here rather than in production silence.</para>
///
/// <para>The both-overloads rule has its own history — see
/// <see cref="AppInsightsRemovalGuardTests"/>, which exists because an exporter was once added to the
/// overload no host calls.</para>
/// </summary>
public class CustomMeterSubscriptionTests
{
    private static readonly string Source = ReadServiceDefaults();

    [Theory]
    [InlineData(IntegrationFailureMetrics.MeterName)]
    [InlineData(RateLimitMetrics.MeterName)]
    public void Both_overloads_subscribe_the_meter_under_its_declared_name(string meterName)
    {
        var subscriptions = Regex.Matches(Source, $@"\.AddMeter\(""{Regex.Escape(meterName)}""\)").Count;

        Assert.True(
            subscriptions == 2,
            $"Expected '{meterName}' to be subscribed in BOTH AddServiceDefaults overloads, found "
                + $"{subscriptions} registration(s). One is worse than none: it reads as wired while "
                + "the hosts that use the other overload export nothing.");
    }

    /// <summary>
    /// The floor that stops this class passing vacuously if the file is renamed, emptied, or the
    /// metrics blocks are refactored away.
    /// </summary>
    [Fact]
    public void Both_overloads_still_build_a_metrics_pipeline()
    {
        Assert.Equal(2, Regex.Matches(Source, @"\.WithMetrics\(").Count);
        Assert.Equal(2, Regex.Matches(Source, @"\.AddRuntimeInstrumentation\(\)").Count);
    }

    private static string ReadServiceDefaults()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !directory.EnumerateFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "Could not locate the solution directory from the test base directory.");

        var path = Path.Combine(directory!.FullName, "Cleansia.ServiceDefaults", "Extensions.cs");
        Assert.True(File.Exists(path), $"Expected ServiceDefaults source not found: {path}");
        return File.ReadAllText(path);
    }
}
