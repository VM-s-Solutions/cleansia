namespace Cleansia.Core.AppServices.Common;

public static class BusinessErrorMessage
{
    public const string Required = "common.required";
    public const string MaxLength = "common.max_length";
    public const string InvalidEmailFormat = "email.invalid_format";
    public const string CleaningDateInFuture = "order.cleaning_date.future";
    public const string TotalPriceMustBePositive = "order.total_price.positive";
    public const string InvalidCurrency = "currency.invalid";
    public const string InvalidSelectedServices = "order.selected_services.invalid";
    public const string InvalidSelectedPackage = "order.selected_package.invalid";
    public const string EmptyOrder = "order.empty";
    public const string TotalPriceNotMatch = "order.total_price.not_match";
    public const string InvalidEnumValue = "common.invalid_enum_value";
    public const string EmailNotSentError = "email.sending_failed";
    public const string InvalidPasswordFormat = "auth.invalid_password_format";
    public const string NotExistingUserWithEmail = "user.not_existing_email";
    public const string InvalidPassword = "validation.invalid_password";
    public const string GoogleAuthTypeError = "auth.google_type_error";
    public const string ExistingUserWithEmail = "user.existing_email";
    public const string InvalidConfirmationCode = "auth.invalid_confirmation_code";
    public const string EmailConfirmed = "user.email_confirmed";
    public const string InvalidGoogleUserToken = "auth.invalid_google_token";
    public const string InternalAuthTypeError = "auth.internal_type_error";
}