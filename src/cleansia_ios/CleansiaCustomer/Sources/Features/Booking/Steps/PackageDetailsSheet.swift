import CleansiaCore
import SwiftUI

/// Package review dialog (CatalogDetailsSheet.kt `PackageDetailsSheet`): name,
/// price, description and the included-services list, plus the primary
/// add/remove action. Unlike services, package selection happens through this
/// dialog rather than on the card itself.
struct PackageDetailsSheet: View {
    @Environment(\.locale) private var locale
    let pkg: CatalogPackage
    let isSelected: Bool
    let onToggle: () -> Void
    let onDismiss: () -> Void

    @State private var contentHeight: CGFloat = 320

    var body: some View {
        ScrollView {
            content
                .padding(Spacing.l)
                .frame(maxWidth: .infinity, alignment: .leading)
                .background(GeometryReader { proxy in
                    Color.clear.preference(key: PackageSheetHeightKey.self, value: proxy.size.height)
                })
        }
        .background(CleansiaColors.surface.ignoresSafeArea())
        .onPreferenceChange(PackageSheetHeightKey.self) { contentHeight = $0 }
        .presentationDetents([.height(contentHeight)])
        .presentationDragIndicator(.visible)
    }

    private var content: some View {
        VStack(alignment: .leading, spacing: Spacing.m) {
            VStack(alignment: .leading, spacing: Spacing.xxs) {
                Text(pkg.localizedName(for: locale))
                    .font(CleansiaTypography.headlineSmall)
                    .foregroundColor(CleansiaColors.onSurface)
                Text(BookingPricing.formatTotal(pkg.price, currencyCode: "CZK"))
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.primary)
            }

            if let description = pkg.localizedDescription(for: locale), !description.isEmpty {
                Text(description)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }

            if !pkg.includedServices.isEmpty {
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    Text(L10n.Booking.packageIncludes)
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    ForEach(Array(pkg.includedServices.enumerated()), id: \.offset) { _, service in
                        IncludedServiceRow(
                            name: CatalogLocalization.name(
                                translations: service.translations,
                                fallback: service.name,
                                languageCode: languageCode
                            )
                        )
                    }
                }
            }

            CleansiaPrimaryButton(
                isSelected ? L10n.Booking.removeFromBooking : L10n.Booking.addToBooking,
                action: {
                    onToggle()
                    onDismiss()
                }
            )
        }
    }

    private var languageCode: String {
        CatalogLocalization.languageCode(for: locale)
    }
}

private struct IncludedServiceRow: View {
    let name: String

    var body: some View {
        HStack(spacing: Spacing.xs) {
            Image(systemName: "checkmark")
                .font(.system(size: 15, weight: .semibold))
                .foregroundColor(CleansiaColors.primary)
            Text(name)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.vertical, Spacing.xxs)
    }
}

private struct PackageSheetHeightKey: PreferenceKey {
    static var defaultValue: CGFloat = 0
    static func reduce(value: inout CGFloat, nextValue: () -> CGFloat) {
        value = max(value, nextValue())
    }
}

#if DEBUG
    struct PackageDetailsSheet_Previews: PreviewProvider {
        static var previews: some View {
            PackageDetailsSheet(
                pkg: CatalogPackage(
                    id: "p-1",
                    name: "Move-out",
                    description: "A top-to-bottom clean for handing back the keys.",
                    price: 2500,
                    translations: [:],
                    includedServices: [
                        CatalogPackageServiceSummary(name: "Deep cleaning", translations: [:]),
                        CatalogPackageServiceSummary(name: "Windows", translations: [:])
                    ]
                ),
                isSelected: false,
                onToggle: {},
                onDismiss: {}
            )
        }
    }
#endif
