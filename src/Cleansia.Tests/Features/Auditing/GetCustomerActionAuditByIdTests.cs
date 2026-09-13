using System.Reflection;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auditing;
using Cleansia.Core.AppServices.Features.Auditing.DTOs;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Moq;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0062 D6 (handler + validator slice): the single-row customer audit read returns the payload and
/// the three request-metadata columns the paged cut withholds, resolves a <c>currencyId</c> in the
/// payload to its code for display (the writer records the id it already holds), fails not-found when
/// the tenant-filtered repository yields nothing, and its DTO carries exactly the persisted columns —
/// no <c>TenantId</c>.
/// </summary>
public class GetCustomerActionAuditByIdTests
{
    private const string AuditId = "aud-1";
    private const string CurrencyId = "01CURRENCYCZK0000000000001";

    private readonly Mock<ICustomerActionAuditRepository> _repository = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();

    private static CustomerActionAudit Row(string? payloadJson)
    {
        var row = CustomerActionAudit.Create(
            userId: "user-1", clientAudience: "cleansia.customer", ipAddress: "203.0.113.9", deviceLabel: "iPhone 15 / iOS 17.4",
            deviceId: "device-abc", action: "customer.order.cancel", resourceType: "Order", resourceId: "order-1",
            success: true, errorCode: null, payloadJson: payloadJson, correlationId: "corr-1");
        row.Id = AuditId;
        return row;
    }

    private async Task<BusinessResult<CustomerActionAuditDetailDto>> InvokeHandler(string auditId)
    {
        var handlerType = typeof(GetCustomerActionAuditById).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = Activator.CreateInstance(handlerType, _repository.Object, _currencies.Object)!;
        var method = handlerType.GetMethod("Handle")!;
        var task = (Task<BusinessResult<CustomerActionAuditDetailDto>>)method.Invoke(
            handler, [new GetCustomerActionAuditById.Query(auditId), CancellationToken.None])!;
        return await task;
    }

    [Fact]
    public async Task Returns_The_Row_With_Its_Payload_And_Request_Metadata_And_Resolves_The_Currency()
    {
        _repository
            .Setup(r => r.GetByIdAsync(AuditId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Row("{\"feeRate\":0.5,\"currencyId\":\"" + CurrencyId + "\"}"));
        _currencies
            .Setup(r => r.GetByIdAsync(CurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Currency.Create("CZK", "Kč", "Czech koruna"));

        var result = await InvokeHandler(AuditId);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(AuditId, dto.Id);
        Assert.Equal("user-1", dto.UserId);
        Assert.Equal("203.0.113.9", dto.IpAddress);
        Assert.Equal("iPhone 15 / iOS 17.4", dto.DeviceLabel);
        Assert.Equal("device-abc", dto.DeviceId);
        Assert.Contains("\"feeRate\":0.5", dto.PayloadJson);
        Assert.Equal("CZK", dto.CurrencyCode);
    }

    [Fact]
    public async Task A_Payload_Without_A_Currency_Resolves_No_Code_And_Asks_No_Repository()
    {
        _repository
            .Setup(r => r.GetByIdAsync(AuditId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Row("{\"consentType\":\"termsOfService\"}"));

        var result = await InvokeHandler(AuditId);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.CurrencyCode);
        _currencies.Verify(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task An_Unknown_Currency_Id_Leaves_The_Code_Null_Rather_Than_Failing_The_Read()
    {
        _repository
            .Setup(r => r.GetByIdAsync(AuditId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Row("{\"currencyId\":\"gone\"}"));
        _currencies
            .Setup(r => r.GetByIdAsync("gone", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Currency?)null);

        var result = await InvokeHandler(AuditId);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.CurrencyCode);
    }

    [Fact]
    public async Task Missing_Or_CrossTenant_Id_Returns_NotFound()
    {
        _repository
            .Setup(r => r.GetByIdAsync(AuditId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerActionAudit?)null);

        var result = await InvokeHandler(AuditId);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.AuditNotFound, result.Error!.Message);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("{}", null)]
    [InlineData("[]", null)]
    [InlineData("{\"currencyId\":null}", null)]
    [InlineData("{\"currencyId\":42}", null)]
    [InlineData("{\"currencyId\":\"\"}", null)]
    [InlineData("{\"before\":{\"currencyId\":\"nested\"}}", null)]
    [InlineData("{\"currencyId\":\"cur-1\"}", "cur-1")]
    public void ReadCurrencyId_Takes_Only_A_TopLevel_String(string? payloadJson, string? expected) =>
        Assert.Equal(expected, GetCustomerActionAuditById.ReadCurrencyId(payloadJson));

    [Fact]
    public void Detail_Dto_Carries_The_Persisted_Columns_And_No_TenantId()
    {
        var expected = new[]
        {
            "Id", "UserId", "ClientAudience", "IpAddress", "DeviceLabel", "DeviceId", "Action", "ResourceType",
            "ResourceId", "Success", "ErrorCode", "OccurredOn", "PayloadJson", "CorrelationId", "CurrencyCode",
        };

        var actual = typeof(CustomerActionAuditDetailDto).GetProperties().Select(p => p.Name).OrderBy(n => n);

        Assert.Equal(expected.OrderBy(n => n), actual);
    }

    [Fact]
    public async Task Validator_Rejects_Empty_Id()
    {
        var validator = new GetCustomerActionAuditById.Validator(_repository.Object);

        var result = await validator.ValidateAsync(new GetCustomerActionAuditById.Query(string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task Validator_Rejects_Unknown_Or_CrossTenant_Id_As_NotFound()
    {
        _repository.Setup(r => r.ExistsAsync(AuditId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var validator = new GetCustomerActionAuditById.Validator(_repository.Object);

        var result = await validator.ValidateAsync(new GetCustomerActionAuditById.Query(AuditId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.AuditNotFound);
    }

    [Fact]
    public async Task Validator_Accepts_An_Existing_Id()
    {
        _repository.Setup(r => r.ExistsAsync(AuditId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new GetCustomerActionAuditById.Validator(_repository.Object);

        var result = await validator.ValidateAsync(new GetCustomerActionAuditById.Query(AuditId));

        Assert.True(result.IsValid);
    }
}
