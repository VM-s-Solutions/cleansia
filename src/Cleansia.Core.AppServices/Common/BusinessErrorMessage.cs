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
    public const string CurrencyNotFound = "currency.not_found";
    
    // Email
    public const string EmailNotSentError = "email.sending_failed";
    public const string InvalidEmailFormat = "email.invalid_format";

    // Order
    public const string CleaningDateInFuture = "order.cleaning_date.future";
    public const string EmptyOrder = "order.empty";
    public const string InvalidSelectedPackage = "order.selected_package.invalid";
    public const string InvalidSelectedServices = "order.selected_services.invalid";
    public const string OrderNotFound = "order.not_found";
    public const string OrderAlreadyAssigned = "order.already_assigned";
    public const string NoAvailableSpots = "order.no_available_spots";
    public const string EmployeeAlreadyAssignedToOrder = "order.employee_already_assigned";
    public const string EmployeeNotAssignedToOrder = "order.employee_not_assigned";
    public const string MaxEmployeesExceeded = "order.max_employees_exceeded";
    public const string TotalPriceMustBePositive = "order.total_price.positive";
    public const string TotalPriceNotMatch = "order.total_price.not_match";
    public const string OrderNotInProgress = "order.not_in_progress";
    public const string ActualTimeMustBePositive = "order.actual_time.positive";
    public const string CompletionNotesRequired = "order.completion_notes.required";
    public const string CompletionNotesTooLong = "order.completion_notes.too_long";
    public const string PaymentGatewayUnavailable = "order.payment_gateway_unavailable";
    public const string OrderCreationFailed = "order.creation_failed";

    // User
    public const string EmailConfirmed = "user.email_confirmed";
    public const string ExistingUserWithEmail = "user.existing_email";
    public const string NotExistingUserWithEmail = "user.not_existing_email";
    public const string NotExistingUserWithId = "user.not_existing_id";
    public const string NotAllowedToUpdateUser = "user.not_allowed_to_update";
    public const string ExistingPhoneNumber = "user.existing_phone_number";

    // Employee
    public const string EmployeeNotFound = "employee.not_found";
    public const string NotExistingEmployeeWithEmail = "employee.not_existing_email";
    public const string NotAllowedToUpdateEmployee = "employee.not_allowed_to_update";
    public const string EmployeeProfileIncomplete = "employee.profile_incomplete";
    public const string EmployeeDocumentsMissing = "employee.documents_missing";
    public const string EmployeeAlreadyApproved = "employee.already_approved";
    public const string EmployeeAlreadyRejected = "employee.already_rejected";

    // Employee Documents
    public const string DocumentNotFound = "employee_document.not_found";
    public const string Unauthorized = "employee_document.unauthorized";
    public const string EmployeeDocumentNotOwned = "employee_document.not_owned";

    // Payroll
    public const string PayPeriodNotFound = "payroll.pay_period.not_found";
    public const string InvoiceNotFound = "payroll.invoice.not_found";
    public const string InvalidInvoiceStatus = "payroll.invoice.invalid_status";
    public const string InvoiceAlreadyExists = "payroll.invoice.already_exists";
    public const string PayPeriodNotOpen = "payroll.pay_period.not_open";
    public const string UnpaidInvoicesExist = "payroll.unpaid_invoices_exist";
    public const string PayAlreadyCalculated = "payroll.pay.already_calculated";
    public const string NoUnpaidOrderPays = "payroll.no_unpaid_order_pays";
    public const string NoActivePeriod = "payroll.no_active_period";
    public const string NoPayConfiguration = "payroll.no_pay_configuration";
    public const string NoCurrencyFound = "payroll.no_currency";
    public const string EmployeeNotAssigned = "payroll.employee_not_assigned";
    public const string PdfGenerationFailed = "payroll.invoice.pdf_generation_failed";
    public const string TemplateNotFound = "payroll.invoice.template_not_found";
    public const string CannotCancelPaidInvoice = "payroll.invoice.cannot_cancel_paid";
    public const string InvoiceAlreadyCancelled = "payroll.invoice.already_cancelled";

    // Receipt
    public const string ReceiptNotFound = "receipt.not_found";
    public const string ReceiptGenerationFailed = "receipt.generation_failed";

    // Pay Period
    public const string InvalidDuration = "pay_period.invalid_duration";
    public const string OverlappingPeriod = "pay_period.overlapping_period";
    public const string HasOrderPays = "pay_period.has_order_pays";
    public const string PayPeriodNotClosed = "pay_period.not_closed";

    // Pay Config
    public const string PayConfigServiceOrPackageRequired = "pay_config.service_or_package_required";
    public const string PayConfigCannotHaveBoth = "pay_config.cannot_have_both";
    public const string PayConfigAlreadyExists = "pay_config.already_exists";
    public const string PayConfigNotFound = "pay_config.not_found";
    public const string PayConfigHasOrderPays = "pay_config.has_order_pays";
    public const string PayConfigBasePayNegative = "pay_config.base_pay_negative";
    public const string PayConfigExtraPerRoomNegative = "pay_config.extra_per_room_negative";
    public const string PayConfigExtraPerBathroomNegative = "pay_config.extra_per_bathroom_negative";
    public const string PayConfigDistanceRateNegative = "pay_config.distance_rate_negative";
    public const string PayConfigMinimumPayNegative = "pay_config.minimum_pay_negative";
    public const string PayConfigMaximumPayNegative = "pay_config.maximum_pay_negative";
    public const string PayConfigMaximumLessThanMinimum = "pay_config.maximum_less_than_minimum";

    // File
    public const string FileNotMatchContentType = "file.content_type_doesnt_match";
    public const string InvalidFileType = "file.invalid_file_type";
    public const string FileSizeExceeded = "file.size_exceeded";
    public const string FileCountExceeded = "file.count_exceeded";
    public const string FileCountTooFew = "file.count_too_few";
    public const string FileRequired = "file.required";

    // Address
    public const string InvalidLength = "address.invalid_length";

    // Language
    public const string LanguageNotSupported = "language.not_supported";
    public const string LanguageNotFound = "language.not_found";

    // Country
    public const string NotExistingCountryWithId = "country.not_existing_id";

    // Company
    public const string CompanyInfoNotFound = "company.not_found";

    // Dispute
    public const string DisputeNotFound = "dispute.not_found";
    public const string DisputeAlreadyExists = "dispute.already_exists";
    public const string InvalidRefundAmount = "dispute.invalid_refund_amount";
    public const string MaxLengthExceeded = "dispute.max_length_exceeded";
    public const string UserNotFound = "user.not_found";

    // Validation
    public const string InvalidPassword = "validation.invalid_password";
    public const string InvalidDate = "validation.invalid_date";
    public const string DateMustBeInPast = "validation.date_must_be_in_past";
    public const string InvalidAge = "validation.invalid_age";
    public const string InvalidPhoneNumber = "validation.invalid_phone_number";
    public const string InvalidNationalId = "validation.invalid_national_id";
    public const string InvalidTaxId = "validation.invalid_tax_id";
    public const string InvalidIban = "validation.invalid_iban";
    public const string InvalidZipCode = "validation.invalid_zip_code";

    // General
    public const string NotFound = "general.not_found";
}
