using Cleansia.Core.AppServices.Features.Packages;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Packages;

/// <summary>
/// Package half of the safe retire path (ADR-0007). Same contract as the service pair:
/// deactivating an IN-USE package is ALLOWED (it only hides the row from new orders; existing
/// orders/carts keep their references, so IsInUseAsync is never consulted), the soft-delete is
/// auditable, Activate reverses it, and both directions are idempotent.
/// </summary>
public class DeactivateActivatePackageHandlerTests
{
    private const string PackageId = "package-1";
    private const string ActorId = "admin-1";

    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<IUserSessionProvider> _userSessionProvider = new();

    private Package ArrangePackage(bool isActive = true)
    {
        var package = Package.Create("Deep Clean", "Full home deep clean", 500m);
        package.Id = PackageId;
        if (!isActive)
        {
            package.Deactivated("seed", DateTimeOffset.UtcNow);
        }

        _packageRepository
            .Setup(r => r.GetByIdAsync(PackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);
        _userSessionProvider
            .Setup(s => s.GetUserId())
            .Returns(ActorId);
        return package;
    }

    [Fact]
    public async Task Deactivate_ActivePackage_SetsInactive_WithAuditActor()
    {
        var package = ArrangePackage();
        var handler = new DeactivatePackage.Handler(_packageRepository.Object, _userSessionProvider.Object);

        var result = await handler.Handle(new DeactivatePackage.Command(PackageId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(package.IsActive);
        Assert.Equal(ActorId, package.DeactivatedBy);
        Assert.NotNull(package.DeactivatedOn);
    }

    [Fact]
    public async Task Deactivate_InUsePackage_IsAllowed_GuardNeverConsulted()
    {
        var package = ArrangePackage();
        _packageRepository
            .Setup(r => r.IsInUseAsync(PackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = new DeactivatePackage.Handler(_packageRepository.Object, _userSessionProvider.Object);

        var result = await handler.Handle(new DeactivatePackage.Command(PackageId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(package.IsActive);
        _packageRepository.Verify(r => r.IsInUseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _packageRepository.Verify(r => r.Remove(It.IsAny<Package>()), Times.Never);
    }

    [Fact]
    public async Task Deactivate_AlreadyInactive_IsIdempotentNoError()
    {
        var package = ArrangePackage(isActive: false);
        var handler = new DeactivatePackage.Handler(_packageRepository.Object, _userSessionProvider.Object);

        var result = await handler.Handle(new DeactivatePackage.Command(PackageId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(package.IsActive);
    }

    [Fact]
    public async Task Activate_InactivePackage_SetsActive()
    {
        var package = ArrangePackage(isActive: false);
        var handler = new ActivatePackage.Handler(_packageRepository.Object);

        var result = await handler.Handle(new ActivatePackage.Command(PackageId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(package.IsActive);
    }

    [Fact]
    public async Task Activate_AlreadyActive_IsIdempotentNoError()
    {
        var package = ArrangePackage();
        var handler = new ActivatePackage.Handler(_packageRepository.Object);

        var result = await handler.Handle(new ActivatePackage.Command(PackageId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(package.IsActive);
    }

    [Fact]
    public async Task Deactivate_Then_Activate_RoundTrip_RestoresActive()
    {
        var package = ArrangePackage();

        await new DeactivatePackage.Handler(_packageRepository.Object, _userSessionProvider.Object)
            .Handle(new DeactivatePackage.Command(PackageId), CancellationToken.None);
        Assert.False(package.IsActive);

        var result = await new ActivatePackage.Handler(_packageRepository.Object)
            .Handle(new ActivatePackage.Command(PackageId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(package.IsActive);
    }
}
