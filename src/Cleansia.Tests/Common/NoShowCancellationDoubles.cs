using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Common;

/// <summary>
/// The no-cleaner body over inert collaborators, for customer-cancel suites whose bookings never reach it:
/// each is before its start, has a crew, or is one the unfilled sweep would not cancel.
/// </summary>
public static class NoShowCancellationDoubles
{
    public static CleanerNoShowCancellation Inert() => new(
        Mock.Of<ICreditAccountRepository>(),
        Mock.Of<IRefundService>(),
        Mock.Of<IRefundRepository>(),
        Mock.Of<INotificationProducer>(),
        new GuestOrderAccessTokenIssuer(Mock.Of<IGuestOrderAccessTokenRepository>()),
        Mock.Of<IPendingDispatch>(),
        NullLogger<CleanerNoShowCancellation>.Instance);
}
