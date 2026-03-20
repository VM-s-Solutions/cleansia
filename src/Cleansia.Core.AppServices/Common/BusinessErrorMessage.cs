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
    public const string InsufficientPrivileges = "auth.insufficient_privileges";
    
    // Common
    public const string InvalidEnumValue = "common.invalid_enum_value";
    public const string MaxLength = "common.max_length";
    public const string MinLength = "common.min_length";
    public const string Required = "common.required";
    
    // Currency
    public const string InvalidCurrency = "currency.invalid";
    public const string CurrencyNotFound = "currency.not_found";
    public const string CurrencyCodeAlreadyExists = "currency.code_already_exists";
    public const string CurrencyInUse = "currency.in_use";
    public const string CannotDeleteDefaultCurrency = "currency.cannot_delete_default";
    public const string ExchangeRateMustBePositive = "currency.exchange_rate_must_be_positive";
    
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
    public const string OrderNotConfirmed = "order.not_confirmed";
    public const string EmployeeAlreadyHasOrderInProgress = "order.employee_already_has_order_in_progress";
    public const string ActualTimeMustBePositive = "order.actual_time.positive";
    public const string CompletionNotesRequired = "order.completion_notes.required";
    public const string CompletionNotesTooLong = "order.completion_notes.too_long";
    public const string AfterPhotosRequired = "order.after_photos.required";
    public const string OrderNoteContentRequired = "order.note.content_required";
    public const string OrderIssueDescriptionRequired = "order.issue.description_required";
    public const string PaymentGatewayUnavailable = "order.payment_gateway_unavailable";
    public const string OrderCreationFailed = "order.creation_failed";
    public const string OrderNotCompleted = "order.not_completed";
    public const string ReviewAlreadyExists = "order.review.already_exists";
    public const string ReviewRatingInvalid = "order.review.rating_invalid";
    public const string OrderNotOwnedByUser = "order.not_owned_by_user";

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
    public const string LanguageCodeAlreadyExists = "language.code_already_exists";
    public const string LanguageInUse = "language.in_use";

    // Country
    public const string NotExistingCountryWithId = "country.not_existing_id";
    public const string CountryNotFound = "country.not_found";
    public const string CountryIsoCodeAlreadyExists = "country.iso_code_already_exists";
    public const string CountryInUse = "country.in_use";

    // Company
    public const string CompanyInfoNotFound = "company.not_found";
    public const string CompanyInfoExistsForCountry = "company.exists_for_country";
    public const string CompanyInfoInUse = "company.in_use";

    // Dispute
    public const string DisputeNotFound = "dispute.not_found";
    public const string DisputeAlreadyExists = "dispute.already_exists";
    public const string InvalidRefundAmount = "dispute.invalid_refund_amount";
    public const string MaxLengthExceeded = "dispute.max_length_exceeded";
    public const string UserNotFound = "user.not_found";

    // Admin User
    public const string AdminUserNotFound = "admin_user.not_found";
    public const string AdminUserEmailExists = "admin_user.email_exists";
    public const string CannotDeactivateSelf = "admin_user.cannot_deactivate_self";
    public const string CannotDeleteSelf = "admin_user.cannot_delete_self";

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

    // Service
    public const string ServiceNotFound = "service.not_found";
    public const string ServiceInUse = "service.in_use";
    public const string TranslationsRequired = "service.translations_required";
    public const string MissingTranslationForLanguage = "service.missing_translation_for_language";

    // Package
    public const string PackageNotFound = "package.not_found";
    public const string PackageInUse = "package.in_use";

    // Common Validation
    public const string MustBePositive = "validation.must_be_positive";

    // General
    public const string NotFound = "general.not_found";

    // Invoice Template
    public const string InvoiceTemplateNotFound = "template.invoice.not_found";
    public const string InvoiceTemplateInUse = "template.invoice.in_use";
    public const string CannotDeleteActiveTemplate = "template.cannot_delete_active";

    // Receipt Template
    public const string ReceiptTemplateNotFound = "template.receipt.not_found";
    public const string ReceiptTemplateInUse = "template.receipt.in_use";

    // Email Template
    public const string EmailTemplateNotFound = "template.email.not_found";
    public const string EmailTemplateKeyExists = "template.email.key_exists";
    public const string InvalidEmailType = "template.email.invalid_type";
    public const string InvalidEmail = "email.invalid_email";

    // Template File
    public const string InvalidTemplateFileType = "template.invalid_file_type";
    public const string TemplateFileSizeExceeded = "template.file_size_exceeded";
    public const string TemplateFileRequired = "template.file_required";

    // Availability
    public const string InvalidAvailabilityFormat = "validation.invalid_availability_format";

    // Device
    public const string InvalidPlatform = "device.invalid_platform";

    // Feature Flag
    public const string FeatureFlagNotFound = "feature_flag.not_found";
    public const string FeatureFlagAlreadyExists = "feature_flag.already_exists";

    // Tenant Configuration
    public const string TenantConfigNotFound = "tenant_config.not_found";
    public const string TenantConfigKeyAlreadyExists = "tenant_config.key_already_exists";

    // Country Configuration
    public const string CountryConfigNotFound = "country_config.not_found";
    public const string CountryConfigAlreadyExists = "country_config.already_exists_for_country";

    // Pagination
    public const string PageMustBePositive = "validation.page_must_be_positive";
    public const string PageSizeMustBePositive = "validation.page_size_must_be_positive";
    public const string PageSizeExceeded = "validation.page_size_exceeded";
    public const string InvalidContractStatus = "validation.invalid_contract_status";

    // Document Upload
    public const string FileSizeExceeded10MB = "file.size_exceeded_10mb";
    public const string FileTypeNotAllowed = "file.type_not_allowed";

    // Payment
    public const string JsonPayloadRequired = "payment.json_payload_required";
    public const string StripeSignatureRequired = "payment.stripe_signature_required";

    // GDPR
    public const string GdprExportFailed = "gdpr.export_failed";
    public const string GdprDeletionFailed = "gdpr.deletion_failed";
    public const string GdprDeletionAlreadyPending = "gdpr.deletion_already_pending";
    public const string ConsentNotFound = "gdpr.consent_not_found";
    public const string ConsentAlreadyGranted = "gdpr.consent_already_granted";
}
