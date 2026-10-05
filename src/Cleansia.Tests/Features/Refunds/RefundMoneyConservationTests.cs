using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StripeException = Stripe.StripeException;

namespace Cleansia.Tests.Features.Refunds;

/// <summary>
/// Random card-and-credit orders taken through partial refunds, card disputes and complaints settled in
/// credit, then ended by a member's cancellation, an admin's full refund, a no-show or a platform
/// cancellation while Stripe pays, is unreachable or refuses, and finally re-driven hourly. The real refund
/// seam and cancellations run over an in-memory ledger in which Stripe keeps what it was first asked on a
/// key and refuses that key with another amount. What came back, the card refunds Stripe made plus the
/// credit returned plus the settlements, never passes the price, and an order still paid in full when it
/// ends gets back the price to the haléř. Once the re-drives have run, every refund Stripe made is on record:
/// a retry that asks another amount on a key Stripe already paid is refused there every time.
/// </summary>
public sealed class RefundMoneyConservationTests
{
    private const int Orders = 500;

    // A re-drive reads its row's slice of the sale back from the card amount, which can lose one haléř.
    private const decimal ReadBackRounding = 0.01m;

    [Fact]
    public async Task No_Order_Gives_Back_More_Than_Was_Paid_And_An_Order_Ended_While_Paid_Gives_Back_All_Of_It()
    {
        var random = new Random(20261005);
        var over = new List<string>();
        var under = new List<string>();
        var unrecorded = new List<string>();
        for (var i = 0; i < Orders; i++)
        {
            var world = new World(random, $"order-{i}");
            await world.RunAsync();
            if (world.Received > world.Paid)
            {
                over.Add(world.Describe());
            }
            else if (world.MustGiveBackAll && world.Paid - world.Received > ReadBackRounding)
            {
                under.Add(world.Describe());
            }

            if (world.LeftAStripeRefundUnrecorded)
            {
                unrecorded.Add(world.Describe());
            }
        }

        Assert.True(over.Count == 0, $"{over.Count} orders gave back more than was paid:\n{string.Join('\n', over.Take(10))}");
        Assert.True(under.Count == 0, $"{under.Count} orders gave back less than was paid:\n{string.Join('\n', under.Take(10))}");
        Assert.True(unrecorded.Count == 0,
            $"{unrecorded.Count} orders left a refund Stripe made unrecorded:\n{string.Join('\n', unrecorded.Take(10))}");
    }

    private enum StripeAnswer { Pays, Unreachable, Refuses, PaysThenTimesOut }

    private enum Ending { MemberCancellation, AdminFullRefund, NoShow, PlatformCancellation }

    private sealed class RefundRow
    {
        public required string Id { get; init; }
        public required string Key { get; init; }
        public required RefundReason Reason { get; init; }
        public string? DisputeId { get; init; }
        public decimal Amount { get; set; }
        public RefundStatus Status { get; set; }
    }

    private sealed class World
    {
        private const string UserId = "user-conservation";

        private readonly Random _random;
        private readonly List<RefundRow> _rows = [];
        private readonly List<(string Key, CreditTransactionReason Reason, decimal Amount)> _credit = [];
        private readonly Dictionary<string, decimal> _stripeRefunded = [];
        private readonly Dictionary<string, RefundRequest> _asked = [];
        private readonly List<string> _steps = [];
        private readonly Mock<IStripeClient> _stripe = new();
        private readonly Mock<ICreditAccountRepository> _creditRepository = new();
        private StripeAnswer _nextAnswer = StripeAnswer.Pays;
        private bool _loseNextSucceededRecord;
        private int _actions;

