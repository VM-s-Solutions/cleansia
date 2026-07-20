namespace Cleansia.Core.Queue.Abstractions;

public static class QueueNames
{
    public const string GenerateReceipt = "generate-receipt";
    public const string GenerateInvoice = "generate-invoice";
    public const string NotificationsDispatch = "notifications-dispatch";
    public const string SitewidePromoFanout = "sitewide-promo-fanout";
    public const string CalculateOrderPay = "calculate-order-pay";
    public const string SendEmail = "send-email";
    public const string LiveActivityDispatch = "live-activity-dispatch";
}
