using System.Reflection;
using Cleansia.Core.AppServices.Features.Packages;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Infra.Common.Validations;
using Moq;

namespace Cleansia.Tests.Features.Packages;

public class UpdatePackageWeightTests
{
    private const string PackageId = "pkg-1";
    private const string ServiceAId = "svc-a";
    private const string ServiceBId = "svc-b";

    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<IServiceRepository> _serviceRepository = new();

    private Package ArrangePackage()
    {
        var package = Package.Create("Bundle", "desc", 100m);
        package.Id = PackageId;
        _packageRepository
            .Setup(r => r.GetByIdAsync(PackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);
        return package;
    }

    private Service ArrangeService(string id)
    {
        var service = Service.Create("cat", $"Service {id}", "desc", 10m, 0m);
        service.Id = id;
        _serviceRepository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);
        return service;
    }

    private async Task<BusinessResult<UpdatePackage.Response>> InvokeHandler(UpdatePackage.Command command)
    {
        var handlerType = typeof(UpdatePackage).GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(handlerType);
        var handler = Activator.CreateInstance(handlerType!, _packageRepository.Object, _serviceRepository.Object)!;
        var handleMethod = handlerType!.GetMethod("Handle");
        Assert.NotNull(handleMethod);
        var task = (Task<BusinessResult<UpdatePackage.Response>>)handleMethod!.Invoke(
            handler, [command, CancellationToken.None])!;
        return await task;
    }

    [Fact]
    public async Task Per_Service_Weights_Persist_On_The_Join()
    {
        var package = ArrangePackage();
        ArrangeService(ServiceAId);
        ArrangeService(ServiceBId);

        var result = await InvokeHandler(new UpdatePackage.Command(
            PackageId,
            "Bundle",
            "desc",
            100m,
            [ServiceAId, ServiceBId],
            new Dictionary<string, decimal> { [ServiceAId] = 3m, [ServiceBId] = 1m },
            null));

        Assert.True(result.IsSuccess);
        Assert.Equal(3m, package.IncludedServices.Single(ps => ps.ServiceId == ServiceAId).PriceWeight);
        Assert.Equal(1m, package.IncludedServices.Single(ps => ps.ServiceId == ServiceBId).PriceWeight);
    }

    [Fact]
    public async Task Omitted_Weight_Falls_Back_To_The_Default_Even_Split()
    {
        var package = ArrangePackage();
        ArrangeService(ServiceAId);
        ArrangeService(ServiceBId);

        var result = await InvokeHandler(new UpdatePackage.Command(
            PackageId,
            "Bundle",
            "desc",
            100m,
            [ServiceAId, ServiceBId],
            new Dictionary<string, decimal> { [ServiceAId] = 5m },
            null));

        Assert.True(result.IsSuccess);
        Assert.Equal(5m, package.IncludedServices.Single(ps => ps.ServiceId == ServiceAId).PriceWeight);
        Assert.Equal(PackageService.DefaultPriceWeight, package.IncludedServices.Single(ps => ps.ServiceId == ServiceBId).PriceWeight);
    }

    [Fact]
    public async Task No_Weights_Supplied_Gives_Every_Service_The_Default_Weight()
    {
        var package = ArrangePackage();
        ArrangeService(ServiceAId);
        ArrangeService(ServiceBId);

        var result = await InvokeHandler(new UpdatePackage.Command(
            PackageId,
            "Bundle",
            "desc",
            100m,
            [ServiceAId, ServiceBId],
            null,
            null));

        Assert.True(result.IsSuccess);
        Assert.All(package.IncludedServices, ps => Assert.Equal(PackageService.DefaultPriceWeight, ps.PriceWeight));
    }

    [Fact]
    public void Detail_Mapper_Returns_The_Stored_Price_Weight()
    {
        var package = Package.Create("Bundle", "desc", 100m);
        var serviceA = Service.Create("cat", "Service A", "desc", 10m, 0m);
        serviceA.Id = ServiceAId;
        var serviceB = Service.Create("cat", "Service B", "desc", 10m, 0m);
        serviceB.Id = ServiceBId;

        package.AddService(serviceA).AddService(serviceB);
        package.IncludedServices.Single(ps => ps.ServiceId == ServiceAId).SetPriceWeight(2m);

        var dto = package.MapToAdminDetail();

        Assert.Equal(2m, dto.IncludedServices.Single(s => s.Id == ServiceAId).PriceWeight);
        Assert.Equal(PackageService.DefaultPriceWeight, dto.IncludedServices.Single(s => s.Id == ServiceBId).PriceWeight);
    }
}
