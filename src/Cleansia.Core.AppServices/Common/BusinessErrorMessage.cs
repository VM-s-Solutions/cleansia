namespace Cleansia.Core.AppServices.Common;

public static class BusinessErrorMessage
{
    public const string Required = "common.required";
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
}