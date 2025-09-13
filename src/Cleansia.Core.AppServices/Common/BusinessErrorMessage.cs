namespace Cleansia.Core.AppServices.Common;

public static class BusinessErrorMessage
{
    // Auth
    public const string GoogleAuthTypeError = "auth.google_type_error";
    public const string InternalAuthTypeError = "auth.internal_type_error";
    public const string InvalidConfirmationCode = "auth.invalid_confirmation_code";
    public const string InvalidGoogleUserToken = "auth.invalid_google_token";
    public const string InvalidPasswordFormat = "auth.invalid_password_format";
    public const string NotValidResetPasswordToken = "auth.invalid_reset_token";
    public const string SameResetPassword = "auth.same_reset_password";
    
    // Common
    public const string InvalidEnumValue = "common.invalid_enum_value";
    public const string MaxLength = "common.max_length";
    public const string Required = "common.required";
    
    // Currency
    public const string InvalidCurrency = "currency.invalid";
    
    // Email
    public const string EmailNotSentError = "email.sending_failed";
    public const string InvalidEmailFormat = "email.invalid_format";

    // Order
    public const string CleaningDateInFuture = "order.cleaning_date.future";
    public const string EmptyOrder = "order.empty";
    public const string InvalidSelectedPackage = "order.selected_package.invalid";
    public const string InvalidSelectedServices = "order.selected_services.invalid";
    public const string TotalPriceMustBePositive = "order.total_price.positive";
    public const string TotalPriceNotMatch = "order.total_price.not_match";

    // User
    public const string EmailConfirmed = "user.email_confirmed";
    public const string ExistingUserWithEmail = "user.existing_email";
    public const string NotExistingUserWithEmail = "user.not_existing_email";
    public const string NotExistingUserWithId = "user.not_existing_id";
    public const string NotAllowedToUpdateUser = "user.not_allowed_to_update";
    public const string ExistingPhoneNumber = "user.existing_phone_number";

    // File
    public const string FileNotMatchContentType = "file.content_type_doesnt_match";

    // Language
    public const string LanguageNotSupported = "language.not_supported";

    // Validation
    public const string InvalidPassword = "validation.invalid_password";
    public const string InvalidDate = "validation.invalid_date";
    public const string DateMustBeInPast = "validation.date_must_be_in_past";
    public const string InvalidAge = "validation.invalid_age";
}