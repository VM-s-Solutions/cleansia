namespace Cleansia.Core.AppServices.Common;

public class Constants
{
    public class BlobContainers
    {
        public const string UserFiles = "user-files";
        public const string BetaWhiteList = "beta-whitelist";
    }

    public class StripeEventType
    {
        public const string CompletedSession = "checkout.session.completed";
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