import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

extension BookingSuccessView {
    enum SizeClass {
        case compact
        case regular
        case tall
        case accessibility
    }

    struct Tiers: Equatable {
        let pMin: CGFloat
        let pNat: CGFloat
        let pMax: CGFloat
        let pOver: CGFloat
        let hdMin: CGFloat
        let hdCap: CGFloat
    }

    static let leafSide: CGFloat = 64

    static func sizeClass(room: CGFloat, accessibilityText: Bool) -> SizeClass {
        if accessibilityText { return .accessibility }
        if room < 560 { return .compact }
        return room < 700 ? .regular : .tall
    }

    // swiftlint:disable large_tuple
    /// Spare height is spent tier by tier, so it never collects into one band: the rows to their natural
    /// padding, the header to its cap, the rows to their cap and past it, and only then a rest. A row's
    /// padding is on both sides of four rows, so a padding tier is shared over eight sides.
    static func distribute(
        available: CGFloat,
        natural: CGFloat,
        tiers: Tiers
    ) -> (header: CGFloat, rowPad: CGFloat, rest: CGFloat) {
        let sides: CGFloat = 8
        var slack = max(0, available - natural)
        func spend(_ room: CGFloat) -> CGFloat {
            let spent = min(slack, max(0, room))
            slack -= spent
            return spent
        }
        var rowPad = tiers.pMin + spend((tiers.pNat - tiers.pMin) * sides) / sides
        let header = tiers.hdMin + spend(tiers.hdCap - tiers.hdMin)
        rowPad += spend((tiers.pMax - tiers.pNat) * sides) / sides
        rowPad += spend((tiers.pOver - tiers.pMax) * sides) / sides
        return (header, rowPad, slack)
    }

    // swiftlint:enable large_tuple

    static func mascotCanvas(figureHeight: CGFloat) -> CGRect {
        let scale = figureHeight / 249
        return CGRect(x: -88 * scale, y: -51 * scale, width: 360 * scale, height: 360 * scale)
    }

    private static let actionsHeight = Spacing.s + 56 + Spacing.xs

    /// The hero's figure height before any confirmation is laid out in a sheet this tall: the tier of the room its
    /// grab zone and actions leave, at its minimum on a compact phone, which only the ticket's slack can raise, and
    /// at its cap on every other.
    static func expectedFigureHeight(sheetHeight: CGFloat, accessibilityText: Bool) -> CGFloat {
        let size = sizeClass(room: sheetHeight - grabZone - actionsHeight, accessibilityText: accessibilityText)
        return size == .regular || size == .tall ? size.tiers.hdCap : size.tiers.hdMin
    }

    /// The cleaner on the busy card stands as tall as the hero it flies to: the hero's own height once the
    /// confirmation is laid out under the card, until then the draft's, and before either the height its tiers give.
    static func busyFigureHeight(
        hero: CGRect?,
        draft: CGRect?,
        sheetHeight: CGFloat,
        accessibilityText: Bool
    ) -> CGFloat {
        (hero ?? draft).map { MascotFigureBox.welcoming.figureHeight(canvasSide: $0.height) }
            ?? expectedFigureHeight(sheetHeight: sheetHeight, accessibilityText: accessibilityText)
    }

    /// The confirmation `booking` will arrive as, laid out unseen, reporting only its hero. Unseen is zero opacity:
    /// under `hidden()` its header never settled.
    static func unseen(_ booking: BookingSuccess, onHeroMascotFrame: @escaping (CGRect) -> Void) -> some View {
        BookingSuccessView(
            booking,
            loadOrder: { _ in nil },
            revealed: false,
            onViewOrder: {},
            onDone: {},
            onHeroMascotFrame: onHeroMascotFrame,
            loadsDetails: false
        )
        .opacity(0)
        .accessibilityHidden(true)
        .allowsHitTesting(false)
    }

    static func timeWindow(_ date: Date, estimatedMinutes: Int, locale: Locale) -> String {
        let time = Date.FormatStyle.dateTime.hour().minute().locale(locale)
        let start = date.formatted(time)
        guard estimatedMinutes > 0 else { return start }
        let end = date.addingTimeInterval(Double(estimatedMinutes) * 60)
        let endFormat = Calendar.current.isDate(date, inSameDayAs: end) ? time : time.day().month(.abbreviated)
        return start + "–" + end.formatted(endFormat)
    }

