using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities;
using Moq;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// ADR-0034 D8 — three routes, three DTOs. The admin default is masked and the DTO has no unmasked field
/// at all, so a client cannot render what it was never sent; the unmasked read is a Command, because the
/// audit gate records only commands and the audit trail is the compensating control for holding these
/// values in plaintext.
/// </summary>
public class PayoutReadContractTests
{
    private const string EmployeeId = "emp-1";
    private const string UserId = "user-1";
    private const string AccountNumber = "5885638003";
    private const string Iban = "CZ3155000000005885638003";
    private const string Swift = "RZBCCZPP";
    private const string HolderName = "Milada Novotna";

    private readonly Mock<IEmployeePayoutDetailsRepository> _payoutDetails = new();
    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<IOrderAccessService> _orderAccess = new();
    private readonly EmployeePayoutDetails _details = EmployeePayoutDetails.Create(
        EmployeeId, PayoutScheme.CzskDomesticWithIban, "country-cz", PayoutDetailsStatus.Provided,
        accountPrefix: null, accountNumber: AccountNumber, bankCode: "5500", iban: Iban,
        swift: Swift, bankName: "Raiffeisenbank", holderName: HolderName, confirmedAt: DateTime.UtcNow);

    public PayoutReadContractTests()
    {
        var user = User.CreateWithPassword("cleaner@cleansia.cz", "Password1", "First", "Last");
        user.Id = UserId;
        var employee = Employee.CreateWithUser(user);
        employee.Id = EmployeeId;

        _employees.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        _payoutDetails
            .Setup(r => r.GetByEmployeeIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_details);
    }

    private static IUserSessionProvider AdminSession() =>
        new TestUserSessionProvider("admin-1", "admin@cleansia.test",
            [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())]);

    private static IUserSessionProvider EmployeeSession() =>
        new TestUserSessionProvider(UserId, "cleaner@cleansia.cz",
            [new Claim(ClaimTypes.Role, UserProfile.Employee.ToString())]);

    private GetEmployeePayoutDetails.Handler CreateMaskedHandler(IUserSessionProvider session) =>
        new(_payoutDetails.Object, _orderAccess.Object, session);

    [Fact]
    public async Task The_Admin_Default_Read_Carries_No_Payout_Identifier()
    {
        var result = await CreateMaskedHandler(AdminSession())
            .Handle(new GetEmployeePayoutDetails.Query(EmployeeId), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var body = JsonSerializer.Serialize(result.Value);
        Assert.DoesNotContain(AccountNumber, body);
        Assert.DoesNotContain(Iban, body);
        Assert.DoesNotContain(Swift, body);
        Assert.DoesNotContain(HolderName, body);
        Assert.DoesNotContain(AccountNumber[..6], body);
        Assert.Equal("****8003", result.Value.MaskedAccount);
    }

    [Fact]
    public async Task A_Cleaner_Reading_Another_Cleaners_Record_Gets_NotFound_Not_Forbidden()
    {
        _orderAccess
            .Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("emp-2");

        var result = await CreateMaskedHandler(EmployeeSession())
            .Handle(new GetEmployeePayoutDetails.Query(EmployeeId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.PayoutDetailsNotFound, result.Error!.Message);
    }

    [Fact]
    public async Task A_Cleaner_Reading_Their_Own_Record_Gets_The_Masked_View()
    {
        _orderAccess
            .Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmployeeId);

        var result = await CreateMaskedHandler(EmployeeSession())
            .Handle(new GetEmployeePayoutDetails.Query(EmployeeId), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task The_Reveal_Returns_The_Full_Value_And_Stamps_The_Record()
    {
        var auditContext = new AuditContext();
        var handler = new RevealEmployeePayoutDetails.Handler(
            _employees.Object, _payoutDetails.Object, auditContext);

        var result = await handler.Handle(new RevealEmployeePayoutDetails.Command(EmployeeId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Iban, result.Value.Iban);
        Assert.Equal(AccountNumber, result.Value.AccountNumber);
        Assert.Equal(1, _details.RevealCount);
        Assert.NotNull(_details.LastRevealedAt);
    }

    [Fact]
    public async Task The_Reveal_Records_An_Audit_Snapshot_Of_Ids_Only()
    {
        var auditContext = new AuditContext();
        var handler = new RevealEmployeePayoutDetails.Handler(
            _employees.Object, _payoutDetails.Object, auditContext);

        await handler.Handle(new RevealEmployeePayoutDetails.Command(EmployeeId), CancellationToken.None);

        var snapshot = auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("User", snapshot!.ResourceType);
        Assert.Equal(UserId, snapshot.ResourceId);

        foreach (var json in new[] { snapshot.BeforeJson, snapshot.AfterJson })
        {
            Assert.NotNull(json);
            Assert.DoesNotContain(AccountNumber, json);
            Assert.DoesNotContain(Iban, json);
            Assert.DoesNotContain(Swift, json);
            Assert.DoesNotContain(HolderName, json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("5500", json);
        }
    }

    [Fact]
    public async Task Revealing_A_Missing_Record_Fails_Without_Confirming_Anything()
    {
        _payoutDetails
            .Setup(r => r.GetByEmployeeIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeePayoutDetails?)null);

        var handler = new RevealEmployeePayoutDetails.Handler(
            _employees.Object, _payoutDetails.Object, new AuditContext());

        var result = await handler.Handle(new RevealEmployeePayoutDetails.Command(EmployeeId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.PayoutDetailsNotFound, result.Error!.Message);
    }
}
