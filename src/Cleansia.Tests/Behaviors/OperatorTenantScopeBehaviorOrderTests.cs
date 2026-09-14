using Cleansia.Config.Validation;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cleansia.Tests.Behaviors;

/// <summary>
/// ADR-0061 D3 — the anonymous scope runs BEFORE validation, because the validators' filtered
/// pre-checks are the first tenanted reads of a market-scoped request; and it steps aside when a claim
/// exists, because a claim is the tenant and the request can never override it (S1).
/// </summary>
public sealed class OperatorTenantScopeBehaviorOrderTests
{
    private static List<Type?> RegisteredBehaviorTypes() =>
        new ServiceCollection().AddValidators()
            .Where(d => d.ServiceType == typeof(IPipelineBehavior<,>))
            .Select(d => d.ImplementationType)
            .ToList();

    [Fact]
    public void The_Scope_Behaviour_Is_Registered_After_PostCommitDispatch_And_Before_Validation()
    {
        var behaviorTypes = RegisteredBehaviorTypes();

        var postCommit = behaviorTypes.IndexOf(typeof(PostCommitDispatchBehavior<,>));
        var scope = behaviorTypes.IndexOf(typeof(OperatorTenantScopeBehavior<,>));
        var validation = behaviorTypes.IndexOf(typeof(ValidationPipelineBehavior<,>));

        Assert.True(scope >= 0, "OperatorTenantScopeBehavior must be registered.");
        Assert.True(postCommit < scope, "OperatorTenantScopeBehavior must follow PostCommitDispatch.");
        Assert.True(
            scope < validation,
            "ADR-0061 D3: OperatorTenantScopeBehavior must be OUTER to ValidationPipelineBehavior so the "
            + "market's operator is the ambient tenant before the validators' filtered pre-checks run.");
    }

    private sealed record ScopedCommand(string? CountryId) : ICommand, IOperatorScopedRequest;

    private sealed record PlainCommand : ICommand;

    private static (OperatorTenantScopeBehavior<TRequest, BusinessResult> Behaviour, Mock<ITenantProvider> Tenant, Mock<IOperatorTenantResolver> Resolver)
        Build<TRequest>(string? ambientTenant, OperatorResolution resolution)
        where TRequest : IRequest<BusinessResult>
    {
        var tenant = new Mock<ITenantProvider>();
        tenant.Setup(t => t.GetCurrentTenantId()).Returns(ambientTenant);
        var resolver = new Mock<IOperatorTenantResolver>();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(resolution);
        return (new OperatorTenantScopeBehavior<TRequest, BusinessResult>(tenant.Object, resolver.Object), tenant, resolver);
    }