    static func dayLine(_ date: Date, locale: Locale) -> String {
        bindLastWord(date.formatted(.dateTime.weekday(.wide).month(.wide).day().locale(locale)))
    }

    /// The free-cancellation deadline the order states, or nil when the sheet says the always-true rule
    /// instead: no hours on the order, or a deadline already behind.
    static func freeCancellationDeadline(cleaningAt: Date?, freeHours: Int?, now: Date, locale: Locale) -> String? {
        guard let cleaningAt, let freeHours else { return nil }
        let deadline = cleaningAt.addingTimeInterval(-Double(freeHours) * 3600)
        guard deadline > now else { return nil }
        let style = Date.FormatStyle.dateTime.weekday(.abbreviated).month(.abbreviated).day().hour().minute()
        return unbreakable(deadline.formatted(style.locale(locale)))
    }

    /// Before the order lands, a slot at least the standard window ahead has its deadline ahead on every
    /// plan, since a Plus window is shorter.
    static func deadlineAheadOnEveryPlan(cleaningAt: Date?, now: Date) -> Bool {
        guard let cleaningAt else { return false }
        return cleaningAt.timeIntervalSince(now) >= Double(CancellationPolicyBuilder.standardFreeHours) * 3600
    }

    static let actionPadding: CGFloat = 20

    /// The frames' `flex: 1 1 auto` at 1 : 1.4: both buttons start at their label and `actionPadding` a side,
    /// and Back to home takes 1 of every 2.4 points left over. Nil until measured, or when the labels do not fit.
    static func homeActionWidth(room: CGFloat, homeLabel: CGFloat, viewLabel: CGFloat) -> CGFloat? {
        let basis = 2 * actionPadding
        let left = room - Spacing.s - homeLabel - viewLabel - 2 * basis
        guard homeLabel > 0, viewLabel > 0, left >= 0 else { return nil }
        return (homeLabel + basis + left / 2.4).rounded(.down)
    }

    static func remedy(_ booking: BookingSuccess) -> String {
        booking.paysCash ? L10n.Booking.successNoCleanerCash : L10n.Booking.successNoCleanerCard
    }

    static func findingSpoken(_ booking: BookingSuccess) -> String {
        [
            L10n.Booking.successFinding,
            L10n.Booking.successCrewNeeded(booking.requiredEmployees),
            L10n.Booking.successFindingSub,
            remedy(booking)
        ].joined(separator: " ")
    }

    static func homeLine(_ booking: BookingSuccess) -> String {
        let size = L10n.OrderDetail.roomsBathrooms(booking.rooms, booking.bathrooms)
        guard booking.dirtiness != .normal else { return size }
        return size + " · " + L10n.Booking.dirtinessLevelLabel + ": " + L10n.Booking.dirtinessName(booking.dirtiness)
    }

    static func addressLine(street: String?, city: String?) -> String? {
        let street = street?.trimmingCharacters(in: .whitespaces) ?? ""
        let city = city?.trimmingCharacters(in: .whitespaces) ?? ""
        let parts = [bindLastWord(street), unbreakable(city)].filter { !$0.isEmpty }
        return parts.isEmpty ? nil : parts.joined(separator: ", ")
    }

    /// The order's own figures only: the confirm step's preview is not what was paid. Total leads every
    /// variant; an empty value is a figure still loading, and the note is the line under the figures.
    static func paymentRows(
        _ state: BookingSuccessUiState,
        booking: BookingSuccess
    ) -> (rows: [SummaryRow], note: String?) {
        let total = L10n.Booking.successTotalLabel
        switch state {
        case let .loaded(order):
            let price = { OrdersFormat.price($0, currencyCode: order.currencyCode) }
            if booking.paysCash {
                return ([SummaryRow(label: total, value: price(order.total - order.creditAppliedAmount))], cashLine)
            }
            guard order.creditAppliedAmount > 0 else {
                return ([SummaryRow(label: total, value: price(order.total))], L10n.Credit.paidByCard)
            }
            return ([
                SummaryRow(label: total, value: price(order.total)),
                SummaryRow(label: L10n.Credit.paidWithCredit, value: "−" + price(order.creditAppliedAmount)),
                SummaryRow(label: L10n.Credit.paidByCard, value: price(order.amountDueOnCard))
            ], nil)
        case .loading:
            if booking.paysCash { return ([SummaryRow(label: total, value: "")], cashLine) }
            guard booking.appliesCredit else { return ([SummaryRow(label: total, value: "")], L10n.Credit.paidByCard) }
            return ([
                SummaryRow(label: total, value: ""),
                SummaryRow(label: L10n.Credit.paidWithCredit, value: ""),
                SummaryRow(label: L10n.Credit.paidByCard, value: "")
            ], nil)
        case .error:
            return ([SummaryRow(label: total, value: "—")], L10n.Booking.successDetailsUnavailable)
        }
    }

