import CleansiaCore
import SwiftUI

/// The Orders board's calendar leaf, one per day: month and day, and on a job row the weekday, a square at every
/// text size. Today is the tint with the selected stroke, an upcoming day the tint, and a closed job (completed or
/// cancelled, whatever its day) grey. In the app locale and the device's zone; hidden from VoiceOver, since every row
/// and header speaks its date.
struct OrdersDateTile: View {
    enum Size {
        /// A day panel's header and the reserved card: month and day, 48 square.
        case header
        /// A job row on Active, History and the next jobs: month, day and weekday, 64 square.
        case row
        /// The Job taken panel: month and day, 64 square.
        case ticket
    }

    enum Emphasis {
        case plain
        case today
        case tomorrow
    }

    @Environment(\.locale) private var locale
    let date: Date
    let size: Size
    var now = Date()
    var closed = false

    var body: some View {
        SquareLeaf(least: size == .header ? 48 : 64) {
            VStack(spacing: 0) {
                Text(Self.month(date, locale: locale))
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(closed ? CleansiaColors.onSurfaceVariant : CleansiaColors.primaryText)
                Text(Self.day(date, locale: locale))
                    .font(CleansiaTypography.titleMedium)
                    .monospacedDigit()
                    .foregroundColor(closed ? CleansiaColors.onSurfaceVariant : CleansiaColors.onSurface)
                if size == .row {
                    Text(Self.weekday(date, locale: locale))
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
            .lineLimit(1)
            .fixedSize()
            .offset(y: size == .header ? 1 : 0.5)
        }
        .background(
            closed ? CleansiaColors.surfaceVariant : CleansiaColors.primary.opacity(0.12),
            in: RoundedRectangle(cornerRadius: CornerRadius.small)
        )
        .overlay {
            if isToday {
                RoundedRectangle(cornerRadius: CornerRadius.small).strokeBorder(CleansiaColors.primary, lineWidth: 1.5)
            }
        }
        .dynamicTypeSize(...DynamicTypeSize.xxxLarge)
        .accessibilityHidden(true)
    }

    private var isToday: Bool {
        !closed && Calendar.current.isDate(date, inSameDayAs: now)
    }

    // MARK: - Text

    /// The standalone abbreviated month as written ("LLL"): "Nov", "lis", never the genitive a day number would take.
    static func month(_ date: Date, locale: Locale) -> String {
        OrdersFormat.pattern("LLL", date, locale: locale)
    }

    static func day(_ date: Date, locale: Locale) -> String {
        OrdersFormat.pattern("d", date, locale: locale)
    }

    static func weekday(_ date: Date, locale: Locale) -> String {
        OrdersFormat.pattern("EEE", date, locale: locale)
    }

    static func emphasis(for date: Date, now: Date, calendar: Calendar = .current) -> Emphasis {
        if calendar.isDate(date, inSameDayAs: now) { return .today }
        guard let tomorrow = calendar.date(byAdding: .day, value: 1, to: now) else { return .plain }
        return calendar.isDate(date, inSameDayAs: tomorrow) ? .tomorrow : .plain
    }
}

/// One subview centred in a square: the size's least side, or more when its text needs it, keeping at least 3 above
/// and under the stack and 6 beside its widest line, so both axes grow together.
struct SquareLeaf: Layout {
    let least: CGFloat

    static func side(least: CGFloat, stack: CGSize) -> CGFloat {
        max(least, stack.height + 2 * 3, stack.width + 2 * 6).rounded(.up)
    }

    func sizeThatFits(proposal _: ProposedViewSize, subviews: Subviews, cache _: inout ()) -> CGSize {
        let stack = subviews.first?.sizeThatFits(.unspecified) ?? .zero
        let side = Self.side(least: least, stack: stack)
        return CGSize(width: side, height: side)
    }

    func placeSubviews(in bounds: CGRect, proposal _: ProposedViewSize, subviews: Subviews, cache _: inout ()) {
        subviews.first?.place(at: CGPoint(x: bounds.midX, y: bounds.midY), anchor: .center, proposal: .unspecified)
    }
}