    [Fact]
    public async Task A_Request_With_A_Claim_Never_Consults_The_Resolver_And_Keeps_The_Claim()
    {
        var (behaviour, tenant, resolver) = Build<ScopedCommand>("cleansia-cz", new OperatorResolution(true, "cleansia-sk"));

        var result = await behaviour.Handle(new ScopedCommand("SVK"), _ => Task.FromResult(BusinessResult.Success()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        resolver.Verify(r => r.ResolveAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        tenant.Verify(t => t.SetTenantOverride(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task A_Request_Without_The_Marker_Is_Passed_Through_Untouched()
    {
        var (behaviour, tenant, resolver) = Build<PlainCommand>(null, new OperatorResolution(true, "cleansia-cz"));

        var result = await behaviour.Handle(new PlainCommand(), _ => Task.FromResult(BusinessResult.Success()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        resolver.Verify(r => r.ResolveAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        tenant.Verify(t => t.SetTenantOverride(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task An_Anonymous_Request_Naming_A_Market_Gets_Its_Operator_As_The_Ambient_Tenant()
    {
        var (behaviour, tenant, resolver) = Build<ScopedCommand>(null, new OperatorResolution(true, "cleansia-sk"));
        var reachedHandler = false;

        var result = await behaviour.Handle(
            new ScopedCommand("SVK"),
            _ => { reachedHandler = true; return Task.FromResult(BusinessResult.Success()); },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(reachedHandler);
        resolver.Verify(r => r.ResolveAsync("SVK", It.IsAny<CancellationToken>()), Times.Once);
        tenant.Verify(t => t.SetTenantOverride("cleansia-sk"), Times.Once);
    }

    [Fact]
    public async Task A_Country_That_Is_Not_A_Market_Is_Refused_As_User_Input_Before_The_Handler()
    {
        var (behaviour, tenant, _) = Build<ScopedCommand>(null, OperatorResolution.NotAMarket);
        var reachedHandler = false;

        var result = await behaviour.Handle(
            new ScopedCommand("XXX"),
            _ => { reachedHandler = true; return Task.FromResult(BusinessResult.Success()); },
            CancellationToken.None);

        Assert.False(reachedHandler);
        var failure = Assert.IsType<ValidationResult>(result);
        var error = Assert.Single(failure.Errors);
        Assert.Equal(nameof(IOperatorScopedRequest.CountryId), error.Code);
        Assert.Equal(BusinessErrorMessage.CountryNotServiced, error.Message);
        tenant.Verify(t => t.SetTenantOverride(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task A_Market_Nobody_Operates_Is_Refused_As_A_Configuration_Defect_Before_The_Handler()
    {
        var (behaviour, tenant, _) = Build<ScopedCommand>(null, new OperatorResolution(true, null));
        var reachedHandler = false;

        var result = await behaviour.Handle(
            new ScopedCommand("POL"),
            _ => { reachedHandler = true; return Task.FromResult(BusinessResult.Success()); },
            CancellationToken.None);

        Assert.False(reachedHandler);
        var failure = Assert.IsType<ValidationResult>(result);
        var error = Assert.Single(failure.Errors);
        Assert.Equal(BusinessErrorMessage.TenantNotFound, error.Message);
        tenant.Verify(t => t.SetTenantOverride(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// The nine requests of ADR-0061 D3 carry the marker, plus the five session acts that name no
    /// market and write a tenanted audit row on refusal (ADR-0062, Q-AUD-L5 overruled: the sign-ins,
    /// the e-mail confirmation and the password reset pair — default market, the account's own company
    /// replacing it on the token, D4). Nothing else does: a grep is the roster, and this is the grep.
    /// </summary>
    [Fact]
    public void Exactly_The_D3_Requests_And_The_Session_Acts_Carry_The_Marker()
    {
        var marked = typeof(IOperatorScopedRequest).Assembly.GetTypes()
            .Where(t => typeof(IOperatorScopedRequest).IsAssignableFrom(t) && !t.IsInterface)
            .Select(t => t.FullName!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
        [
            "Cleansia.Core.AppServices.Features.Auth.AppleAuth+Command",
            "Cleansia.Core.AppServices.Features.Auth.ConfirmUserEmail+Command",
            "Cleansia.Core.AppServices.Features.Auth.GoogleAuth+Command",
            "Cleansia.Core.AppServices.Features.Auth.Login+Command",
            "Cleansia.Core.AppServices.Features.Auth.MobileLogin+Command",
            "Cleansia.Core.AppServices.Features.Auth.Register+Command",
            "Cleansia.Core.AppServices.Features.Auth.RegisterEmployee+Command",
            "Cleansia.Core.AppServices.Features.Orders.CreateOrder+Command",
            "Cleansia.Core.AppServices.Features.Orders.QuoteOrder+Command",
            "Cleansia.Core.AppServices.Features.Orders.QuotePlusSavings+Query",
            "Cleansia.Core.AppServices.Features.PromoCodes.RequestPromoCode+Command",
            "Cleansia.Core.AppServices.Features.Referrals.ValidateReferral+Query",
            "Cleansia.Core.AppServices.Features.Users.ChangePassword+Command",
            "Cleansia.Core.AppServices.Features.Users.RequestPasswordChange+Command",
        ], marked);
    }
}