    private static var cashLine: String {
        L10n.Booking.successCashOnDay
    }

    private static func bindLastWord(_ text: String) -> String {
        guard let space = text.lastIndex(of: " ") else { return text }
        return text.replacingCharacters(in: space ... space, with: "\u{00A0}")
    }

    private static func unbreakable(_ text: String) -> String {
        text.replacingOccurrences(of: " ", with: "\u{00A0}")
    }
}

extension BookingSuccessView {
    @ViewBuilder
    func answerRow(_ size: SizeClass, date: Date?, minutes: Int, address: String?, locale: Locale) -> some View {
        if let date {
            if size == .accessibility {
                VStack(alignment: .leading, spacing: 6) {
                    HStack(alignment: .center, spacing: Spacing.m) {
                        leaf(date, locale: locale)
                        dayText(date, locale: locale)
                    }
                    windowText(date, minutes: minutes, size: size, locale: locale).padding(.top, Spacing.xs)
                    addressText(address)
                }
                .accessibilityElement(children: .combine)
            } else {
                HStack(alignment: .center, spacing: 14) {
                    leaf(date, locale: locale)
                    VStack(alignment: .leading, spacing: 0) {
                        dayText(date, locale: locale)
                        windowText(date, minutes: minutes, size: size, locale: locale).padding(.vertical, 2)
                        addressText(address)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .accessibilityElement(children: .combine)
                }
            }
        } else {
            addressText(address)
        }
    }

    func leaf(_ date: Date, locale: Locale) -> some View {
        VStack(spacing: Spacing.xxs) {
            Text(date.formatted(.dateTime.month(.abbreviated).locale(locale)).uppercased(with: locale))
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.primaryTextOnContainer)
            Text(date.formatted(.dateTime.day().locale(locale)))
                .font(CleansiaTypography.titleMedium)
                .monospacedDigit()
                .foregroundColor(CleansiaColors.onPrimaryContainer)
        }
        .lineLimit(1)
        .minimumScaleFactor(0.7)
        .dynamicTypeSize(...DynamicTypeSize.xxxLarge)
        .frame(width: Self.leafSide, height: Self.leafSide)
        .background(CleansiaColors.primaryContainer, in: RoundedRectangle(cornerRadius: CornerRadius.small))
        .accessibilityHidden(true)
    }

    func dayText(_ date: Date, locale: Locale) -> some View {
        Text(Self.dayLine(date, locale: locale))
            .font(CleansiaTypography.bodyLarge)
            .foregroundColor(CleansiaColors.onSurface)
            .fixedSize(horizontal: false, vertical: true)
    }

    func windowText(_ date: Date, minutes: Int, size: SizeClass, locale: Locale) -> some View {
        Text(Self.timeWindow(date, estimatedMinutes: minutes, locale: locale))
            .cleansiaFont(CleansiaTypography.headlineMedium)
            .monospacedDigit()
            .foregroundColor(CleansiaColors.onSurface)
            .lineLimit(size == .accessibility ? nil : 1)
            .minimumScaleFactor(0.85)
            .dynamicTypeSize(...DynamicTypeSize.xxxLarge)
    }

    @ViewBuilder
    func addressText(_ address: String?) -> some View {
        if let address {
            Text(address)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .fixedSize(horizontal: false, vertical: true)
        }
    }

