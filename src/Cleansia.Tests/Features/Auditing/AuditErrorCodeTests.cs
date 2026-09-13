using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0062 D1 ("the error code is the refusal KEY, not the field") — <c>Error(code, message)</c> puts
/// the field name in <c>Code</c> and the <c>BusinessErrorMessage</c> key in <c>Message</c>, and a
/// validation reject's top-level Error is the generic sentinel. A row that read "OrderId" or
/// "ValidationError" proved nothing. Pure logic, red-first.
/// </summary>
public sealed class AuditErrorCodeTests
{
    [Fact]
    public void A_Handler_Returned_Failure_Resolves_To_The_Key_Not_The_Field_Name()
    {
        var result = BusinessResult.Failure(new Error("OrderId", BusinessErrorMessage.OrderInProgressCannotCancel));

        Assert.Equal(BusinessErrorMessage.OrderInProgressCannotCancel, AuditErrorCode.Resolve(result));
    }

    [Fact]
    public void A_Generic_Failure_Resolves_The_Same_Way()
    {
        var result = BusinessResult.Failure<string>(new Error("OrderId", BusinessErrorMessage.OrderNotFound));

        Assert.Equal(BusinessErrorMessage.OrderNotFound, AuditErrorCode.Resolve(result));
    }

    [Fact]
    public void A_Validation_Reject_Resolves_To_The_First_Rule_Failures_Key_Not_The_Sentinel()
    {
        var result = ValidationResult.WithErrors(
        [
            new Error("TotalPrice", BusinessErrorMessage.TotalPriceNotMatch),
            new Error("CurrencyId", BusinessErrorMessage.Required)
        ]);

        Assert.Equal(IValidationResult.ValidationError, result.Error);
        Assert.Equal(BusinessErrorMessage.TotalPriceNotMatch, AuditErrorCode.Resolve(result));
    }

    [Fact]
    public void A_Generic_Validation_Reject_Resolves_The_Same_Way()
    {
        var result = ValidationResult<string>.WithErrors([new Error("Email", BusinessErrorMessage.Required)]);

        Assert.Equal(BusinessErrorMessage.Required, AuditErrorCode.Resolve(result));
    }

    [Fact]
    public void A_Success_Resolves_To_Null()
    {
        Assert.Null(AuditErrorCode.Resolve(BusinessResult.Success()));
    }

    [Fact]
    public void An_Error_With_No_Message_Falls_Back_To_Its_Code_Rather_Than_Recording_Nothing()
    {
        var result = BusinessResult.Failure(new Error("stripe.declined", ""));

        Assert.Equal("stripe.declined", AuditErrorCode.Resolve(result));
    }
}
