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

    // User
    public const string CanViewPagedUser = nameof(CanViewPagedUser); // Admin + Employee
    public const string CanViewUserDetail = nameof(CanViewUserDetail); // Authenticated (All roles) + Admin + Employee
    public const string CanGetCurrentUser = nameof(CanGetCurrentUser); // Authenticated (All roles)
    public const string CanRequestPasswordChange = nameof(CanRequestPasswordChange); // Authenticated (All roles)
    public const string CanChangePassword = nameof(CanChangePassword);
    public const string CanUpdateCurrentUser = nameof(CanUpdateCurrentUser); // Authenticated (All roles)
    public const string CanAddPhoneNumber = nameof(CanAddPhoneNumber); // Authenticated (All roles)
}