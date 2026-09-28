using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using Moq;
using AppConstants = Cleansia.Core.AppServices.Common.Constants;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The handler removes the row after the delete, and the URL is the only name the blob has — so a delete
/// sent to a name that still carries the container reports nothing, the row goes, and the file is orphaned
/// for good. Azure serves <c>/&lt;container&gt;/&lt;blob&gt;</c> and Azurite (DEV)
/// <c>/&lt;account&gt;/&lt;container&gt;/&lt;blob&gt;</c>; both must reach the same container-relative name.
/// </summary>
public sealed class DeleteOrderPhotoBlobNameTests
{
    private const string OrderId = "order-delete-photo";
    private const string EmployeeId = "emp-delete-photo";

    [Theory]
    [InlineData($"https://account.blob.core.windows.net/order-photos/2026/{OrderId}/after.jpg")]
    [InlineData($"http://127.0.0.1:10000/devstoreaccount1/order-photos/2026/{OrderId}/after.jpg")]
    public async Task The_Blob_Is_Deleted_By_Its_Name_Inside_The_Container(string blobUrl)
    {
        var photo = OrderPhoto.Create(OrderId, PhotoType.After, blobUrl, "after.jpg", "after.jpg", 1024, "image/jpeg", EmployeeId);
        var photos = new Mock<IOrderPhotoRepository>();
        photos.Setup(r => r.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(photo);

        var order = Order.Create("Customer", "customer@example.com", "+420000000000",
            Address.Create("Dlouha 1", "Praha", "11000", "cz"), 1, 1, DateTime.UtcNow.AddDays(1),
            PaymentType.Cash, 1000m, "czk", PaymentStatus.Pending);
        order.Id = OrderId;
        order.AddAssignedEmployee(OrderEmployee.Create(order, ValidatorTestHelpers.BuildEmployee(EmployeeId, ContractStatus.Approved)));
        var orders = new Mock<IOrderRepository>();
        orders.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());

        var access = new Mock<IOrderAccessService>();
        access.Setup(a => a.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EmployeeId);

        var blobClient = new Mock<IBlobContainerClient>();
        var blobs = new Mock<IBlobContainerClientFactory>();
        blobs.Setup(f => f.GetBlobContainerClient(AppConstants.BlobContainers.OrderPhotos)).Returns(blobClient.Object);

        var result = await new DeleteOrderPhoto.Handler(photos.Object, orders.Object, access.Object, blobs.Object)
            .Handle(new DeleteOrderPhoto.Command(photo.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        blobClient.Verify(c => c.DeleteAsync($"2026/{OrderId}/after.jpg", It.IsAny<CancellationToken>()), Times.Once);
        photos.Verify(r => r.Remove(photo), Times.Once);
    }
}
