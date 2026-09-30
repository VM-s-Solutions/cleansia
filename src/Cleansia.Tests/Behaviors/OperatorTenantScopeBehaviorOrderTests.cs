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

    private sealed record ScopedListRead(string? CountryId) : IRequest<IReadOnlyList<string>>, IOperatorScopedRequest;

    private static (OperatorTenantScopeBehavior<TRequest, BusinessResult> Behaviour, Mock<ITenantProvider> Tenant, Mock<IOperatorTenantResolver> Resolver)
        Build<TRequest>(string? ambientTenant, OperatorResolution resolution)
        where TRequest : IRequest<BusinessResult> =>
        Build<TRequest, BusinessResult>(ambientTenant, resolution);

    private static (OperatorTenantScopeBehavior<TRequest, TResponse> Behaviour, Mock<ITenantProvider> Tenant, Mock<IOperatorTenantResolver> Resolver)
        Build<TRequest, TResponse>(string? ambientTenant, OperatorResolution resolution)
        where TRequest : IRequest<TResponse>
    {
        var tenant = new Mock<ITenantProvider>();
        tenant.Setup(t => t.GetCurrentTenantId()).Returns(ambientTenant);
        var resolver = new Mock<IOperatorTenantResolver>();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(resolution);
        return (new OperatorTenantScopeBehavior<TRequest, TResponse>(tenant.Object, resolver.Object,
            new Cleansia.Core.AppServices.Features.Orders.GuestOrderAccess(
                Mock.Of<IOrderRepository>(), Mock.Of<IGuestOrderAccessTokenRepository>()),
            new Cleansia.Core.AppServices.Auditing.AuditContext()), tenant, resolver);
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

    [Fact]
    public async Task An_Anonymous_List_Read_Naming_A_Market_Gets_Its_Operator_As_The_Ambient_Tenant()
    {
        var (behaviour, tenant, _) = Build<ScopedListRead, IReadOnlyList<string>>(null, new OperatorResolution(true, "cleansia-sk"));

        var result = await behaviour.Handle(
            new ScopedListRead("SVK"),
            _ => Task.FromResult<IReadOnlyList<string>>(["entry"]),
            CancellationToken.None);

        Assert.Equal(["entry"], result);
        tenant.Verify(t => t.SetTenantOverride("cleansia-sk"), Times.Once);
    }

    /// <summary>
    /// A bare list cannot carry a refusal, and a catalogue read naming a country that is not a market
    /// answers an empty list rather than a 400. So it reaches its handler with no tenant, where the
    /// filter shows it no company's rows.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_Anonymous_List_Read_Naming_No_Operated_Market_Reaches_Its_Handler_Unscoped(bool isMarket)
    {
        var (behaviour, tenant, _) = Build<ScopedListRead, IReadOnlyList<string>>(null, new OperatorResolution(isMarket, null));
        var reachedHandler = false;

        var result = await behaviour.Handle(
            new ScopedListRead("XXX"),
            _ => { reachedHandler = true; return Task.FromResult<IReadOnlyList<string>>([]); },
            CancellationToken.None);

        Assert.True(reachedHandler);
        Assert.Empty(result);
        tenant.Verify(t => t.SetTenantOverride(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// A generic constraint on the behaviour is not a compile error for the open-generic registration:
    /// the container silently leaves it out of the chain for a response type it rejects, and the marker
    /// on that request scopes nothing. That is how the anonymous catalogue read no company's pay
    /// configs. So the container is asked, the way MediatR asks it, for every marked request.
    /// </summary>
    [Fact]
    public void The_Scope_Behaviour_Wraps_Every_Marked_Request()
    {
        var registration = Assert.Single(new ServiceCollection().AddValidators(), d =>
            d.ServiceType == typeof(IPipelineBehavior<,>) && d.ImplementationType == typeof(OperatorTenantScopeBehavior<,>));
        IServiceCollection services = new ServiceCollection();
        services.Add(registration);
        services.AddSingleton(Mock.Of<ITenantProvider>());
        services.AddSingleton(Mock.Of<IOperatorTenantResolver>());
        services.AddSingleton(new Cleansia.Core.AppServices.Features.Orders.GuestOrderAccess(
            Mock.Of<IOrderRepository>(), Mock.Of<IGuestOrderAccessTokenRepository>()));
        services.AddSingleton<Cleansia.Core.AppServices.Auditing.IAuditContext>(new Cleansia.Core.AppServices.Auditing.AuditContext());
        using var provider = services.BuildServiceProvider();

        var unwrapped = typeof(IOperatorScopedRequest).Assembly.GetTypes()
            .Where(t => typeof(IOperatorScopedRequest).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Select(request => (Request: request, Response: request.GetInterfaces()
                .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
                .GetGenericArguments()[0]))
            .Where(pair => !provider
                .GetServices(typeof(IPipelineBehavior<,>).MakeGenericType(pair.Request, pair.Response))
                .Any(behavior => behavior!.GetType().GetGenericTypeDefinition() == typeof(OperatorTenantScopeBehavior<,>)))
            .Select(pair => $"{pair.Request.FullName} -> {pair.Response.Name}")
            .ToList();

        Assert.Empty(unwrapped);
    }

    /// <summary>
    /// Market requests, anonymous session acts and secret-key guest operations are explicitly
    /// enumerated so adding an operator-scoped request requires reviewing its tenant resolution.
    /// </summary>
    [Fact]
    public void Only_The_Listed_Market_Session_And_Guest_Requests_Carry_The_Marker()
    {
        var marked = typeof(IOperatorScopedRequest).Assembly.GetTypes()
            .Where(t => typeof(IOperatorScopedRequest).IsAssignableFrom(t) && !t.IsInterface)
            .Select(t => t.FullName!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
        [
            "Cleansia.Core.AppServices.Features.Auth.AdminLogin+Command",
            "Cleansia.Core.AppServices.Features.Auth.AppleAuth+Command",
            "Cleansia.Core.AppServices.Features.Auth.ConfirmUserEmail+Command",
            "Cleansia.Core.AppServices.Features.Auth.GoogleAuth+Command",
            "Cleansia.Core.AppServices.Features.Auth.Login+Command",
            "Cleansia.Core.AppServices.Features.Auth.MobileLogin+Command",
            "Cleansia.Core.AppServices.Features.Auth.Register+Command",
            "Cleansia.Core.AppServices.Features.Auth.RegisterEmployee+Command",
            "Cleansia.Core.AppServices.Features.Orders.CancelGuestOrder+Command",
            "Cleansia.Core.AppServices.Features.Orders.CreateOrder+Command",
            "Cleansia.Core.AppServices.Features.Orders.GetGuestCancellationFeePreview+Query",
            "Cleansia.Core.AppServices.Features.Orders.LookupOrder+Query",
            "Cleansia.Core.AppServices.Features.Orders.QuoteOrder+Command",
            "Cleansia.Core.AppServices.Features.Orders.QuotePlusSavings+Query",
            "Cleansia.Core.AppServices.Features.Orders.ReportGuestCleanerNoShow+Command",
            "Cleansia.Core.AppServices.Features.Packages.GetPackageOverview+Request",
            "Cleansia.Core.AppServices.Features.PromoCodes.RequestPromoCode+Command",
            "Cleansia.Core.AppServices.Features.Referrals.ValidateReferral+Query",
            "Cleansia.Core.AppServices.Features.Services.GetServiceOverview+Request",
            "Cleansia.Core.AppServices.Features.Users.ChangePassword+Command",
            "Cleansia.Core.AppServices.Features.Users.RequestPasswordChange+Command",
        ], marked);
    }
}