        public World(Random random, string orderId)
        {
            _random = random;
            var total = (decimal)random.Next(100, 5001);
            var credit = random.Next(5) == 0
                ? 0m
                : random.Next(1, (int)Math.Floor(total * BookingPolicy.MaxCreditShareOfOrder) + 1);
            var currency = Currency.Create("CZK", "Kč", "Czech koruna");
            Order = Order.Create(
                customerName: "Conservation",
                customerEmail: "conservation@example.test",
                customerPhone: "+420111222333",
                customerAddress: Address.Create("Main 1", "Prague", "11000", "cz"),
                rooms: 1,
                bathrooms: 1,
                cleaningDateTime: DateTime.UtcNow.AddDays(10),
                paymentType: PaymentType.Card,
                totalPrice: total,
                currencyId: currency.Id,
                paymentStatus: PaymentStatus.Paid,
                userId: UserId,
                cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
            Order.Id = orderId;
            Order.SetCurrency(currency);
            Order.ApplyCredit(credit, UserId);
            Order.AssignStripePaymentIntentId($"pi_{orderId}");
            Order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, Order));
            _steps.Add($"{orderId} total {total} credit {credit}");

            _stripe.Setup(s => s.RefundPaymentIntentAsync(
                    It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string _, decimal amount, string key, CancellationToken _) => Stripe(amount, key));
            _creditRepository
                .Setup(c => c.TryReturnAsync(UserId, It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>(), Order.Id, It.IsAny<string?>()))
                .ReturnsAsync((string _, string _, decimal amount, string key, string _, CancellationToken _, string? _, string? _) =>
                {
                    if (_credit.Any(c => c.Key == key))
                    {
                        return false;
                    }

                    _credit.Add((key, CreditTransactionReason.OrderPaymentReturned, amount));
                    return true;
                });
            _creditRepository
                .Setup(c => c.GetReturnedTotalForOrderAsync(Order.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Sum(CreditTransactionReason.OrderPaymentReturned));
            _creditRepository
                .Setup(c => c.GetDisputeSettledTotalForOrderAsync(Order.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Sum(CreditTransactionReason.DisputeSettlement));
            _creditRepository
                .Setup(c => c.GetReturnedAmountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string key, CancellationToken _) => _credit
                    .Where(c => c.Key == key && c.Reason == CreditTransactionReason.OrderPaymentReturned)
                    .Sum(c => c.Amount));
        }

        public Order Order { get; }

        public decimal Paid => Order.TotalPrice;

        public decimal Received => _stripeRefunded.Values.Sum()
            + Sum(CreditTransactionReason.OrderPaymentReturned)
            + Sum(CreditTransactionReason.DisputeSettlement);

        public bool MustGiveBackAll { get; private set; }

        public bool LeftAStripeRefundUnrecorded =>
            _rows.Any(r => r.Status != RefundStatus.Succeeded && _stripeRefunded.ContainsKey(r.Key));

        public string Describe() =>
            $"paid {Paid} received {Received}: {string.Join("; ", _steps)}";

