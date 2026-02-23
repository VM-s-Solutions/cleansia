namespace Cleansia.Core.AppServices.Authentication;

public class Policy
{
    // Code
    public const string CanViewCodeOverview = nameof(CanViewCodeOverview); // Anonymous

    // Global Search
    public const string CanPerformGlobalSearch = nameof(CanPerformGlobalSearch); // Anonymous

    // Order
    public const string CanViewPagedOrder = nameof(CanViewPagedOrder); // Admin + Employee
    public const string CanViewPagedUserOrder = nameof(CanViewPagedUserOrder); // Authenticated (All roles)
    public const string CanViewOrderDetail = nameof(CanViewOrderDetail); // Authenticated (All roles) + Admin + Employee
    public const string CanViewOrderDetailWithOrderNumberAndEmail = nameof(CanViewOrderDetailWithOrderNumberAndEmail); // Anonymous
    public const string CanUpdateOrder = nameof(CanUpdateOrder); // Admin + Employee
    public const string CanCreateOrder = nameof(CanCreateOrder); // Anonymous
    public const string CanGetOrderStatus = nameof(CanGetOrderStatus); // Anonymous
    public const string CanTakeOrder = nameof(CanTakeOrder); // Employee
    public const string CanStartOrder = nameof(CanStartOrder); // Employee
    public const string CanCompleteOrder = nameof(CanCompleteOrder); // Employee
    public const string CanUploadOrderPhoto = nameof(CanUploadOrderPhoto); // Employee
    public const string CanViewOrderPhotos = nameof(CanViewOrderPhotos); // Authenticated (All roles)
    public const string CanDeleteOrderPhoto = nameof(CanDeleteOrderPhoto); // Employee
    public const string CanAddOrderNote = nameof(CanAddOrderNote); // Employee
    public const string CanReportOrderIssue = nameof(CanReportOrderIssue); // Employee

    // User
    public const string CanViewPagedUser = nameof(CanViewPagedUser); // Admin + Employee
    public const string CanViewUserDetail = nameof(CanViewUserDetail); // Authenticated (All roles) + Admin + Employee
    public const string CanGetCurrentUser = nameof(CanGetCurrentUser); // Authenticated (All roles)
    public const string CanRequestPasswordChange = nameof(CanRequestPasswordChange); // Authenticated (All roles)
    public const string CanChangePassword = nameof(CanChangePassword);
    public const string CanUpdateCurrentUser = nameof(CanUpdateCurrentUser); // Authenticated (All roles)
    public const string CanAddPhoneNumber = nameof(CanAddPhoneNumber); // Authenticated (All roles)

    // Employee
    public const string CanGetCurrentEmployee = nameof(CanGetCurrentEmployee); // Authenticated (All roles)
    public const string CanCheckCurrentEmployee = nameof(CanCheckCurrentEmployee); // Authenticated (All roles)
    public const string CanUpdateCurrentEmployee = nameof(CanUpdateCurrentEmployee); // Authenticated (All roles)
    public const string CanViewPagedEmployee = nameof(CanViewPagedEmployee); // Admin
    public const string CanApproveEmployee = nameof(CanApproveEmployee); // Admin
    public const string CanRejectEmployee = nameof(CanRejectEmployee); // Admin
    public const string CanAdminUpdateEmployee = nameof(CanAdminUpdateEmployee); // Admin

    // Employee Documents
    public const string CanViewEmployeeDocuments = nameof(CanViewEmployeeDocuments); // Admin + Employee (own documents)
    public const string CanUploadEmployeeDocument = nameof(CanUploadEmployeeDocument); // Admin + Employee (own documents)
    public const string CanDownloadEmployeeDocument = nameof(CanDownloadEmployeeDocument); // Admin + Employee (own documents)
    public const string CanApproveEmployeeDocument = nameof(CanApproveEmployeeDocument); // Admin
    public const string CanRejectEmployeeDocument = nameof(CanRejectEmployeeDocument); // Admin
    public const string CanDeleteEmployeeDocument = nameof(CanDeleteEmployeeDocument); // Admin + Employee (own documents)

    // Employee Payroll
    public const string CanViewPagedInvoices = nameof(CanViewPagedInvoices); // Admin
    public const string CanViewPeriodPays = nameof(CanViewPeriodPays); // Admin + Employee (own data)
    public const string CanCalculateOrderPay = nameof(CanCalculateOrderPay); // Admin
    public const string CanGenerateInvoice = nameof(CanGenerateInvoice); // Admin
    public const string CanApproveInvoice = nameof(CanApproveInvoice); // Admin
    public const string CanMarkInvoicePaid = nameof(CanMarkInvoicePaid); // Admin
    public const string CanCancelInvoice = nameof(CanCancelInvoice); // Admin
    public const string CanClosePayPeriod = nameof(CanClosePayPeriod); // Admin

    // Pay Period
    public const string CanViewPayPeriods = nameof(CanViewPayPeriods); // Admin + Employee
    public const string CanViewPayPeriod = nameof(CanViewPayPeriod); // Admin + Employee
    public const string CanCreatePayPeriod = nameof(CanCreatePayPeriod); // Admin
    public const string CanUpdatePayPeriod = nameof(CanUpdatePayPeriod); // Admin
    public const string CanOpenPayPeriod = nameof(CanOpenPayPeriod); // Admin
    public const string CanDeletePayPeriod = nameof(CanDeletePayPeriod); // Admin

