import CleansiaCore
import SwiftUI

struct PropertyStepper: View {
    let label: String
    let value: Int
    let onChange: (Int) -> Void

    var body: some View {
        HStack(spacing: 0) {
            stepButton(systemImage: "minus") { onChange(value - 1) }
            Text(label)
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(CleansiaColors.onSurface)
                .padding(.horizontal, Spacing.xxs)
            stepButton(systemImage: "plus") { onChange(value + 1) }
        }
        .background(CleansiaColors.surface)
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.pill))
    }

    private func stepButton(systemImage: String, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Image(systemName: systemImage)
                .font(.system(size: 12, weight: .bold))
                .foregroundColor(CleansiaColors.primary)
                .frame(width: 28, height: 28)
        }
        .buttonStyle(.plain)
    }
}

struct CategoryChip: View {
    let label: String
    let systemImage: String
    let tint: Color
    let selected: Bool
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            HStack(spacing: Spacing.xxs) {
                Image(systemName: systemImage)
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundColor(selected ? .white : tint)
                Text(label)
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(selected ? .white : CleansiaColors.onSurface)
            }
            .padding(.horizontal, Spacing.s)
            .padding(.vertical, Spacing.xs)
            .background(selected ? tint : CleansiaColors.surface)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.pill))
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.pill)
                    .stroke(selected ? tint : CleansiaColors.outlineVariant, lineWidth: 1)
            )
        }
        .buttonStyle(.plain)
    }
}

struct ServiceRow: View {
    let service: CatalogService
    let selected: Bool
    let onToggle: () -> Void

    var body: some View {
        Button(action: onToggle) {
            HStack(alignment: .top, spacing: Spacing.s) {
                let tint = CategoryPalette.tint(for: service.category.slug)
                Image(systemName: CategoryPalette.symbol(for: service.category.slug))
                    .font(.system(size: 20))
                    .foregroundColor(tint)
                    .frame(width: 44, height: 44)
                    .background(tint.opacity(0.12))
                    .clipShape(RoundedRectangle(cornerRadius: CornerRadius.small))

                details

                SelectionBadge(selected: selected)
            }
            .padding(Spacing.s)
            .background(selected ? CleansiaColors.primaryContainer.opacity(0.5) : CleansiaColors.surface)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.medium)
                    .stroke(
                        selected ? CleansiaColors.primary : CleansiaColors.outlineVariant,
                        lineWidth: selected ? 2 : 1
                    )
            )
        }
        .buttonStyle(.plain)
    }

    private var details: some View {
        VStack(alignment: .leading, spacing: Spacing.xxs) {
            Text(service.localizedName)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .lineLimit(1)
            if let description = service.localizedDescription, !description.isEmpty {
                Text(description)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .lineLimit(2)
            }
            HStack(spacing: Spacing.xxs) {
                Text(L10n.Booking.priceFrom(Int(service.basePrice)))
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(CleansiaColors.primary)
                if service.perRoomPrice > 0 {
                    Text(L10n.Booking.pricePerRoom(Int(service.perRoomPrice)))
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

struct PackageCard: View {
    let pkg: CatalogPackage
    let selected: Bool
    let onToggle: () -> Void

    var body: some View {
        Button(action: onToggle) {
            VStack(alignment: .leading, spacing: Spacing.xxs) {
                HStack {
                    Text(pkg.localizedName)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onPrimary)
                        .lineLimit(1)
                    Spacer()
                    if selected {
                        SelectionBadge(selected: true, onPrimary: true)
                    }
                }
                if let description = pkg.localizedDescription, !description.isEmpty {
                    Text(description)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onPrimary.opacity(0.9))
                        .lineLimit(1)
                }
                if let summary = pkg.includesSummary {
                    Text(summary)
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.onPrimary.opacity(0.85))
                        .lineLimit(1)
                }
                Spacer(minLength: Spacing.xxs)
                Text(BookingPricing.formatTotal(pkg.price, currencyCode: "CZK"))
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onPrimary)
            }
            .padding(Spacing.s)
            .frame(width: 240, height: 150, alignment: .topLeading)
            .background(CleansiaColors.primary)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
        }
        .buttonStyle(.plain)
    }
}

struct SelectionBadge: View {
    let selected: Bool
    var onPrimary = false

    var body: some View {
        if selected {
            Image(systemName: "checkmark")
                .font(.system(size: 12, weight: .bold))
                .foregroundColor(onPrimary ? CleansiaColors.primary : CleansiaColors.onPrimary)
                .frame(width: 22, height: 22)
                .background(onPrimary ? CleansiaColors.surface : CleansiaColors.primary)
                .clipShape(Circle())
        }
    }
}

struct SectionHeader: View {
    let text: String

    init(_ text: String) {
        self.text = text
    }

    var body: some View {
        Text(text)
            .font(CleansiaTypography.headlineSmall)
            .foregroundColor(CleansiaColors.onBackground)
    }
}

struct EmptyResults: View {
    var body: some View {
        VStack(spacing: Spacing.xs) {
            Image(systemName: "magnifyingglass")
                .font(.system(size: 40))
                .foregroundColor(CleansiaColors.outlineVariant)
            Text(L10n.Booking.noResults)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
        .frame(maxWidth: .infinity)
        .padding(Spacing.xxl)
    }
}

struct CatalogMessageView: View {
    let systemImage: String
    let message: String
    var showsSpinner = false
    var retryTitle: String?
    var onRetry: (() -> Void)?

    var body: some View {
        VStack(spacing: Spacing.s) {
            if showsSpinner {
                ProgressView()
                    .tint(CleansiaColors.primary)
            } else {
                Image(systemName: systemImage)
                    .font(.system(size: 44))
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            Text(message)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
            if let retryTitle, let onRetry {
                Button(action: onRetry) {
                    Text(retryTitle)
                        .font(CleansiaTypography.labelLarge)
                        .foregroundColor(CleansiaColors.primary)
                        .padding(.horizontal, Spacing.m)
                        .padding(.vertical, Spacing.xs)
                }
                .buttonStyle(.plain)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(Spacing.xxl)
    }
}

/// Slug-keyed palette mirroring Android ServicesStep.kt: SF Symbols map the
/// Material icons' meaning (no SF broom exists, so home→bubbles.and.sparkles
/// and deep→leaf stand in for CleaningServices/Spa), tints are the Android
/// per-category hexes.
enum CategoryPalette {
    static let defaultTint = Color(red: 2 / 255, green: 132 / 255, blue: 199 / 255)

    static func symbol(for slug: String) -> String {
        switch slug {
        case "home": "bubbles.and.sparkles"
        case "deep": "leaf"
        case "laundry": "washer"
        case "pet": "pawprint"
        default: "star"
        }
    }

    static func tint(for slug: String) -> Color {
        switch slug {
        case "deep": Color(red: 124 / 255, green: 58 / 255, blue: 237 / 255)
        case "laundry": Color(red: 8 / 255, green: 145 / 255, blue: 178 / 255)
        case "pet": Color(red: 234 / 255, green: 88 / 255, blue: 12 / 255)
        default: defaultTint
        }
    }
}