        public async Task RunAsync()
        {
            var ending = (Ending)_random.Next(4);
            var answer = ending == Ending.PlatformCancellation
                ? (_random.Next(2) == 0 ? StripeAnswer.Pays : StripeAnswer.Refuses)
                : RandomAnswer(mayTakeTheMoney: true);
            var loseTheRecord = ending == Ending.AdminFullRefund && answer == StripeAnswer.Pays && _random.Next(2) == 0;

            // A refund Stripe paid without it being recorded is counted by no other refund's ceiling until its
            // own retry records it, so here it is only ever the last money in flight on the order.
            var earlierAnswers = answer == StripeAnswer.PaysThenTimesOut || loseTheRecord
                ? () => StripeAnswer.Pays
                : (Func<StripeAnswer>)(() => RandomAnswer(mayTakeTheMoney: false));
            for (var n = _random.Next(4); n > 0; n--)
            {
                switch (_random.Next(3))
                {
                    case 0:
                        Settle();
                        break;
                    case 1:
                        await RefundAsync(
                            new RefundRequest(Order.Id, Whole(1, Paid), RefundReason.AdminDiscretion, "admin",
                                RefundRequestId: $"partial-{++_actions}"),
                            earlierAnswers());
                        break;
                    default:
                        await RefundAsync(
                            new RefundRequest(Order.Id, Whole(1, Paid), RefundReason.DisputeResolution, "admin",
                                DisputeId: $"dispute-{++_actions}"),
                            earlierAnswers());
                        break;
                }
            }

            MustGiveBackAll = Order.PaymentStatus == PaymentStatus.Paid;
            _steps.Add($"{ending} with Stripe {answer}{(MustGiveBackAll ? " while paid" : "")}");
            await EndAsync(ending, answer, loseTheRecord);

            if (answer is StripeAnswer.Unreachable or StripeAnswer.Refuses && _random.Next(3) == 0)
            {
                Settle();
            }

            // The hourly re-drive finishes a cancellation's own refund; any other is retried by the action that
            // asked for it.
            for (var round = 0; round < 3; round++)
            {
                foreach (var row in _rows.Where(r => r.Status == RefundStatus.Pending).ToList())
                {
                    if (row.Key != RefundService.BuildRefundKey(new RefundRequest(Order.Id, 0m, row.Reason, "system")))
                    {
                        await RefundAsync(_asked[row.Key], StripeAnswer.Pays);
                        continue;
                    }

                    var uow = new UnitOfWork(this);
                    var result = await uow.RefundService().RedriveAsync(row.Id, "system", CancellationToken.None);
                    await uow.CommitAsync();
                    _steps.Add($"redrive {row.Key} -> {(result.IsSuccess ? result.Value!.Amount.ToString() : result.Error!.Message)}");
                }
            }
        }

        private async Task EndAsync(Ending ending, StripeAnswer answer, bool loseTheRecord)
        {
            switch (ending)
            {
                case Ending.MemberCancellation:
                {
                    _nextAnswer = answer;
                    var uow = new UnitOfWork(this);
                    var policy = new Mock<ICancellationPolicyResolver>();
                    policy.Setup(p => p.ResolveForOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new CancellationPolicy(48, 24, 0.25m, 0.5m,
                            BookingPolicy.OopsWindowMinutesStandard, OopsWindowRule.Standard));
                    await new CustomerOrderCancellation(
                            Mock.Of<ITenantProvider>(), uow.RefundService(), uow.Refunds.Object,
                            Mock.Of<IReceivableRepository>(), _creditRepository.Object, Mock.Of<ILoyaltyService>(),
                            policy.Object, Mock.Of<INotificationProducer>(), Mock.Of<ILiveActivityProducer>(),
                            Mock.Of<IExpressWaiverConsumer>(), Mock.Of<IPendingDispatch>(), new AuditContext(),
                            TimeProvider.System, NullLogger<CustomerOrderCancellation>.Instance)
                        .ExecuteAsync(Order, reason: null, UserId, CancellationToken.None);
                    await uow.CommitAsync();
                    break;
                }

                case Ending.AdminFullRefund:
                {
                    var full = new RefundRequest(Order.Id, Paid, RefundReason.AdminDiscretion, "admin", RefundRequestId: "full");
                    _loseNextSucceededRecord = loseTheRecord;
                    if (loseTheRecord)
                    {
                        _steps.Add("record lost");
                    }

                    await RefundAsync(full, answer);
                    if (_random.Next(2) == 0)
                    {
                        await RefundAsync(full, StripeAnswer.Pays);
                    }

                    break;
                }

                case Ending.NoShow:
                {
                    _nextAnswer = answer;
                    var uow = new UnitOfWork(this);
                    await new CleanerNoShowCancellation(
                            _creditRepository.Object, uow.RefundService(), uow.Refunds.Object,
                            Mock.Of<INotificationProducer>(),
                            new GuestOrderAccessTokenIssuer(Mock.Of<IGuestOrderAccessTokenRepository>()),
                            Mock.Of<IPendingDispatch>(), NullLogger<CleanerNoShowCancellation>.Instance)
                        .ExecuteAsync(Order, CancelledBy.System, "system", DateTime.UtcNow, CancellationToken.None);
                    await uow.CommitAsync();
                    break;
                }

                default:
                {
                    _nextAnswer = answer;
                    var uow = new UnitOfWork(this);
                    await new PlatformOrderCancellation(
                            uow.RefundService(), uow.Refunds.Object, _creditRepository.Object, Mock.Of<ILoyaltyService>(),
                            Mock.Of<INotificationProducer>(), Mock.Of<ILiveActivityProducer>(),
                            Mock.Of<IExpressWaiverConsumer>(),
                            new GuestOrderAccessTokenIssuer(Mock.Of<IGuestOrderAccessTokenRepository>()),
                            Mock.Of<IPendingDispatch>())
                        .CancelAsync(Order, "admin", CancelledBy.Admin, null, RefundReason.CustomerCancellation,
                            CancellationToken.None);
                    await uow.CommitAsync();
                    break;
                }
            }
        }

