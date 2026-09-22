using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0068 D3 — the one writer of the acceptance row: it names the seat and the exact text, freezes the
/// builder's facts, takes the IP and device label from the request, the device id from the session's
/// signed claim (never the header), the client from the host, stamps both rows with the ORDER's company,
/// stages the audit index row beside it, commits nothing, and refuses to stage a text that is not of the
/// order's document.
/// </summary>
public sealed class WorkContractAcceptorTests
{
    private const string OrderId = "order-acceptor-1";
    private const string EmployeeId = "emp-acceptor-1";
    private const string OrderTenant = "cleansia-sk";

    private static readonly WorkContractFacts Facts = new(
        "ORD-ACC", new DateTime(2026, 9, 22, 9, 0, 0, DateTimeKind.Utc), 120, 1000m, "CZK", "Prague · 110 xx", "cz", 1, 1, [], [], []);

    private readonly Mock<IWorkContractFactsBuilder> _factsBuilder = new();
    private readonly Mock<IWorkContractAcceptanceRepository> _acceptanceRepository = new();
    private readonly Mock<IEmployeeActionAuditRepository> _auditRepository = new();

    private WorkContractAcceptor CreateAcceptor(IUserSessionProvider session, IRequestMetadataProvider metadata, string audience = "cleansia.mobile") =>
        new(
            WorkContractTestData.LegalDocumentRepository().Object,
            _factsBuilder.Object,
            _acceptanceRepository.Object,
            _auditRepository.Object,
            metadata,
            session,
            new HostAudienceProvider(audience));

    private static (Order Order, OrderEmployee Seat) ArrangeSeat()
    {
        var order = ValidatorTestHelpers.BuildOrder(OrderId, OrderStatus.Confirmed, EmployeeId);
        order.TenantId = OrderTenant;
        return (order, order.AssignedEmployees.Single());
    }

    private static TestUserSessionProvider SessionWithDeviceClaim(string? deviceId) =>
        new($"{EmployeeId}-user", "cleaner@example.com",
            deviceId is null ? [] : [new Claim(AuthExtensions.DeviceIdClaimType, deviceId)]);

    [Fact]
    public async Task Stage_Builds_The_Row_From_The_Seat_The_Text_The_Facts_And_The_Request()
    {
        var (order, seat) = ArrangeSeat();
        _factsBuilder.Setup(b => b.BuildAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(Facts);
        WorkContractAcceptance? added = null;
        _acceptanceRepository.Setup(r => r.Add(It.IsAny<WorkContractAcceptance>())).Callback<WorkContractAcceptance>(a => added = a);

        var acceptance = await CreateAcceptor(
                SessionWithDeviceClaim("claim-device-9"),
                new TestRequestMetadataProvider("203.0.113.9", "Pixel 8", deviceId: "header-device-1"))
            .StageAsync(order, seat, WorkContractTestData.TextIdCs, CancellationToken.None);

        Assert.Same(acceptance, added);
        Assert.Equal(OrderId, acceptance.OrderId);
        Assert.Equal(seat.Id, acceptance.OrderEmployeeId);
        Assert.Equal(EmployeeId, acceptance.EmployeeId);
        Assert.Equal(WorkContractTestData.TextIdCs, acceptance.LegalDocumentTextId);
        Assert.Equal(WorkContractTestData.Version, acceptance.DocumentVersion);
        Assert.Equal("cleansia.mobile", acceptance.ClientAudience);
        Assert.Equal("203.0.113.9", acceptance.IpAddress);
        Assert.Equal("Pixel 8", acceptance.DeviceLabel);
        Assert.Equal("claim-device-9", acceptance.DeviceId);
        Assert.Equal(Facts.ToJson(), acceptance.FactsJson);
        Assert.Equal(OrderTenant, acceptance.TenantId);
    }

    [Fact]
    public async Task The_Device_Is_The_Sessions_Claim_Or_Nothing_Never_The_Header()
    {
        var (order, seat) = ArrangeSeat();
        _factsBuilder.Setup(b => b.BuildAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(Facts);

        var acceptance = await CreateAcceptor(
                SessionWithDeviceClaim(null),
                new TestRequestMetadataProvider("203.0.113.9", "Firefox", deviceId: "header-device-1"))
            .StageAsync(order, seat, WorkContractTestData.TextIdEn, CancellationToken.None);

        Assert.Null(acceptance.DeviceId);
        Assert.Equal("Firefox", acceptance.DeviceLabel);
    }

    [Fact]
    public async Task Stage_Adds_The_Audit_Index_Row_For_The_Cleaner_On_The_Orders_Company_And_Commits_Nothing()
    {
        var (order, seat) = ArrangeSeat();
        _factsBuilder.Setup(b => b.BuildAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(Facts);
        EmployeeActionAudit? audit = null;
        _auditRepository.Setup(r => r.Add(It.IsAny<EmployeeActionAudit>())).Callback<EmployeeActionAudit>(a => audit = a);

        await CreateAcceptor(SessionWithDeviceClaim(null), new TestRequestMetadataProvider())
            .StageAsync(order, seat, WorkContractTestData.TextIdEn, CancellationToken.None);

        Assert.NotNull(audit);
        Assert.Equal(EmployeeId, audit!.EmployeeId);
        Assert.Equal(OrderId, audit.OrderId);
        Assert.Equal(EmployeeAuditAction.ContractAccepted, audit.Action);
        Assert.Equal(OrderTenant, audit.TenantId);
        _acceptanceRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _auditRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Text_Of_Another_Document_Is_A_Programming_Error_Not_A_Row()
    {
        var (order, seat) = ArrangeSeat();
        _factsBuilder.Setup(b => b.BuildAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(Facts);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateAcceptor(SessionWithDeviceClaim(null), new TestRequestMetadataProvider())
                .StageAsync(order, seat, WorkContractTestData.OtherTextId, CancellationToken.None));

        _acceptanceRepository.Verify(r => r.Add(It.IsAny<WorkContractAcceptance>()), Times.Never);
        _auditRepository.Verify(r => r.Add(It.IsAny<EmployeeActionAudit>()), Times.Never);
    }

    [Fact]
    public async Task An_Order_With_No_Document_Is_A_Programming_Error_Not_A_Row()
    {
        var order = Order.Create(
            "Test Customer", "test@example.com", "+420000000000",
            Core.Domain.Users.Address.Create("123 Main St", "Prague", "11000", "cz"),
            1, 1, ValidatorTestHelpers.DefaultCleaningTime, PaymentType.Cash, 1000m, ValidatorTestHelpers.CurrencyId, PaymentStatus.Pending);
        order.Id = OrderId;
        var seat = OrderEmployee.Create(order, ValidatorTestHelpers.BuildEmployee(EmployeeId, ContractStatus.Approved));
        order.AddAssignedEmployee(seat);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateAcceptor(SessionWithDeviceClaim(null), new TestRequestMetadataProvider())
                .StageAsync(order, seat, WorkContractTestData.TextIdEn, CancellationToken.None));

        _acceptanceRepository.Verify(r => r.Add(It.IsAny<WorkContractAcceptance>()), Times.Never);
    }
}
