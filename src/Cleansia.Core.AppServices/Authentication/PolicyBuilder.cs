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
    };

    public static string ToPhysicalPolicy(this string permission) =>
        Map.GetValueOrDefault(permission, PhysicalPolicy.Authenticated);
}