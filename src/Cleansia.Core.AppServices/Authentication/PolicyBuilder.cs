namespace Cleansia.Core.AppServices.Authentication;

public static class PolicyBuilder
{

    private static readonly Dictionary<string, string> Map = new()
    {
        // Code
        //[Policy.CanViewCodeOverview] = PhysicalPolicy.Anonymous,

        // Global Search
        //[Policy.CanPerformGlobalSearch] = PhysicalPolicy.Anonymous,

        // Order
        [Policy.CanViewPagedOrder] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanViewPagedUserOrder] = PhysicalPolicy.Authenticated,
        [Policy.CanViewOrderDetail] = PhysicalPolicy.Authenticated,
        [Policy.CanUpdateOrder] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanTakeOrder] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanCompleteOrder] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanUploadOrderPhoto] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanViewOrderPhotos] = PhysicalPolicy.Authenticated,
        [Policy.CanDeleteOrderPhoto] = PhysicalPolicy.EmployeeOrAdmin,
        //[Policy.CanCreateOrder] = PhysicalPolicy.Anonymous,
        //[Policy.CanGetOrderStatus] = PhysicalPolicy.Anonymous,

        // User
        [Policy.CanViewPagedUser] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanViewUserDetail] = PhysicalPolicy.OwnerOrElevated,
        [Policy.CanGetCurrentUser] = PhysicalPolicy.Authenticated,
        //[Policy.CanRequestPasswordChange] = PhysicalPolicy.Anonymous,
        //[Policy.CanChangePassword] = PhysicalPolicy.Anonymous,
        [Policy.CanUpdateCurrentUser] = PhysicalPolicy.Authenticated,
        [Policy.CanAddPhoneNumber] = PhysicalPolicy.Authenticated,

        // Employee
        [Policy.CanGetCurrentEmployee] = PhysicalPolicy.Authenticated,
        [Policy.CanCheckCurrentEmployee] = PhysicalPolicy.Authenticated,
        [Policy.CanUpdateCurrentEmployee] = PhysicalPolicy.Authenticated,
        [Policy.CanViewPagedEmployee] = PhysicalPolicy.AdminOnly,
        [Policy.CanApproveEmployee] = PhysicalPolicy.AdminOnly,
        [Policy.CanRejectEmployee] = PhysicalPolicy.AdminOnly,

        // Employee Documents
        [Policy.CanViewEmployeeDocuments] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanUploadEmployeeDocument] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanDownloadEmployeeDocument] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanApproveEmployeeDocument] = PhysicalPolicy.AdminOnly,
        [Policy.CanRejectEmployeeDocument] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeleteEmployeeDocument] = PhysicalPolicy.EmployeeOrAdmin,

        // Dispute
        [Policy.CanCreateDispute] = PhysicalPolicy.CustomerOnly,
        [Policy.CanViewDispute] = PhysicalPolicy.CustomerOnly,
        [Policy.CanViewDisputeList] = PhysicalPolicy.CustomerOnly,
        [Policy.CanRespondToDispute] = PhysicalPolicy.AdminOnly,
        [Policy.CanResolveDispute] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateDisputeStatus] = PhysicalPolicy.AdminOnly,

        // Reports
        [Policy.CanViewRevenueReport] = PhysicalPolicy.AdminOnly,
        [Policy.CanViewPayrollReport] = PhysicalPolicy.AdminOnly,

        // Services
        [Policy.CanViewServices] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateService] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateService] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeleteService] = PhysicalPolicy.AdminOnly,

        // Packages
        [Policy.CanViewPackages] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreatePackage] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdatePackage] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeletePackage] = PhysicalPolicy.AdminOnly,

        // Languages
        [Policy.CanViewLanguages] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateLanguage] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateLanguage] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeleteLanguage] = PhysicalPolicy.AdminOnly,

        // Countries
        [Policy.CanViewCountries] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateCountry] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateCountry] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeleteCountry] = PhysicalPolicy.AdminOnly,

        // Currencies
        [Policy.CanViewCurrencies] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateCurrency] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateCurrency] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeleteCurrency] = PhysicalPolicy.AdminOnly,

        // Admin Users
        [Policy.CanViewAdminUsers] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateAdminUser] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateAdminUser] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeactivateAdminUser] = PhysicalPolicy.AdminOnly,
        [Policy.CanActivateAdminUser] = PhysicalPolicy.AdminOnly,

        // Company Info
        [Policy.CanViewCompanyInfo] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateCompanyInfo] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateCompanyInfo] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeleteCompanyInfo] = PhysicalPolicy.AdminOnly,

        // Invoice Templates
        [Policy.CanViewInvoiceTemplates] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateInvoiceTemplate] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateInvoiceTemplate] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeleteInvoiceTemplate] = PhysicalPolicy.AdminOnly,
        [Policy.CanActivateInvoiceTemplate] = PhysicalPolicy.AdminOnly,

        // Receipt Templates
        [Policy.CanViewReceiptTemplates] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateReceiptTemplate] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateReceiptTemplate] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeleteReceiptTemplate] = PhysicalPolicy.AdminOnly,
        [Policy.CanActivateReceiptTemplate] = PhysicalPolicy.AdminOnly,

        // Email Templates
        [Policy.CanViewEmailTemplates] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateEmailTemplate] = PhysicalPolicy.AdminOnly,
    };

    public static string ToPhysicalPolicy(this string permission) =>
        Map.GetValueOrDefault(permission, PhysicalPolicy.Authenticated);
}