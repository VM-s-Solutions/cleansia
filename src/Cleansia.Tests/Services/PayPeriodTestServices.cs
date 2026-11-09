using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cleansia.Tests.Services;

internal static class PayPeriodTestServices
{
    public static IServiceScopeFactory ScopeFactory(Func<PayPeriodBackgroundService> serviceFactory)
    {
        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(() =>
        {
            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(PayPeriodBackgroundService))).Returns(serviceFactory);
            provider.Setup(p => p.GetService(typeof(ITenantProvider))).Returns(Mock.Of<ITenantProvider>());
            var scope = new Mock<IServiceScope>();
            scope.SetupGet(s => s.ServiceProvider).Returns(provider.Object);
            return scope.Object;
        });
        return factory.Object;
    }

    public static PayPeriodClosingConfig ClosingConfig(int? lengthInDays = null) =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(lengthInDays is null
                ? []
                : [new KeyValuePair<string, string?>("PayPeriodClosing:LengthInDays", lengthInDays.ToString())])
            .Build());
}
