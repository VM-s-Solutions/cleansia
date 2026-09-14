using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Auditing;

/// <summary>
/// The refusal KEY a failure row records. <c>Error(code, message)</c> carries the FIELD name in
/// <c>Code</c> and the <c>BusinessErrorMessage</c> key in <c>Message</c>, and a validation reject's
/// top-level <c>Error</c> is the generic <c>ValidationError</c> sentinel — so a row that stored
/// <c>Error.Code</c> read "OrderId" or "ValidationError" and proved nothing. A validation reject that
/// travelled as a throw (the response type could not carry it) resolves to the same first rule's key as
/// one that came back as a result; every other exception path is not here — the behaviors record the
/// exception type name directly.
/// </summary>
public static class AuditErrorCode
{
    public static string? Resolve(BusinessResult result)
    {
        var error = result is IValidationResult { Errors.Length: > 0 } validation
            ? validation.Errors[0]
            : result.Error;

        return KeyOf(error);
    }

    public static string? Resolve(Error[] validationErrors) =>
        KeyOf(validationErrors.Length > 0 ? validationErrors[0] : null);

    private static string? KeyOf(Error? error)
    {
        if (error is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(error.Message) ? error.Code : error.Message;
    }
}