        private async Task RefundAsync(RefundRequest request, StripeAnswer answer)
        {
            _nextAnswer = answer;
            _asked[RefundService.BuildRefundKey(request)] = request;
            var uow = new UnitOfWork(this);
            try
            {
                var result = await uow.RefundService().IssueRefundAsync(request, CancellationToken.None);
                if (result.IsSuccess)
                {
                    await uow.CommitAsync();
                }

                _steps.Add($"refund {request.Amount} on {RefundService.BuildRefundKey(request)} with Stripe {answer} -> "
                    + (result.IsSuccess ? result.Value!.Amount.ToString() : result.Error!.Message));
            }
            catch (Exception ex) when (ex is HttpRequestException or DbUpdateException)
            {
                _steps.Add($"refund {request.Amount} on {RefundService.BuildRefundKey(request)} with Stripe {answer} -> {ex.GetType().Name}");
            }
        }

        private void Settle()
        {
            var outstanding = Paid
                - _rows.Where(r => r.Status == RefundStatus.Succeeded).Sum(r => r.Amount)
                - Sum(CreditTransactionReason.OrderPaymentReturned)
                - Sum(CreditTransactionReason.DisputeSettlement);
            if (outstanding < 1m)
            {
                return;
            }

            var amount = Whole(1, outstanding);
            _credit.Add(($"dispute-settlement:dispute-{++_actions}", CreditTransactionReason.DisputeSettlement, amount));
            _steps.Add($"settled {amount} in credit");
        }

        private Task Stripe(decimal amount, string key)
        {
            var answer = _nextAnswer;
            _nextAnswer = StripeAnswer.Pays;
            if (_stripeRefunded.TryGetValue(key, out var took))
            {
                return took == amount
                    ? Task.CompletedTask
                    : Task.FromException(new StripeException($"Key {key} was first used for {took}, not {amount}."));
            }

            switch (answer)
            {
                case StripeAnswer.Unreachable:
                    return Task.FromException(new HttpRequestException("connection reset"));
                case StripeAnswer.Refuses:
                    return Task.FromException(new StripeException("card network unavailable"));
                case StripeAnswer.PaysThenTimesOut:
                    _stripeRefunded[key] = amount;
                    return Task.FromException(new HttpRequestException("timed out"));
                default:
                    _stripeRefunded[key] = amount;
                    return Task.CompletedTask;
            }
        }

        private StripeAnswer RandomAnswer(bool mayTakeTheMoney) => _random.Next(mayTakeTheMoney ? 10 : 8) switch
        {
            < 6 => StripeAnswer.Pays,
            6 => StripeAnswer.Unreachable,
            7 => StripeAnswer.Refuses,
            _ => StripeAnswer.PaysThenTimesOut,
        };

        private decimal Whole(decimal min, decimal max) => _random.Next((int)min, (int)Math.Floor(max) + 1);

