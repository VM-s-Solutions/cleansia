namespace Cleansia.Core.AppServices.Common;

public class Constants
{
    public class BlobContainers
    {
        public const string UserFiles = "user-files";
        public const string BetaWhiteList = "beta-whitelist";
        public const string EmployeeDocuments = "employee-documents";
        public const string GeneratedInvoices = "generated-invoices";
        public const string GeneratedReceipts = "generated-receipts";
        public const string OrderPhotos = "order-photos";
        public const string DisputeEvidence = "dispute-evidence";
    }

    public class VirtualDirectories
    {
        public const string EmployeeDocuments = "employees/{0}/documents";
    }

    public class StripeEventType
    {
        public const string CompletedSession = "checkout.session.completed";
        public const string ExpiredSession = "checkout.session.expired";

        // PaymentIntent flow used by mobile PaymentSheet. Web's Checkout
        // Session flow remains in parallel; both event families flow through
        // the same webhook endpoint and are dispatched by Type.
        public const string PaymentIntentSucceeded = "payment_intent.succeeded";
        public const string PaymentIntentPaymentFailed = "payment_intent.payment_failed";
        public const string PaymentIntentCanceled = "payment_intent.canceled";

        // Subscription lifecycle for Cleansia Plus. Stripe is the source of
        // truth — local UserMembership rows are mirrors that webhook handlers
        // keep in sync. We act on these four; trialing / paused / etc. fold
        // into UpdateFromStripeWebhook's status-string switch.
        public const string SubscriptionCreated = "customer.subscription.created";
        public const string SubscriptionUpdated = "customer.subscription.updated";
        public const string SubscriptionDeleted = "customer.subscription.deleted";
        public const string InvoicePaymentFailed = "invoice.payment_failed";

        // Bank chargebacks (ADR-0006 D4). These carry charge + payment_intent
        // but NO order metadata, so they resolve to the Order by payment-intent,
        // not the OrderId-metadata path the other order events use.
        public const string ChargeDisputeCreated = "charge.dispute.created";
        public const string ChargeDisputeUpdated = "charge.dispute.updated";
        public const string ChargeDisputeClosed = "charge.dispute.closed";

        public static bool IsOrderEvent(string eventType) =>
            eventType is CompletedSession
                      or ExpiredSession
                      or PaymentIntentSucceeded
                      or PaymentIntentPaymentFailed
                      or PaymentIntentCanceled;

        public static bool IsSubscriptionEvent(string eventType) =>
            eventType is SubscriptionCreated
                      or SubscriptionUpdated
                      or SubscriptionDeleted
                      or InvoicePaymentFailed;

        public static bool IsChargebackEvent(string eventType) =>
            eventType is ChargeDisputeCreated
                      or ChargeDisputeUpdated
                      or ChargeDisputeClosed;
    }

    public class Language
    {
        public const string English = "en";
    }

    public class Currency
    {
        // CZK is the platform's primary fiat — fallback when an order/receipt
        // didn't capture a currency record. Multi-currency is supported via
        // the Currency entity; this is just the safety-net string default.
        public const string Czk = "CZK";
    }

    public class ReceiptNumberFormat
    {
        public const string Prefix = "RCP";
        public const string Format = "D4";
        public const string Pattern = "RCP-{0}-{1:D4}"; // RCP-YYYY-NNNN
    }

    public static readonly (byte[] Signature, string MimeType)[] ImageSignatures =
    [
        ([0xFF, 0xD8, 0xFF], "image/jpeg"),
        ([0x89, 0x50, 0x4E, 0x47], "image/png"),
        ("GIF8"u8.ToArray(), "image/gif"),
        ("BM"u8.ToArray(), "image/bmp"),
        ([0x49, 0x49, 0x2A, 0x00], "image/tiff"),
        ([0x4D, 0x4D, 0x00, 0x2A], "image/tiff"),
        ("RIFF"u8.ToArray(), "image/webp")
    ];
}