    @ViewBuilder
    func bounceOnlyWhenScrolling(_ scroll: some View) -> some View {
        if #available(iOS 16.4, *) {
            scroll.scrollBounceBehavior(.basedOnSize)
        } else {
            scroll
        }
    }

    func freeUntil(_ date: String) -> some View {
        Text(L10n.Booking.successFreeUntil(date))
            .font(CleansiaTypography.bodyMedium)
            .foregroundColor(CleansiaColors.onSurface)
            .fixedSize(horizontal: false, vertical: true)
    }

    func freeWhile(_ font: Font) -> some View {
        Text(L10n.Booking.successFreeWhile)
            .font(font)
            .foregroundColor(CleansiaColors.onSurface)
            .fixedSize(horizontal: false, vertical: true)
    }

    var bookedRow: some View {
        VStack(alignment: .leading, spacing: 0) {
            if !booking.basketNames.isEmpty {
                Text(booking.basketNames.joined(separator: " · "))
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
            }
            Text(Self.homeLine(booking))
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            if !booking.extraNames.isEmpty {
                ChipFlow(spacing: 6) {
                    ForEach(booking.extraNames, id: \.self) { chip($0) }
                }
                .padding(.top, Spacing.xs)
            }
        }
        .fixedSize(horizontal: false, vertical: true)
        .frame(maxWidth: .infinity, alignment: .leading)
        .accessibilityElement(children: .combine)
    }

    func chip(_ name: String) -> some View {
        HStack(spacing: 6) {
            Image(systemName: "checkmark")
                .font(.system(size: 12, weight: .bold))
                .frame(width: 14, height: 14)
                .accessibilityHidden(true)
            Text(name).font(CleansiaTypography.bodyMedium)
        }
        .foregroundColor(CleansiaColors.onSurfaceVariant)
        .padding(.leading, 9)
        .padding(.trailing, Spacing.s)
        .padding(.vertical, Spacing.xxs)
        .frame(minHeight: 30)
        .background(CleansiaColors.surfaceVariant, in: Capsule())
    }
}

extension BookingSuccessView.SizeClass {
    var tiers: BookingSuccessView.Tiers {
        switch self {
        case .compact: .init(pMin: 7, pNat: 10, pMax: 14, pOver: 16, hdMin: 64, hdCap: 96)
        case .regular: .init(pMin: 10, pNat: 13, pMax: 20, pOver: 24, hdMin: 96, hdCap: 116)
        case .tall: .init(pMin: 10, pNat: 14, pMax: 20, pOver: 24, hdMin: 104, hdCap: 136)
        case .accessibility: .init(pMin: 14, pNat: 14, pMax: 14, pOver: 14, hdMin: 96, hdCap: 96)
        }
    }

    var seat: CGFloat {
        switch self {
        case .compact: 26
        case .regular: 28
        case .tall: 30
        case .accessibility: 36
        }
    }
}

struct BookingSuccess {
    var orderId: String
    var confirmationCode: String
    var cleaningAt: Date?
    var estimatedMinutes = 0
    var street = ""
    var city = ""
    var appliesCredit = false
    var requiredEmployees = 1
    var rooms = 1
    var bathrooms = 1
    var dirtiness: Dirtiness = .normal
    var basketNames: [String] = []
    var extraNames: [String] = []
    var paysCash = false

    func placed(orderId: String, confirmationCode: String) -> BookingSuccess {
        var placed = self
        placed.orderId = orderId
        placed.confirmationCode = confirmationCode
        return placed
    }
}

struct SummaryRow: Identifiable {
    let label: String
    let value: String

    var id: String {
        label
    }
}

#if DEBUG
    struct BookingSuccessView_Previews: PreviewProvider {
        static var previews: some View {
            BookingSuccessView(
                BookingSuccess(
                    orderId: "",
                    confirmationCode: "",
                    cleaningAt: Date().addingTimeInterval(3 * 86400),
                    estimatedMinutes: 180,
                    street: "Zenklova 6",
                    city: "Praha 8",
                    appliesCredit: true,
                    requiredEmployees: 2,
                    rooms: 2,
                    bathrooms: 1,
                    basketNames: ["General Cleaning"],
                    extraNames: ["Inside oven cleaning"]
                ),
                loadOrder: { _ in nil },
                onViewOrder: {},
                onDone: {}
            )
        }
    }
#endif
