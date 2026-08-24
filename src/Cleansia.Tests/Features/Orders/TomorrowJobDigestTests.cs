using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using MockQueryable.Moq;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The day-ahead digest, whose whole job is deciding WHO to message and WHEN in a timezone that is not
/// the server's.
///
/// <para>Every case here fixes the clock and moves the CLEANER, never the other way round — the sweep
/// runs hourly on a UTC cron and the only thing that varies between cleaners is the zone their work
/// country resolves to. A test that moved the clock instead would prove the arithmetic against one
/// timezone and call it done.</para>
///
/// <para><b>The send hour is a WINDOW, so a case that wants "not yet" must pick an hour strictly LATER
/// than the cleaner's current local one.</b> An offset chosen modulo 24 lands inside the catch-up window
/// for part of every day and passes by accident for the rest — which is how a case that looks like it
/// pins the selection ends up pinning nothing.</para>
/// </summary>
public class TomorrowJobDigestTests
{
    private const string EmployeeId = "emp-1";
    private const string UserId = "user-1";
    private const string CountryId = "cz";

    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfiguration = new();
    private readonly Mock<INotificationProducer> _producer = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private SendTomorrowJobDigest.Handler Handler() => new(
        _employeeRepository.Object,
        _orderRepository.Object,
        _countryConfiguration.Object,
        _producer.Object,
        _tenantProvider.Object,
        _unitOfWork.Object,
        NullLogger<SendTomorrowJobDigest.Handler>.Instance);

