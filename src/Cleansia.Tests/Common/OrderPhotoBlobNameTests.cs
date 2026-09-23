using Cleansia.Core.AppServices.Common;

namespace Cleansia.Tests.Common;

/// <summary>
/// The photo retention sweep deletes a blob by the name this returns, so a name that still carries the
/// container addresses a blob that does not exist and the delete reports nothing: the file would outlive
/// its row with nothing left able to name it.
/// </summary>
public sealed class OrderPhotoBlobNameTests
{
    [Theory]
    [InlineData("https://account.blob.core.windows.net/order-photos/2026/order-1/after.jpg")]
    [InlineData("http://127.0.0.1:10000/devstoreaccount1/order-photos/2026/order-1/after.jpg")]
    public void The_Name_Is_Relative_To_The_Container_On_Azure_And_On_Azurite(string blobUrl)
    {
        Assert.Equal("2026/order-1/after.jpg", OrderPhotoBlobName.FromUrl(blobUrl));
    }
}
