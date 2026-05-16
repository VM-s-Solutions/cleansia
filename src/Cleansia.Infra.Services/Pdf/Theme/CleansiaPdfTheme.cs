using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Cleansia.Infra.Services.Pdf.Theme;

public static class CleansiaPdfTheme
{
    public static readonly Color BrandPrimary = Color.FromHex("#0284c7");
    public static readonly Color BrandDark = Color.FromHex("#0369a1");
    public static readonly Color LightBlue = Color.FromHex("#eff6ff");
    public static readonly Color HeaderMetaBg = Color.FromHex("#0c4a6e");

    public static readonly Color PaidGreen = Color.FromHex("#16a34a");
    public static readonly Color PendingYellow = Color.FromHex("#ca8a04");
    public static readonly Color FailedRed = Color.FromHex("#dc2626");

    public static readonly Color TextPrimary = Color.FromHex("#1e293b");
    public static readonly Color TextSecondary = Color.FromHex("#64748b");
    public static readonly Color BorderLight = Color.FromHex("#e2e8f0");
    public static readonly Color TableHeaderBg = Color.FromHex("#f8fafc");
    public static readonly Color TableAltRowBg = Color.FromHex("#f1f5f9");
    public static readonly Color LegalNoticeBg = Color.FromHex("#fefce8");
    public static readonly Color LegalNoticeBorder = Color.FromHex("#ca8a04");
    public static readonly Color White = Colors.White;

    public const float FontSizeCompanyName = 24;
    public const float FontSizeTagline = 10;
    public const float FontSizeSectionTitle = 14;
    public const float FontSizeBody = 9;
    public const float FontSizeLabel = 8;
    public const float FontSizeSmall = 7;
    public const float FontSizeMetaValue = 10;

    public const float SectionSpacing = 15;
    public const float InnerPadding = 12;
    public const float SmallPadding = 6;

    public static Color GetStatusColor(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "paid" => PaidGreen,
            "pending" => PendingYellow,
            "failed" or "refunded" => FailedRed,
            _ => TextSecondary
        };
    }
}
