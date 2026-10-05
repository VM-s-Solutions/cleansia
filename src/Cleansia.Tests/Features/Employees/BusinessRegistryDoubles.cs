using Cleansia.Infra.Services.BusinessRegistry;
using Moq;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// The public business register as the cleaner rules read it. Suites about something else take
/// <see cref="NotConsulted"/>, so a registration number is judged on the suite's own terms alone.
/// </summary>
internal static class BusinessRegistryDoubles
{
    public static IBusinessRegistry NotConsulted() => Answering(BusinessRegistryRecord.NotConsulted).Object;

    public static Mock<IBusinessRegistry> Answering(BusinessRegistryRecord record)
    {
        var registry = new Mock<IBusinessRegistry>();
        registry
            .Setup(r => r.LookupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);
        return registry;
    }

    public static BusinessRegistryRecord InForce() =>
        new(BusinessRegistryAnswer.Registered, Ceased: false, TradeLicenceActive: true);
}
