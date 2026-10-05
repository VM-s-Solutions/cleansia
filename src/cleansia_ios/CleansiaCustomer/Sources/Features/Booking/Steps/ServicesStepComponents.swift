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
    /// Takes the width and height it is offered, the label centred between the buttons — the booking's
    /// size row splits its width between two of these. Off, the pill is as wide as its label.
    var fills = false
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
                .frame(minWidth: 16, maxWidth: fills ? .infinity : nil)
                .padding(.horizontal, Spacing.xxs)
            stepButton(systemImage: "plus", outward: .trailing, enabled: canIncrement) { step(+1) }
        }
        .frame(maxWidth: fills ? .infinity : nil, maxHeight: fills ? .infinity : nil)
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

/// A home-size title with the size caption ("Up to 8 rooms and 4 bathrooms") at its trailing end, on
/// its baseline, so the caps are stated beside the steppers' heading rather than under them (owner remark
/// 2026-10-04). Where the two do not fit on one line — a 320pt phone, Ukrainian or Russian, large text —
/// the caption drops to its own line under the title, leading-aligned; never below the steppers.
/// VoiceOver reads the title, then the caption. The one-off booking's size card and the recurring form's
/// size section both use it, so the fit rule is the same in both.
struct SizeLimitTitleRow<Title: View>: View {
    private let title: Title

    init(@ViewBuilder title: () -> Title) {
        self.title = title()
    }

    var body: some View {
        ViewThatFits(in: .horizontal) {
            HStack(alignment: .firstTextBaseline, spacing: Spacing.s) {
                title
                Spacer(minLength: 0)
                caption
            }
            VStack(alignment: .leading, spacing: Spacing.hair) {
                title
                caption
            }
        }
    }

