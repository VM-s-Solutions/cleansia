using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Packages;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Cleansia.Tests.Features.Packages;

/// <summary>
/// A Package referenced by an order or a pay config must not be physically removed (the FK cascade
/// would strip the line item from historical orders and corrupt receipts/pay calc). The guard
/// returns <c>package.in_use</c> instead; an unreferenced package still hard-deletes. Mirrors the
/// proven <c>DeleteCurrency</c> in-use guard.
/// </summary>
public class DeletePackageHandlerTests
{
    private const string PackageId = "package-1";

    private readonly Mock<IPackageRepository> _packageRepository = new();

    private Package ArrangePackage()
    {
        var package = Package.Create("Deluxe", "Deluxe bundle", 500m);
        package.Id = PackageId;

        _packageRepository
            .Setup(r => r.GetByIdAsync(PackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);
        return package;
    }

    private DeletePackage.Handler CreateHandler() =>
        new(_packageRepository.Object);

    [Fact]
    public async Task In_Use_Package_Is_Rejected_With_PackageInUse_And_Not_Removed()
    {
        ArrangePackage();
        _packageRepository
            .Setup(r => r.IsInUseAsync(PackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = CreateHandler();

        var result = await handler.Handle(new DeletePackage.Command(PackageId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.PackageInUse, result.Error!.Message);
        _packageRepository.Verify(r => r.Remove(It.IsAny<Package>()), Times.Never);
    }

    [Fact]
    public async Task Not_In_Use_Package_Is_Removed()
    {
        var package = ArrangePackage();
        _packageRepository
            .Setup(r => r.IsInUseAsync(PackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = CreateHandler();

        var result = await handler.Handle(new DeletePackage.Command(PackageId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _packageRepository.Verify(r => r.Remove(package), Times.Once);
    }

    // The durable TOCTOU backstop: the pre-check passes (a concurrent reference is inserted in the race
    // window), but the FLUSH hits the catalog FK's ON DELETE RESTRICT. The handler maps that restrict
    // violation to PackageInUse instead of letting a raw DbUpdateException reach the pipeline as a 500.
    [Theory]
    [InlineData("23001")] // restrict_violation (explicit ON DELETE RESTRICT)
    [InlineData("23503")] // foreign_key_violation (NO ACTION)
    public async Task Reference_Racing_Past_The_Check_Maps_Restrict_Violation_To_PackageInUse(string sqlState)
    {
        ArrangePackage();
        _packageRepository
            .Setup(r => r.IsInUseAsync(PackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _packageRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("fk", new FakePostgresException(sqlState)));
        var handler = CreateHandler();

        var result = await handler.Handle(new DeletePackage.Command(PackageId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.PackageInUse, result.Error!.Message);
    }

    private sealed class FakePostgresException(string sqlState) : Exception
    {
        public string SqlState { get; } = sqlState;
    }
}
