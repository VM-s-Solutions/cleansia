using System.ComponentModel.DataAnnotations;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Services;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;

namespace Cleansia.Tests.Features.Orders;

public sealed class OrderListPagingContractTests
{
    private const int MaximumSafeOffset = int.MaxValue - 100000;

    [Theory]
    [InlineData(0, true)]
    [InlineData(500, true)]
    [InlineData(79980, true)]
    [InlineData(MaximumSafeOffset, true)]
    [InlineData(MaximumSafeOffset + 1, false)]
    [InlineData(-1, false)]
    public void Advertised_Deep_Order_Pages_Are_Allowed_Within_Integer_Bounds(int offset, bool expectedValid)
    {
        foreach (var request in new DataRangeRequest[]
                 {
                     new GetPagedOrders.Request { Offset = offset, Limit = 20 },
                     new GetCustomerOrders.Request { Offset = offset, Limit = 20 }
                 })
        {
            Assert.Equal(expectedValid, IsValid(request));
        }
    }

    [Fact]
    public void The_Maximum_Offset_Preserves_Representable_Page_And_Window_Arithmetic()
    {
        var oneRow = new DataRangeRequest { Offset = MaximumSafeOffset, Limit = 1 };
        var fullWindow = new DataRangeRequest { Offset = MaximumSafeOffset, Limit = 100000 };

        Assert.True(IsValid(oneRow));
        Assert.True(IsValid(fullWindow));
        Assert.Equal(MaximumSafeOffset + 1, Array.Empty<string>().MapToDto(0, oneRow).PageNumber);
        Assert.Equal(int.MaxValue, checked(fullWindow.Offset + fullWindow.Limit));
    }

    [Fact]
    public void The_Existing_Thousand_Service_Lookup_Is_Still_Admitted()
    {
        Assert.True(IsValid(new GetPagedServices.Request { Limit = 1000 }));
    }

    private static bool IsValid(DataRangeRequest request) =>
        Validator.TryValidateObject(request, new ValidationContext(request), [], validateAllProperties: true);
}
