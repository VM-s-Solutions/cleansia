using System.Text.RegularExpressions;
using Cleansia.Config;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Cleansia.IntegrationTests.Features.Payments.Webhooks;

/// <summary>
/// The structural "signature-stays-on" lock for the webhook surface, mirroring the audit's
/// "remove IsDevelopment bypass" lock for server-side token paths. Two complementary guards:
///   • a MECHANICAL source assertion that <c>HandlePaymentNotification</c>'s signature path
///     (<c>EventUtility.ConstructEvent</c>) is NOT gated behind any <c>IsDevelopment()</c> /
///     environment / config-flag branch — so a future "make local testing easier" change that
///     short-circuits verification fails this test at the source level;
///   • a BEHAVIORAL assertion that, with the host configured as <c>Development</c>, an unsigned event is
///     STILL rejected exactly as in any other environment.
///
/// This test builds its OWN provider with a Development <see cref="IHostEnvironment"/> rather than
/// editing the shared <c>BaseIntegrationTest</c> harness, keeping that harness backward-compatible. It
/// does not touch the DB beyond resolving the mediator pipeline; the unsigned event is rejected by the
/// validator before any handler/DB work.
/// </summary>
[Collection("PostgresCollection")]
public class WebhookSignatureLockTests : BaseIntegrationTest
{
    public WebhookSignatureLockTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public void SignaturePath_HasNoEnvironmentOrConfigFlagBypass()
    {
        var source = ReadHandlerSource();

        // The signature is verified by EventUtility.ConstructEvent — it must be present (the lock has a
        // surface to protect).
        Assert.Contains("EventUtility.ConstructEvent", source);

        // No environment / config bypass anywhere in the webhook handler: an IsDevelopment()-gated skip
        // (the IDA-SEC-01 failure mode) would let a forged event mint paid orders + free memberships.
        Assert.DoesNotContain("IsDevelopment", source);
        Assert.DoesNotContain("IsStaging", source);
        Assert.DoesNotContain("EnvironmentName", source);

        // And ConstructEvent is never invoked conditionally on an environment/flag: assert there is no
        // `if (... ) { ... ConstructEvent ... }` form guarded by an env/flag token on the same branch.
        var conditionalConstruct = Regex.IsMatch(
            source,
            @"if\s*\([^)]*(IsDevelopment|Environment|BypassSignature|SkipSignature|DisableSignature)[^)]*\)",
            RegexOptions.IgnoreCase);
        Assert.False(conditionalConstruct, "Signature verification must not be environment/flag-gated.");
    }

    [Fact]
    public async Task InDevelopmentEnvironment_UnsignedEvent_IsStillRejected()
    {
        await using var provider = BuildDevelopmentProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var body = StripeWebhookTestPayloads.CheckoutSessionCompletedBody("evt_dev_unsigned", "order-does-not-matter");
        BusinessResult<string> result = await mediator.Send(new HandlePaymentNotification.Command(body, string.Empty));

        // Development env makes no difference: an unsigned event is rejected exactly as in AC4.
        Assert.True(result.IsFailure);
    }

    private ServiceProvider BuildDevelopmentProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddSingleton(Configuration);

        // The DELIBERATE deviation from BaseIntegrationTest.Setup: a Development host environment, to
        // prove the signature check is not environment-gated.
        var developmentEnvironment = new TestHostEnvironment { EnvironmentName = Environments.Development };
        services.AddCoreBindings(Configuration, developmentEnvironment);

        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(
            _ => new TestUserSessionProvider(new TestClaimsPrincipalUser())));
        services.AddSingleton<IHostAudienceProvider>(new HostAudienceProvider(JwtAudiences.Customer));
        services.Replace(ServiceDescriptor.Singleton<IDatabaseConnectionString>(
            _ => new DatabaseConnectionString(Configuration)
            {
                ConnectionString = Fixture.GetConnectionString()
            }));

        return services.BuildServiceProvider();
    }

    private static string ReadHandlerSource()
    {
        var path = ResolveRepoRelativePath(
            "src", "Cleansia.Core.AppServices", "Features", "Payments", "HandlePaymentNotification.cs");
        return File.ReadAllText(path);
    }

    private static string ResolveRepoRelativePath(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate {Path.Combine(segments)} walking up from {AppContext.BaseDirectory}.");
    }
}