    // Pay Config
    public const string CanViewPayConfigs = nameof(CanViewPayConfigs); // Admin
    public const string CanViewPayConfig = nameof(CanViewPayConfig); // Admin
    public const string CanCreatePayConfig = nameof(CanCreatePayConfig); // Admin
    public const string CanUpdatePayConfig = nameof(CanUpdatePayConfig); // Admin
    public const string CanDeletePayConfig = nameof(CanDeletePayConfig); // Admin

    // Dispute
    public const string CanCreateDispute = nameof(CanCreateDispute); // Authenticated (Customers can create disputes)
    public const string CanViewDispute = nameof(CanViewDispute); // Authenticated (Users can view their own disputes)
    public const string CanViewDisputeList = nameof(CanViewDisputeList); // Authenticated (Users can view their dispute list)
    public const string CanRespondToDispute = nameof(CanRespondToDispute); // Admin (Only admins can respond/add messages)
    public const string CanResolveDispute = nameof(CanResolveDispute); // Admin (Only admins can resolve disputes)
    public const string CanUpdateDisputeStatus = nameof(CanUpdateDisputeStatus); // Admin (Only admins can update status)

    // Reports
    public const string CanViewRevenueReport = nameof(CanViewRevenueReport); // Admin
    public const string CanViewPayrollReport = nameof(CanViewPayrollReport); // Admin

    // Services
    public const string CanViewServices = nameof(CanViewServices); // Admin
    public const string CanCreateService = nameof(CanCreateService); // Admin
    public const string CanUpdateService = nameof(CanUpdateService); // Admin
    public const string CanDeleteService = nameof(CanDeleteService); // Admin

    // Packages
    public const string CanViewPackages = nameof(CanViewPackages); // Admin
    public const string CanCreatePackage = nameof(CanCreatePackage); // Admin
    public const string CanUpdatePackage = nameof(CanUpdatePackage); // Admin
    public const string CanDeletePackage = nameof(CanDeletePackage); // Admin

    // Languages
    public const string CanViewLanguages = nameof(CanViewLanguages); // Admin
    public const string CanCreateLanguage = nameof(CanCreateLanguage); // Admin
    public const string CanUpdateLanguage = nameof(CanUpdateLanguage); // Admin
    public const string CanDeleteLanguage = nameof(CanDeleteLanguage); // Admin

    // Countries
    public const string CanViewCountries = nameof(CanViewCountries); // Admin
    public const string CanCreateCountry = nameof(CanCreateCountry); // Admin
    public const string CanUpdateCountry = nameof(CanUpdateCountry); // Admin
    public const string CanDeleteCountry = nameof(CanDeleteCountry); // Admin

    // Currencies
    public const string CanViewCurrencies = nameof(CanViewCurrencies); // Admin
    public const string CanCreateCurrency = nameof(CanCreateCurrency); // Admin
    public const string CanUpdateCurrency = nameof(CanUpdateCurrency); // Admin
    public const string CanDeleteCurrency = nameof(CanDeleteCurrency); // Admin

    // Admin Users
    public const string CanViewAdminUsers = nameof(CanViewAdminUsers); // SuperAdmin
    public const string CanCreateAdminUser = nameof(CanCreateAdminUser); // SuperAdmin
    public const string CanUpdateAdminUser = nameof(CanUpdateAdminUser); // SuperAdmin
    public const string CanDeactivateAdminUser = nameof(CanDeactivateAdminUser); // SuperAdmin
    public const string CanActivateAdminUser = nameof(CanActivateAdminUser); // SuperAdmin

    // Company Info
    public const string CanViewCompanyInfo = nameof(CanViewCompanyInfo); // Admin
    public const string CanCreateCompanyInfo = nameof(CanCreateCompanyInfo); // Admin
    public const string CanUpdateCompanyInfo = nameof(CanUpdateCompanyInfo); // Admin
    public const string CanDeleteCompanyInfo = nameof(CanDeleteCompanyInfo); // Admin

    // Invoice Templates
    public const string CanViewInvoiceTemplates = nameof(CanViewInvoiceTemplates); // Admin
    public const string CanCreateInvoiceTemplate = nameof(CanCreateInvoiceTemplate); // Admin
    public const string CanUpdateInvoiceTemplate = nameof(CanUpdateInvoiceTemplate); // Admin
    public const string CanDeleteInvoiceTemplate = nameof(CanDeleteInvoiceTemplate); // Admin
    public const string CanActivateInvoiceTemplate = nameof(CanActivateInvoiceTemplate); // Admin

    // Receipt Templates
    public const string CanViewReceiptTemplates = nameof(CanViewReceiptTemplates); // Admin
    public const string CanCreateReceiptTemplate = nameof(CanCreateReceiptTemplate); // Admin
    public const string CanUpdateReceiptTemplate = nameof(CanUpdateReceiptTemplate); // Admin
    public const string CanDeleteReceiptTemplate = nameof(CanDeleteReceiptTemplate); // Admin
    public const string CanActivateReceiptTemplate = nameof(CanActivateReceiptTemplate); // Admin

    // Email Templates
    public const string CanViewEmailTemplates = nameof(CanViewEmailTemplates); // Admin
    public const string CanUpdateEmailTemplate = nameof(CanUpdateEmailTemplate); // Admin

    // Device
    public const string Authenticated = nameof(Authenticated); // Authenticated (All roles)
}
