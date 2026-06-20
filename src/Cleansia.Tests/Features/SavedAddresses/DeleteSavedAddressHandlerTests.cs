using Cleansia.Core.AppServices.Features.SavedAddresses;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.SavedAddresses;

/// <summary>
/// Deleting a saved address is a soft-delete: the handler routes through
/// <see cref="ISavedAddressRepository.Deactivate"/> so the row survives with its audit trail, and
/// never through the physical <see cref="ISavedAddressRepository.Remove"/>. The repository owns the
/// <c>IsActive</c>/<c>DeactivatedOn</c> stamping; the handler must not set those flags itself.
/// </summary>
public class DeleteSavedAddressHandlerTests
{
    private const string SavedAddressId = "saved-1";
    private const string CallerUserId = "user-1";

    private readonly Mock<ISavedAddressRepository> _savedAddressRepository = new();

    private SavedAddress ArrangeSaved()
    {
        var saved = SavedAddress.Create(
            userId: CallerUserId,
            addressId: "address-1",
            label: "Home",
            isDefault: false);
        saved.Id = SavedAddressId;

        _savedAddressRepository
            .Setup(r => r.GetByIdAsync(SavedAddressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(saved);
        return saved;
    }

    private DeleteSavedAddress.Handler CreateHandler() =>
        new(_savedAddressRepository.Object);

    [Fact]
    public async Task Deleting_Saved_Address_Deactivates_And_Does_Not_Remove()
    {
        var saved = ArrangeSaved();
        var handler = CreateHandler();

        var result = await handler.Handle(new DeleteSavedAddress.Command(SavedAddressId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SavedAddressId, result.Value!.SavedAddressId);
        _savedAddressRepository.Verify(r => r.Deactivate(saved), Times.Once);
        _savedAddressRepository.Verify(r => r.Remove(It.IsAny<SavedAddress>()), Times.Never);
    }

    [Fact]
    public async Task Handler_Does_Not_Commit_Or_Flip_IsActive_Itself()
    {
        var saved = ArrangeSaved();
        var handler = CreateHandler();

        await handler.Handle(new DeleteSavedAddress.Command(SavedAddressId), CancellationToken.None);

        Assert.True(saved.IsActive);
        _savedAddressRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