    private var caption: some View {
        Text(L10n.Booking.sizeLimitCaption)
            .font(CleansiaTypography.labelSmall)
            .foregroundColor(CleansiaColors.onSurfaceVariant)
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
    @Environment(\.colorScheme) private var colorScheme
    let service: CatalogService
    let currencyCode: String
    let selected: Bool
    /// "In your package: …" when a selected package already includes this service; part of the row's
    /// VoiceOver label like every other line on it. The row stays selectable, and reads as covered
    /// until it is picked, when the selected look (already a primary tint) takes over.
    let inPackageNote: String?
    let onToggle: () -> Void

    private var covered: Bool {
        inPackageNote != nil && !selected
    }

    /// The "from" price's ink. The primary is sky-600 in light mode, 4.1:1 on the plain card and 3.7:1 on
    /// a covered or picked row, so light mode takes sky-700 (5.9:1 / 5.4:1); the primary stays the brand
    /// colour of the row's fills and borders. Dark mode keeps it (sky-400, 5.0:1 at worst).
    static func fromPriceInk(_ scheme: ColorScheme) -> Color {
        scheme == .dark ? CleansiaColors.primary : CleansiaColors.primaryText
    }

    /// The secondary text's ink. On a covered or picked (`tinted`) row in dark mode slate-400 measures
    /// 4.2:1, so those rows take slate-300 (7.2:1); the plain row (5.7:1) and light mode (slate-700, 9.4:1
    /// at worst) keep the theme's.
    static func secondaryInk(_ scheme: ColorScheme, tinted: Bool) -> Color {
        scheme == .dark && tinted ? slate300 : CleansiaColors.onSurfaceVariant
    }

    private static let slate300 = Color(red: 203 / 255, green: 213 / 255, blue: 225 / 255)

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
            .background(covered ? InPackageStyle.rowTint(colorScheme) : .clear)
            .background(selected ? CleansiaColors.primaryContainer.opacity(0.5) : CleansiaColors.surface)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.medium)
                    .stroke(border, lineWidth: selected ? 2 : covered ? InPackageStyle.borderWidth : 1)
            )
        }
        .buttonStyle(.plain)
    }

    private var border: Color {
        if selected { return CleansiaColors.primary }
        return covered ? InPackageStyle.border : CleansiaColors.outlineVariant
    }

    private var details: some View {
        VStack(alignment: .leading, spacing: Spacing.xxs) {
            Text(service.localizedName(for: locale))
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .lineLimit(1)
            if let inPackageNote {
                InPackageNote(text: inPackageNote)
            }
            if let description = service.localizedDescription(for: locale), !description.isEmpty {
                Text(description)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(Self.secondaryInk(colorScheme, tinted: selected || covered))
                    .lineLimit(2)
            }
            HStack(spacing: Spacing.xxs) {
                Text(L10n.Booking.priceFrom(price(service.basePrice)))
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(Self.fromPriceInk(colorScheme))
                if service.perRoomPrice > 0 {
                    Text(L10n.Booking.pricePerRoom(price(service.perRoomPrice)))
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(Self.secondaryInk(colorScheme, tinted: selected || covered))
                }
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private func price(_ amount: Double) -> String {
        BookingPricing.formatTotal(amount, currencyCode: currencyCode)
    }
}

/// How a service row reads while a selected package already includes it, so the customer sees at a
/// glance that it is booked already: a primary tint replaces the row's neutral card (a picked row keeps
/// its selected fill, already a primary tint), a primary border replaces the neutral one (the selected
/// border wins), and the note is a badge. The booking's services list and the recurring form's both draw
/// it; iOS is the reference the Android and web rows follow.
enum InPackageStyle {
    static let borderWidth: CGFloat = 1.5
    static let border = CleansiaColors.primary.opacity(0.6)
    /// The badge's ink, text and icon. The primary itself measures about 3.1:1 on the badge in both
    /// schemes (sky-600 is 4.1:1 even on white), so the badge takes the primary family's ink for primary
    /// tints, sky-900 / sky-100: about 7.2:1 light and 6.1:1 dark, 5.5:1 at worst on a picked row.
    static let ink = CleansiaColors.onPrimaryContainer

    static func rowTintOpacity(_ scheme: ColorScheme) -> Double {
        scheme == .dark ? 0.16 : 0.08
    }

    static func badgeOpacity(_ scheme: ColorScheme) -> Double {
        scheme == .dark ? 0.24 : 0.14
    }

    static func rowTint(_ scheme: ColorScheme) -> Color {
        CleansiaColors.primary.opacity(rowTintOpacity(scheme))
    }
}

/// The badge a service row carries while a selected package already includes it — "In your package: …"
/// after a check, on a primary tint.
struct InPackageNote: View {
    @Environment(\.colorScheme) private var colorScheme
    let text: String

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: Spacing.xxs) {
            Image(systemName: "checkmark.circle.fill")
                .font(.system(size: 13, weight: .semibold))
                .foregroundColor(InPackageStyle.ink)
                .accessibilityHidden(true)
            Text(text)
                .cleansiaFont(.nunito(.semibold, size: 14))
                .foregroundColor(InPackageStyle.ink)
                .lineLimit(2)
        }
        .padding(.horizontal, Spacing.xs)
        .padding(.vertical, Spacing.xxs)
        .background(
            CleansiaColors.primary.opacity(InPackageStyle.badgeOpacity(colorScheme)),
            in: RoundedRectangle(cornerRadius: CornerRadius.small)
        )
    }
}

/// The twice-booking confirm (→ /product/business-rules#charging-a-package-and-a-service-together):
/// the pick lands only on the confirm; Cancel, the escape, leaves the selection as it was.
struct TwiceBookedAlert: ViewModifier {
    @Environment(\.locale) private var locale
    let pick: TwiceBookedPick?
    let catalog: Catalog?
    let onConfirm: (TwiceBookedPick) -> Void
    let onCancel: () -> Void

    func body(content: Content) -> some View {
        content.alert(
            pick?.title ?? "",
            isPresented: Binding(get: { pick != nil }, set: { if !$0 { onCancel() } }),
            presenting: pick
        ) { pick in
            Button(confirmLabel(pick)) { onConfirm(pick) }
            Button(L10n.cancel, role: .cancel, action: onCancel)
        } message: { pick in
            Text(catalog?.twiceBookedMessage(pick, locale: locale) ?? "")
        }
    }

    private func confirmLabel(_ pick: TwiceBookedPick) -> String {
        switch pick {
        case .service: L10n.Booking.twiceServiceConfirm
        case .package: L10n.Booking.twicePackageConfirm
        }
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
