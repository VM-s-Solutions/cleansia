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

    [Theory]
    [InlineData(false, 1, true)]
    [InlineData(false, 100, true)]
    [InlineData(false, 101, false)]
    [InlineData(false, 100000, false)]
    [InlineData(false, 0, false)]
    [InlineData(false, -1, false)]
    [InlineData(true, 1, true)]
    [InlineData(true, 100, true)]
    [InlineData(true, 101, false)]
    [InlineData(true, 100000, false)]
    [InlineData(true, 0, false)]
    [InlineData(true, -1, false)]
    public void Every_Order_Page_Admits_At_Most_One_Hundred_Rows(
        bool customer, int limit, bool expectedValid)
    {
        DataRangeRequest request = customer
            ? new GetCustomerOrders.Request { Offset = 79980, Limit = limit }
            : new GetPagedOrders.Request { Offset = 79980, Limit = limit };
        var errors = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(
            request, new ValidationContext(request), errors, validateAllProperties: true);

        Assert.Equal(expectedValid, valid);
        Assert.Equal(limit, request.Limit);
        if (!expectedValid)
        {
            var error = Assert.Single(errors);
            Assert.Contains(nameof(DataRangeRequest.Limit), error.MemberNames);
        }
    }

    [Fact]
    public void Order_Page_Limits_Preserve_The_Default_And_Shared_Mapper_Value()
    {
        Assert.Equal(50, new GetPagedOrders.Request().Limit);
        Assert.Equal(50, new GetCustomerOrders.Request().Limit);

        foreach (var request in new DataRangeRequest[]
                 {
                     new GetPagedOrders.Request { Offset = 79980, Limit = 100 },
                     new GetCustomerOrders.Request { Offset = 79980, Limit = 100 }
                 })
        {
            var page = Array.Empty<string>().MapToDto(80000, request);

            Assert.Equal(100, request.Limit);
            Assert.Equal(100, page.PageSize);
            Assert.Equal(800, page.PageNumber);
            Assert.Equal(80000, page.Total);
        }
    }

    private static bool IsValid(DataRangeRequest request) =>
        Validator.TryValidateObject(request, new ValidationContext(request), [], validateAllProperties: true);
}
