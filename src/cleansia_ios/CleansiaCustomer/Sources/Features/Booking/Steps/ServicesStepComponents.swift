import CleansiaCore
import SwiftUI

struct PropertyStepper: View {
    /// What VoiceOver calls the stepper; the visible label is read as its value.
    let name: String
    let label: String
    let value: Int
    /// Greys a button once its bound is reached, so a tap that cannot move the
    /// number never looks like one that can (Android's `Stepper`).
    var minimum: Int?
    var maximum: Int?
    let onChange: (Int) -> Void

    private var canDecrement: Bool {
        minimum.map { value > $0 } ?? true
    }

    private var canIncrement: Bool {
        maximum.map { value < $0 } ?? true
    }

    var body: some View {
        HStack(spacing: 0) {
            stepButton(systemImage: "minus", outward: .leading, enabled: canDecrement) { step(-1) }
            // Android CompactCounter parity: both buttons are measured first, and a label wider than
            // the space left wraps onto a second line (between words) rather than pushing the plus out
            // of the pill — two uk steppers ("3 кімнати", "2 ванні кімнати") overrun a 320 pt row.
            Text(label)
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(CleansiaColors.onSurface)
                .lineLimit(2)
                .multilineTextAlignment(.center)
                .fixedSize(horizontal: false, vertical: true)
                .contentTransition(.numericText())
                .animation(.default, value: value)
                // Room for both buttons' inward hit regions (12pt each) under a one-digit label.
                .frame(minWidth: 16)
                .padding(.horizontal, Spacing.xxs)
            stepButton(systemImage: "plus", outward: .trailing, enabled: canIncrement) { step(+1) }
        }
        .background(CleansiaColors.surface)
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.pill))
        // One adjustable element (swipe up / down), not two bare glyph buttons around an unnamed number.
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(name)
        .accessibilityValue(label)
        .accessibilityAdjustableAction { direction in
            switch direction {
            case .increment: if canIncrement { step(+1) }
            case .decrement: if canDecrement { step(-1) }
            @unknown default: break
            }
        }
    }

    /// Each accepted tick is felt (ADR-0018 D2); the bounded buttons are disabled, so a refused one never fires.
    private func step(_ delta: Int) {
        UISelectionFeedbackGenerator().selectionChanged()
        onChange(value + delta)
    }

    /// The glyph stays 28pt (Android `CompactCounter`); the hit region is HIG's 44pt without growing the
    /// pill: 8pt more above and below, 12pt toward the label and 4pt outward, so two steppers sharing a
    /// row split the gap between them.
    private func stepButton(
        systemImage: String,
        outward: HorizontalEdge,
        enabled: Bool,
        action: @escaping () -> Void
    ) -> some View {
        Button(action: action) {
            Image(systemName: systemImage)
                .font(.system(size: 12, weight: .bold))
                .foregroundColor(enabled ? CleansiaColors.primary : CleansiaColors.onSurfaceVariant.opacity(0.4))
                .frame(width: 28, height: 28)
                .contentShape(Rectangle().inset(by: -8).offset(x: outward == .leading ? 4 : -4))
        }
        .buttonStyle(.plain)
        .disabled(!enabled)
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
    @Environment(\.locale) private var locale
    let service: CatalogService
    let currencyCode: String
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
            Text(service.localizedName(for: locale))
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .lineLimit(1)
            if let description = service.localizedDescription(for: locale), !description.isEmpty {
                Text(description)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .lineLimit(2)
            }
            HStack(spacing: Spacing.xxs) {
                Text(L10n.Booking.priceFrom(price(service.basePrice)))
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(CleansiaColors.primary)
                if service.perRoomPrice > 0 {
                    Text(L10n.Booking.pricePerRoom(price(service.perRoomPrice)))
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private func price(_ amount: Double) -> String {
        BookingPricing.formatTotal(amount, currencyCode: currencyCode)
    }
}

/// Package cards cycle the three brand gradients by index (ServicesStep.kt
/// `accentForIndex`), so a shelf of packages reads as layered brand tiers
/// rather than one flat blue.
enum PackageAccent {
    static func gradient(for index: Int) -> BrandGradient {
        switch index % 3 {
        case 0: .blue
        case 1: .purple
        default: .cyan
        }
    }
}

struct PackageCard: View {
    @Environment(\.locale) private var locale
    let pkg: CatalogPackage
    let currencyCode: String
    let accent: BrandGradient
    let selected: Bool
    let onOpen: () -> Void

    var body: some View {
        Button(action: onOpen) {
            VStack(alignment: .leading, spacing: Spacing.xxs) {
                HStack {
                    Text(pkg.localizedName(for: locale))
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(.white)
                        .lineLimit(1)
                    Spacer()
                    if selected {
                        checkBadge
                    }
                }
                if let description = pkg.localizedDescription(for: locale), !description.isEmpty {
                    Text(description)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(.white.opacity(0.9))
                        .lineLimit(1)
                }
                if let summary = pkg.includesSummary(for: locale) {
                    Text(summary)
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(.white.opacity(0.85))
                        .lineLimit(1)
                }
                Spacer(minLength: Spacing.xxs)
                Text(BookingPricing.formatTotal(pkg.price, currencyCode: currencyCode))
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(.white)
            }
            .padding(Spacing.s)
            .frame(width: 240, height: 150, alignment: .topLeading)
            .background(accent.linearGradient)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
        }
        .buttonStyle(.plain)
    }

    private var checkBadge: some View {
        Image(systemName: "checkmark")
            .font(.system(size: 12, weight: .bold))
            .foregroundColor(accent.colors.first ?? CleansiaColors.primary)
            .frame(width: 22, height: 22)
            .background(Color.white)
            .clipShape(Circle())
    }
}

struct SelectionBadge: View {
    let selected: Bool

    var body: some View {
        if selected {
            Image(systemName: "checkmark")
                .font(.system(size: 12, weight: .bold))
                .foregroundColor(CleansiaColors.onPrimary)
                .frame(width: 22, height: 22)
                .background(CleansiaColors.primary)
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
            .cleansiaFont(CleansiaTypography.headlineSmall)
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
