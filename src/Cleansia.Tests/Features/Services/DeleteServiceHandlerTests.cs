using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Services;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Moq;

namespace Cleansia.Tests.Features.Services;

/// <summary>
/// A Service referenced by an order, a package's included service, or a pay config must not be
/// physically removed (the FK cascade would strip the line item from historical orders and corrupt
/// receipts/pay calc). The guard returns <c>service.in_use</c> instead; an unreferenced service
/// still hard-deletes. Mirrors the proven <c>DeleteCurrency</c> in-use guard.
/// </summary>
public class DeleteServiceHandlerTests
{
    private const string ServiceId = "service-1";

    private readonly Mock<IServiceRepository> _serviceRepository = new();

    private Service ArrangeService()
    {
        var service = Service.Create(
            categoryId: "cat-1",
            name: "Windows",
            description: "Window cleaning",
            basePrice: 100m,
            perRoomPrice: 10m,
            estimatedTime: 30);
        service.Id = ServiceId;

        _serviceRepository
            .Setup(r => r.GetByIdAsync(ServiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);
        return service;
    }

    private DeleteService.Handler CreateHandler() =>
        new(_serviceRepository.Object);

    [Fact]
    public async Task In_Use_Service_Is_Rejected_With_ServiceInUse_And_Not_Removed()
    {
        ArrangeService();
        _serviceRepository
            .Setup(r => r.IsInUseAsync(ServiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = CreateHandler();

        var result = await handler.Handle(new DeleteService.Command(ServiceId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.ServiceInUse, result.Error!.Message);
        _serviceRepository.Verify(r => r.Remove(It.IsAny<Service>()), Times.Never);
    }

    [Fact]
    public async Task Not_In_Use_Service_Is_Removed()
    {
        var service = ArrangeService();
        _serviceRepository
            .Setup(r => r.IsInUseAsync(ServiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = CreateHandler();

        var result = await handler.Handle(new DeleteService.Command(ServiceId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _serviceRepository.Verify(r => r.Remove(service), Times.Once);
    }
}