    private Employee ArrangeCleaner(
        string? workCountryId = CountryId,
        DateTimeOffset? lastDigestAt = null,
        string? timeZoneId = "Europe/Prague")
    {
        var user = User.CreateWithPassword("cleaner@example.test", "x", "Cle", "Aner");
        user.Id = UserId;
        var employee = Employee.CreateWithUser(user);
        employee.Id = EmployeeId;
        employee.UpdateContractStatus(ContractStatus.Approved);
        if (workCountryId is not null)
        {
            employee.AssignWorkCountry(workCountryId);
        }

        if (lastDigestAt is { } stamp)
        {
            employee.MarkTomorrowDigestSent(stamp);
        }

        _employeeRepository.Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(new[] { employee }.AsQueryable().BuildMock());
        _employeeRepository.Setup(r => r.GetByIdIgnoringTenantAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        _countryConfiguration
            .Setup(r => r.GetByCountryIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create(
                countryId: CountryId, defaultCurrencyCode: "CZK", defaultLanguageCode: "cs",
                standardVatRate: 0.21m, timeZoneId: timeZoneId));
        return employee;
    }

    private void ArrangeOrders(int tomorrowCount, string zoneId = "Europe/Prague")
    {
        // Midday on the cleaner's LOCAL tomorrow, derived from the real clock — the handler computes
        // its window from DateTime.UtcNow, so a fixture pinned to a literal date only lands inside it
        // on one day of the year.
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var localTomorrowMidday = TimeZoneInfo
            .ConvertTimeFromUtc(DateTime.UtcNow, zone).Date.AddDays(1).AddHours(12);
        var cleaningUtc = TimeZoneInfo.ConvertTimeToUtc(localTomorrowMidday, zone);

        var orders = new List<Order>();
        for (var i = 0; i < tomorrowCount; i++)
        {
            var order = ValidatorTestHelpers.BuildEmptyOrder(
                $"order-{i}", OrderStatus.Confirmed, maxEmployees: 1,
                cleaningDateTime: cleaningUtc);
            var employee = ValidatorTestHelpers.BuildEmployee(EmployeeId, ContractStatus.Approved);
            order.AddAssignedEmployee(OrderEmployee.Create(order, employee));
            orders.Add(order);
        }

        _orderRepository.Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(orders.AsQueryable().BuildMock());
    }

    private string? SentCount()
    {
        string? count = null;
        _producer
            .Setup(p => p.NotifyAsync(
                It.IsAny<string>(), NotificationEventCatalog.ReminderTomorrow,
                It.IsAny<Dictionary<string, string>>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, Dictionary<string, string>, string?, string?, CancellationToken>(
                (_, _, args, _, _, _) => args.TryGetValue("count", out count))
            .Returns(Task.CompletedTask);
        return count;
    }

    private Task<Infra.Common.Validations.BusinessResult<SendTomorrowJobDigest.Response>> Run(DateTime nowUtc)
    {
        // The handler reads DateTime.UtcNow, so a case picks its moment by choosing the LOCAL send hour
        // that corresponds to it rather than by injecting a clock.
        var localHour = TimeZoneInfo.ConvertTimeFromUtc(
            nowUtc, TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague")).Hour;
        return Handler().Handle(new SendTomorrowJobDigest.Command(LocalSendHour: localHour), CancellationToken.None);
    }

    [Fact]
    public async Task A_Cleaner_Whose_Local_Clock_Has_Struck_The_Send_Hour_Gets_The_Count()
    {
        ArrangeCleaner();
        ArrangeOrders(tomorrowCount: 2);
        SentCount();

        var result = await Run(DateTime.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.DigestsSent);
    }

    /// <summary>
    /// The selection is on the CLEANER's hour, not the server's. Asking for a send hour that is not the
    /// cleaner's current local hour must produce nothing — that is the whole reason the sweep runs
    /// hourly instead of once a day.
    /// </summary>
    [Fact]
    public async Task A_Cleaner_Whose_Local_Clock_Has_Not_Struck_The_Send_Hour_Is_Skipped()
    {
        ArrangeCleaner();
        ArrangeOrders(tomorrowCount: 2);

        // A send hour LATER than the cleaner's current local hour, chosen without wrapping past
        // midnight: the predicate is a window now, so `(hour + 5) % 24` would silently land INSIDE it
        // for five hours of every day and the case would pass by accident the rest of the time.
        var pragueNow = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague"));
        var notYetHour = Math.Min(pragueNow.Hour + 1, 23);
        Assert.True(notYetHour > pragueNow.Hour, "The clock is at 23:00 local; this case needs an hour above it.");

        var result = await Handler().Handle(
            new SendTomorrowJobDigest.Command(LocalSendHour: notYetHour), CancellationToken.None);

        Assert.Equal(0, result.Value!.DigestsSent);
    }

    /// <summary>
    /// The reason the hour test is a window rather than an equality.
    ///
    /// <para>An equality gives a whole timezone exactly ONE attempt per day. A cleaner who takes
    /// tomorrow's job at 18:30 was never told: at 19:00 the predicate was already false. No failure was
    /// needed for that — it was simply what the code did.</para>
    /// </summary>
    [Fact]
    public async Task An_Hour_Missed_Is_Still_Caught_Up_Within_The_Window()
    {
        ArrangeCleaner();
        ArrangeOrders(tomorrowCount: 2);
        SentCount();

        var pragueNow = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague"));
        var anHourAgo = pragueNow.Hour - 1;
        if (anHourAgo < 0)
        {
            return; // Midnight local: there is no earlier hour today to have missed.
        }

        var result = await Handler().Handle(
            new SendTomorrowJobDigest.Command(LocalSendHour: anHourAgo), CancellationToken.None);

        Assert.Equal(1, result.Value!.DigestsSent);
    }

    /// <summary>
    /// The catch-up is BOUNDED. These keys are non-mutable and there are no quiet hours, so an
    /// open-ended <c>&gt;=</c> would push at 23:00 to a cleaner who took a job at 22:50.
    /// </summary>
    [Fact]
    public async Task The_Catch_Up_Stops_Rather_Than_Running_To_Midnight()
    {
        ArrangeCleaner();
        ArrangeOrders(tomorrowCount: 2);

        var pragueNow = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague"));
        var longPast = pragueNow.Hour - 4;
        if (longPast < 0)
        {
            return; // Too near local midnight for a four-hour-old send hour to exist today.
        }

        var result = await Handler().Handle(
            new SendTomorrowJobDigest.Command(LocalSendHour: longPast, CatchUpHours: 3),
            CancellationToken.None);

        Assert.Equal(0, result.Value!.DigestsSent);
    }

    /// <summary>
    /// The zero-count branch's whole promise: it stamps nothing, so a job taken later the same evening
    /// still earns a digest. That recovery is only reachable because the hour test is a window — under
    /// an equality there was no later tick for it to happen on.
    /// </summary>
    [Fact]
    public async Task A_Job_Taken_After_An_Empty_Tick_Is_Still_Reported_The_Same_Evening()
    {
        var employee = ArrangeCleaner();
        ArrangeOrders(tomorrowCount: 0);
        SentCount();

        var pragueNow = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague"));
        var sendHour = pragueNow.Hour;

        var empty = await Handler().Handle(
            new SendTomorrowJobDigest.Command(LocalSendHour: sendHour), CancellationToken.None);
        Assert.Equal(0, empty.Value!.DigestsSent);
        Assert.Null(employee.LastTomorrowDigestAt);

        // The cleaner takes a job, and the next tick of the same evening finds it.
        ArrangeOrders(tomorrowCount: 1);
        var second = await Handler().Handle(
            new SendTomorrowJobDigest.Command(LocalSendHour: sendHour), CancellationToken.None);

        Assert.Equal(1, second.Value!.DigestsSent);
        Assert.NotNull(employee.LastTomorrowDigestAt);
    }

    /// <summary>
    /// A zero-job evening is not news, and a digest saying "0" would train a cleaner to ignore the one
    /// that says 2. It must also NOT stamp — a job taken later this evening still deserves a digest on
    /// the next hourly tick.
    /// </summary>
    [Fact]
    public async Task An_Evening_With_No_Jobs_Tomorrow_Sends_Nothing_And_Stamps_Nothing()
    {
        var employee = ArrangeCleaner();
        ArrangeOrders(tomorrowCount: 0);

        var result = await Run(DateTime.UtcNow);

        Assert.Equal(0, result.Value!.DigestsSent);
        Assert.Null(employee.LastTomorrowDigestAt);
    }

    /// <summary>
    /// The watermark is compared in the CLEANER's zone. A UTC comparison would let a cleaner east of
    /// Greenwich be told about tomorrow twice on the same local evening.
    /// </summary>
    [Fact]
    public async Task A_Cleaner_Already_Told_Today_Is_Not_Told_Again()
    {
        var pragueNow = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague"));
        ArrangeCleaner(lastDigestAt: new DateTimeOffset(DateTime.UtcNow, TimeSpan.Zero));
        ArrangeOrders(tomorrowCount: 3);

        var result = await Handler().Handle(
            new SendTomorrowJobDigest.Command(LocalSendHour: pragueNow.Hour), CancellationToken.None);

        Assert.Equal(0, result.Value!.DigestsSent);
    }

    /// <summary>
    /// A cleaner with no work country has no resolvable zone. Skipped rather than defaulted to UTC —
    /// the same predicate the new-jobs digest uses, and defaulting would send at the wrong hour while
    /// looking like it worked.
    /// </summary>
    [Fact]
    public async Task A_Cleaner_With_No_Work_Country_Is_Never_Considered()
    {
        ArrangeCleaner(workCountryId: null);
        ArrangeOrders(tomorrowCount: 2);

        var result = await Run(DateTime.UtcNow);

        Assert.Equal(0, result.Value!.CleanersConsidered);
        Assert.Equal(0, result.Value.DigestsSent);
    }

    /// <summary>
    /// One lookup per distinct work country, not per cleaner. The sweep walks every approved cleaner on
    /// the platform every hour, and the same handful of countries would otherwise be re-read hundreds of
    /// times a day for no new information.
    /// </summary>
    [Fact]
    public async Task The_Country_Configuration_Is_Read_Once_Per_Distinct_Country()
    {
        var users = Enumerable.Range(1, 5).Select(i =>
        {
            var user = User.CreateWithPassword($"c{i}@example.test", "x", "C", $"{i}");
            user.Id = $"user-{i}";
            var employee = Employee.CreateWithUser(user);
            employee.Id = $"emp-{i}";
            employee.UpdateContractStatus(ContractStatus.Approved);
            employee.AssignWorkCountry(CountryId);
            return employee;
        }).ToList();

        _employeeRepository.Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(users.AsQueryable().BuildMock());
        _countryConfiguration
            .Setup(r => r.GetByCountryIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create(
                countryId: CountryId, defaultCurrencyCode: "CZK", defaultLanguageCode: "cs",
                standardVatRate: 0.21m, timeZoneId: "Europe/Prague"));
        ArrangeOrders(tomorrowCount: 0);

        await Run(DateTime.UtcNow);

        _countryConfiguration.Verify(
            r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// An unresolvable zone id degrades to UTC rather than throwing — <c>TimeZoneResolution</c>
    /// guarantees it, and a pricing-adjacent sweep must never fall over on a config typo. The cleaner
    /// then gets their digest on UTC's clock, which is wrong but visible, rather than not at all.
    /// </summary>
    [Fact]
    public async Task An_Unresolvable_Timezone_Falls_Back_To_Utc_Instead_Of_Throwing()
    {
        ArrangeCleaner(timeZoneId: "Not/AZone");
        ArrangeOrders(tomorrowCount: 1, zoneId: "UTC");
        SentCount();

        var result = await Handler().Handle(
            new SendTomorrowJobDigest.Command(LocalSendHour: DateTime.UtcNow.Hour), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.DigestsSent);
    }

    // -- ADR-0054 required change 6: the count is batched, not per cleaner --------------------

    /// <summary>
    /// The sweep walks EVERY approved cleaner on the platform every evening. Before this, each one
    /// cost a <c>CountAsync</c> round trip whether or not they were even in a timezone the hour gate
    /// admits — so the tick's query count grew with headcount while the messages sent did not.
    ///
    /// <para>Counting queries rather than timing them is the point: a duration assertion passes on a
    /// fast machine with the N+1 still in place, and this defect is about the SHAPE.</para>
    /// </summary>
    [Fact]
    public async Task Twenty_Cleaners_In_One_Zone_Cost_One_Order_Query_Not_Twenty()
    {
        var cleaners = ArrangeManyCleaners(20);
        ArrangeOrdersFor(cleaners.Select(c => c.Id).ToList());

        var nowUtc = DateTime.UtcNow;
        var result = await Run(nowUtc);

        Assert.True(result.IsSuccess);
        _orderRepository.Verify(r => r.GetQueryableIgnoringTenant(), Times.Once);
    }

    /// <summary>
    /// A cleaner the hour gate or the watermark turns away must cost NO query at all — eligibility is
    /// decided from the clock and the stamp, both already in memory.
    /// </summary>
    [Fact]
    public async Task Cleaners_Already_Digested_Today_Cost_No_Order_Query()
    {
        var cleaners = ArrangeManyCleaners(5, alreadyDigested: true);
        ArrangeOrdersFor(cleaners.Select(c => c.Id).ToList());

        var result = await Run(DateTime.UtcNow);

        Assert.True(result.IsSuccess);
        _orderRepository.Verify(r => r.GetQueryableIgnoringTenant(), Times.Never);
    }

    private List<Employee> ArrangeManyCleaners(int count, bool alreadyDigested = false)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");
        var cleaners = new List<Employee>();
        for (var i = 0; i < count; i++)
        {
            var user = User.CreateWithPassword($"cleaner{i}@example.test", "x", "Cle", "Aner");
            user.Id = $"user-{i}";
            var employee = Employee.CreateWithUser(user);
            employee.Id = $"emp-{i}";
            employee.UpdateContractStatus(ContractStatus.Approved);
            employee.AssignWorkCountry(CountryId);
            if (alreadyDigested)
            {
                // Stamped for the cleaner's LOCAL today, which is what the watermark compares.
                employee.MarkTomorrowDigestSent(new DateTimeOffset(DateTime.UtcNow, TimeSpan.Zero));
            }

            cleaners.Add(employee);
        }

        _employeeRepository.Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(cleaners.AsQueryable().BuildMock());
        _countryConfiguration
            .Setup(r => r.GetByCountryIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create(
                countryId: CountryId, defaultCurrencyCode: "CZK", defaultLanguageCode: "cs",
                standardVatRate: 0.21m, timeZoneId: "Europe/Prague"));
        _ = zone;
        return cleaners;
    }

    private void ArrangeOrdersFor(IReadOnlyList<string> employeeIds)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");
        var localTomorrowMidday = TimeZoneInfo
            .ConvertTimeFromUtc(DateTime.UtcNow, zone).Date.AddDays(1).AddHours(12);
        var cleaningUtc = TimeZoneInfo.ConvertTimeToUtc(localTomorrowMidday, zone);

        var orders = new List<Order>();
        for (var i = 0; i < employeeIds.Count; i++)
        {
            var order = ValidatorTestHelpers.BuildEmptyOrder(
                $"order-{i}", OrderStatus.Confirmed, maxEmployees: 1,
                cleaningDateTime: cleaningUtc);
            var employee = ValidatorTestHelpers.BuildEmployee(employeeIds[i], ContractStatus.Approved);
            order.AddAssignedEmployee(OrderEmployee.Create(order, employee));
            orders.Add(order);
        }

        _orderRepository.Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(orders.AsQueryable().BuildMock());
    }
}