        private decimal Sum(CreditTransactionReason reason) =>
            _credit.Where(c => c.Reason == reason).Sum(c => c.Amount);

        /// <summary>
        /// One request's view of the refunds table: rows read are materialized afresh, changes are kept until
        /// the commit, and a commit that fails leaves the table as it was.
        /// </summary>
        private sealed class UnitOfWork
        {
            private readonly World _world;
            private readonly Dictionary<string, Refund> _tracked = [];
            private readonly List<Refund> _added = [];

            public UnitOfWork(World world)
            {
                _world = world;
                Refunds.Setup(r => r.GetByRefundKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((string key, CancellationToken _) =>
                        _tracked.Values.SingleOrDefault(r => r.RefundKey == key)
                        ?? Track(_world._rows.SingleOrDefault(r => r.Key == key)));
                Refunds.Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((string id, CancellationToken _) =>
                        _tracked.GetValueOrDefault(id) ?? Track(_world._rows.SingleOrDefault(r => r.Id == id)));
                Refunds.Setup(r => r.GetSucceededRefundTotalForOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => _world._rows.Where(r => r.Status == RefundStatus.Succeeded).Sum(r => r.Amount));
                Refunds.Setup(r => r.Add(It.IsAny<Refund>())).Callback<Refund>(_added.Add);
                Refunds.Setup(r => r.Rollback()).Callback(_added.Clear);
                Refunds.Setup(r => r.CommitAsync(It.IsAny<CancellationToken>())).Returns(CommitAsync);
            }

            public Mock<IRefundRepository> Refunds { get; } = new();

            public RefundService RefundService()
            {
                var factory = new Mock<IStripeClientFactory>();
                factory.Setup(f => f.CreateClient()).Returns(_world._stripe.Object);
                return new RefundService(
                    Refunds.Object, Mock.Of<IOrderRepository>(o => o.GetByIdAsync(_world.Order.Id, It.IsAny<CancellationToken>())
                        == Task.FromResult<Order?>(_world.Order)),
                    _world._creditRepository.Object, factory.Object, NullLogger<RefundService>.Instance);
            }

            public Task CommitAsync()
            {
                if (_world._loseNextSucceededRecord
                    && _tracked.Values.Any(r => r.Status == RefundStatus.Succeeded
                        && _world._rows.Single(row => row.Id == r.Id).Status != RefundStatus.Succeeded))
                {
                    _world._loseNextSucceededRecord = false;
                    return Task.FromException(new DbUpdateException("The connection was lost."));
                }

                foreach (var refund in _added)
                {
                    _world._rows.Add(new RefundRow
                    {
                        Id = refund.Id,
                        Key = refund.RefundKey,
                        Reason = refund.Reason,
                        DisputeId = refund.DisputeId,
                        Amount = refund.Amount,
                        Status = refund.Status,
                    });
                    _tracked[refund.Id] = refund;
                }

                _added.Clear();
                foreach (var refund in _tracked.Values)
                {
                    var row = _world._rows.Single(r => r.Id == refund.Id);
                    row.Amount = refund.Amount;
                    row.Status = refund.Status;
                }

                return Task.CompletedTask;
            }

            private Refund? Track(RefundRow? row)
            {
                if (row is null)
                {
                    return null;
                }

                var refund = Refund.Create(
                    _world.Order.Id, row.Key, row.Amount, "CZK", row.Reason, RefundSource.AppRefund, disputeId: row.DisputeId);
                refund.Id = row.Id;
                if (row.Status == RefundStatus.Succeeded)
                {
                    refund.MarkSucceeded(stripeRefundId: null, confirmedOnUtc: DateTimeOffset.UtcNow);
                }
                else if (row.Status == RefundStatus.Failed)
                {
                    refund.MarkFailed();
                }

                _tracked[refund.Id] = refund;
                return refund;
            }
        }
    }
}
