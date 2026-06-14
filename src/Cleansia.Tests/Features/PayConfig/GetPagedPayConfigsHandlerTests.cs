using System.Linq.Expressions;
using System.Reflection;
using Cleansia.Core.AppServices.Features.PayConfig;
using Cleansia.Core.AppServices.Features.PayConfig.DTOs;
using Cleansia.Core.AppServices.Features.PayConfig.Filters;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Sorting.Common;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.PayConfig;

/// <summary>
/// Characterization of the admin pay-config paged list across the A7 accessor
/// (Filter set -> init) and the A6 read-path order (AsNoTracking/Include)
/// canonicalization. Pins the projection + page metadata and that the global-only
/// scoping (employeeId absent => globalOnly) reaches the spec — none of which the
/// canonicalization may alter.
/// </summary>
public class GetPagedPayConfigsHandlerTests
{
    private readonly Mock<IEmployeePayConfigRepository> _repository = new();

    private Task<PagedData<EmployeePayConfigDto>> Handle(GetPagedPayConfigs.Request request)
    {
        var handlerType = typeof(GetPagedPayConfigs).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = Activator.CreateInstance(handlerType, _repository.Object)!;
        var method = handlerType.GetMethod("Handle")!;
        return (Task<PagedData<EmployeePayConfigDto>>)method.Invoke(handler, [request, CancellationToken.None])!;
    }

    private static EmployeePayConfig GlobalServiceConfig()
    {
        var config = EmployeePayConfig.CreateForService(
            serviceId: "svc-1",
            basePay: 300m,
            currencyId: "cur-czk",
            extraPerRoom: 10m,
            extraPerBathroom: 5m,
            distanceRatePerKm: 2m,
            description: "global rate");
        config.SetPayLimits(100m, 900m);
        config.Id = "cfg-1";
        config.Created("system", new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero));
        return config;
    }

    [Fact]
    public async Task Projects_Row_And_PageMetadata()
    {
        var config = GlobalServiceConfig();
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<EmployeePayConfig, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(12);
        _repository
            .Setup(r => r.GetPagedSort<EmployeePayConfigSort>(
                0, 50, It.IsAny<Expression<Func<EmployeePayConfig, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(new[] { config }.AsQueryable().BuildMock());

        var result = await Handle(new GetPagedPayConfigs.Request());

        Assert.Equal(12, result.Total);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(50, result.PageSize);

        var row = Assert.Single(result.Data);
        Assert.Equal("cfg-1", row.Id);
        Assert.Null(row.EmployeeId);
        Assert.Equal("svc-1", row.ServiceId);
        Assert.Equal(300m, row.BasePay);
        Assert.Equal(10m, row.ExtraPerRoom);
        Assert.Equal(5m, row.ExtraPerBathroom);
        Assert.Equal(2m, row.DistanceRatePerKm);
        Assert.Equal(100m, row.MinimumPay);
        Assert.Equal(900m, row.MaximumPay);
        Assert.Equal("cur-czk", row.CurrencyId);
    }

    [Fact]
    public async Task No_EmployeeId_Filter_Scopes_To_Global_Configs()
    {
        Expression<Func<EmployeePayConfig, bool>>? captured = null;
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<EmployeePayConfig, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<EmployeePayConfig, bool>>?, CancellationToken>((f, _) => captured = f)
            .ReturnsAsync(0);
        _repository
            .Setup(r => r.GetPagedSort<EmployeePayConfigSort>(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Expression<Func<EmployeePayConfig, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(Array.Empty<EmployeePayConfig>().AsQueryable().BuildMock());

        await Handle(new GetPagedPayConfigs.Request());

        Assert.NotNull(captured);
        var predicate = captured!.Compile();
        var global = EmployeePayConfig.CreateForService("svc", 1m, "cur");
        var perEmployee = EmployeePayConfig.CreateForService("svc", 1m, "cur", employeeId: "emp-1");
        Assert.True(predicate(global));
        Assert.False(predicate(perEmployee));
    }

    [Fact]
    public async Task EmployeeId_Filter_Scopes_To_That_Employee()
    {
        Expression<Func<EmployeePayConfig, bool>>? captured = null;
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<EmployeePayConfig, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<EmployeePayConfig, bool>>?, CancellationToken>((f, _) => captured = f)
            .ReturnsAsync(0);
        _repository
            .Setup(r => r.GetPagedSort<EmployeePayConfigSort>(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Expression<Func<EmployeePayConfig, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(Array.Empty<EmployeePayConfig>().AsQueryable().BuildMock());

        var request = new GetPagedPayConfigs.Request
        {
            Filter = new PayConfigFilter("emp-1", null, null, null)
        };
        await Handle(request);

        Assert.NotNull(captured);
        var predicate = captured!.Compile();
        var mine = EmployeePayConfig.CreateForService("svc", 1m, "cur", employeeId: "emp-1");
        var other = EmployeePayConfig.CreateForService("svc", 1m, "cur", employeeId: "emp-2");
        var global = EmployeePayConfig.CreateForService("svc", 1m, "cur");
        Assert.True(predicate(mine));
        Assert.False(predicate(other));
        Assert.False(predicate(global));
    }
}
