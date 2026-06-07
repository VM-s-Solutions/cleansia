using Cleansia.Core.AppServices.Features.SavedAddresses;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cleansia.Tests.Features.SavedAddresses;

/// <summary>
/// A saved address whose shared Address FK is null is a data defect. The handler must not silently
/// swallow it: the orphaned row is kept out of the user's list AND surfaced as a Warning, so ops can
/// observe the defect instead of the user's address vanishing without a trace. Rows with a present
/// Address map normally.
/// </summary>
public class GetSavedAddressesHandlerTests
{
    private const string CallerUserId = "user-1";

    private readonly Mock<ISavedAddressRepository> _savedAddressRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ILogger<GetSavedAddresses.Handler>> _logger = new();

    public GetSavedAddressesHandlerTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CallerUserId);
    }

    private GetSavedAddresses.Handler CreateHandler() =>
        new(_savedAddressRepository.Object, _session.Object, _logger.Object);

    private static SavedAddress SavedWithAddress(string id)
    {
        var address = Address.Create("Main Street 1", "Prague", "11000", "country-1");
        var saved = SavedAddress.Create(CallerUserId, address.Id, "Home", isDefault: true);
        saved.Id = id;
        typeof(SavedAddress).GetProperty(nameof(SavedAddress.Address))!.SetValue(saved, address);
        return saved;
    }

    private static SavedAddress SavedWithNullAddress(string id)
    {
        var saved = SavedAddress.Create(CallerUserId, "orphan-address", "Work", isDefault: false);
        saved.Id = id;
        return saved;
    }

    [Fact]
    public async Task Saved_Address_With_Null_Address_Is_Excluded_And_Logged_As_Warning()
    {
        _savedAddressRepository
            .Setup(r => r.GetByUserAsync(CallerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([SavedWithAddress("saved-ok"), SavedWithNullAddress("saved-orphan")]);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetSavedAddresses.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("saved-ok", result.Value[0].Id);

        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task All_Addresses_Present_Maps_Without_Warning()
    {
        _savedAddressRepository
            .Setup(r => r.GetByUserAsync(CallerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([SavedWithAddress("saved-a"), SavedWithAddress("saved-b")]);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetSavedAddresses.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
