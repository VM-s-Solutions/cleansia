namespace Cleansia.Core.AppServices.Common;

public class Constants
{
    public class BlobContainers
    {
        public const string UserFiles = "user-files";
        public const string BetaWhiteList = "beta-whitelist";
        public const string EmployeeDocuments = "employee-documents";
        public const string InvoiceTemplates = "invoice-templates";
        public const string GeneratedInvoices = "generated-invoices";
        public const string ReceiptTemplates = "receipt-templates";
        public const string GeneratedReceipts = "generated-receipts";
        public const string OrderPhotos = "order-photos";
    }

    public class VirtualDirectories
    {
        public const string EmployeeDocuments = "employees/{0}/documents";
    }

    public class StripeEventType
    {
        public const string CompletedSession = "checkout.session.completed";
    }

    public class Language
    {
        public const string English = "en";
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