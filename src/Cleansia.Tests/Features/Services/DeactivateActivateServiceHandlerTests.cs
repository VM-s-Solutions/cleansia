using Cleansia.Core.AppServices.Features.Services;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Moq;

namespace Cleansia.Tests.Features.Services;

/// <summary>
/// The safe retire path (ADR-0007). Deactivate soft-retires via the auditable domain
/// primitive (IsActive=false + DeactivatedBy/On; the customer overview's Where(IsActive) hides it,
/// history survives); Activate reverses it. Contract: deactivating an IN-USE service is ALLOWED —
/// unlike delete it only hides the row from new orders, existing orders/carts keep their
/// references — so the handler never consults the IsInUseAsync guard. Both directions are
/// idempotent.
/// </summary>
public class DeactivateActivateServiceHandlerTests
{
    private const string ServiceId = "service-1";
    private const string ActorId = "admin-1";

    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IUserSessionProvider> _userSessionProvider = new();

    private Service ArrangeService(bool isActive = true)
    {
        var service = Service.Create(
            categoryId: "cat-1",
            name: "Windows",
            description: "Window cleaning",
            basePrice: 100m,
            perRoomPrice: 10m,
            estimatedTime: 30);
        service.Id = ServiceId;
        if (!isActive)
        {
            service.Deactivated("seed", DateTimeOffset.UtcNow);
        }

        _serviceRepository
            .Setup(r => r.GetByIdAsync(ServiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);
        _userSessionProvider
            .Setup(s => s.GetUserId())
            .Returns(ActorId);
        return service;
    }

    [Fact]
    public async Task Deactivate_ActiveService_SetsInactive_WithAuditActor()
    {
        var service = ArrangeService();
        var handler = new DeactivateService.Handler(_serviceRepository.Object, _userSessionProvider.Object);

        var result = await handler.Handle(new DeactivateService.Command(ServiceId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(service.IsActive);
        Assert.Equal(ActorId, service.DeactivatedBy);
        Assert.NotNull(service.DeactivatedOn);
    }

    [Fact]
    public async Task Deactivate_InUseService_IsAllowed_GuardNeverConsulted()
    {
        var service = ArrangeService();
        _serviceRepository
            .Setup(r => r.IsInUseAsync(ServiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = new DeactivateService.Handler(_serviceRepository.Object, _userSessionProvider.Object);

        var result = await handler.Handle(new DeactivateService.Command(ServiceId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(service.IsActive);
        _serviceRepository.Verify(r => r.IsInUseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _serviceRepository.Verify(r => r.Remove(It.IsAny<Service>()), Times.Never);
    }

    [Fact]
    public async Task Deactivate_AlreadyInactive_IsIdempotentNoError()
    {
        var service = ArrangeService(isActive: false);
        var handler = new DeactivateService.Handler(_serviceRepository.Object, _userSessionProvider.Object);

        var result = await handler.Handle(new DeactivateService.Command(ServiceId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(service.IsActive);
    }

    [Fact]
    public async Task Activate_InactiveService_SetsActive()
    {
        var service = ArrangeService(isActive: false);
        var handler = new ActivateService.Handler(_serviceRepository.Object);

        var result = await handler.Handle(new ActivateService.Command(ServiceId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(service.IsActive);
    }

    [Fact]
    public async Task Activate_AlreadyActive_IsIdempotentNoError()
    {
        var service = ArrangeService();
        var handler = new ActivateService.Handler(_serviceRepository.Object);

        var result = await handler.Handle(new ActivateService.Command(ServiceId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(service.IsActive);
    }

    [Fact]
    public async Task Deactivate_Then_Activate_RoundTrip_RestoresActive()
    {
        var service = ArrangeService();

        await new DeactivateService.Handler(_serviceRepository.Object, _userSessionProvider.Object)
            .Handle(new DeactivateService.Command(ServiceId), CancellationToken.None);
        Assert.False(service.IsActive);

        var result = await new ActivateService.Handler(_serviceRepository.Object)
            .Handle(new ActivateService.Command(ServiceId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(service.IsActive);
    }
}
