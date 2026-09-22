using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Legal;
using Cleansia.TestUtilities;
using Moq;

namespace Cleansia.Tests.Features.Orders;

internal static class WorkContractResolvers
{
    /// <summary>A resolver with <see cref="WorkContractTestData.Document"/> in force for every market, or nothing at all.</summary>
    public static Mock<ILegalDocumentResolver> Resolver(bool inForce = true)
    {
        var resolver = new Mock<ILegalDocumentResolver>();
        resolver
            .Setup(r => r.ResolveInForceAsync(LegalDocumentType.WorkContract, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inForce ? WorkContractTestData.Document() : null);
        return resolver;
    }
}
