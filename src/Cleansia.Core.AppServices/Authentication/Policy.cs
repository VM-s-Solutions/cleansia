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
    public const string CanCompleteOrder = nameof(CanCompleteOrder); // Employee
    public const string CanUploadOrderPhoto = nameof(CanUploadOrderPhoto); // Employee
    public const string CanViewOrderPhotos = nameof(CanViewOrderPhotos); // Authenticated (All roles)
    public const string CanDeleteOrderPhoto = nameof(CanDeleteOrderPhoto); // Employee

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
}
