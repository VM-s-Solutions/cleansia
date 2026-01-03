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
    };

    public static string ToPhysicalPolicy(this string permission) =>
        Map.GetValueOrDefault(permission, PhysicalPolicy.Authenticated);
}