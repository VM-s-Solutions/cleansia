import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct BookingSuccessView: View {
    let booking: BookingSuccess
    let revealed: Bool
    let mascotTravels: Bool
    let onViewOrder: (() -> Void)?
    let onDone: () -> Void
    let onDetailsSettled: () -> Void
    let onHeroMascotFrame: (CGRect) -> Void
    /// Off for a confirmation laid out before its order exists: it stays as the booked one first arrives, its
    /// details loading.
    let loadsDetails: Bool

    @StateObject private var orderVM: BookingSuccessViewModel
    @State private var viewport: CGFloat = 0
    @State private var titleBlockHeight: CGFloat = 0
    @State private var rowHeights: [CGFloat] = [0, 0, 0, 0]
    @State private var decided: (size: SizeClass, fit: (header: CGFloat, rowPad: CGFloat))?
    @State private var actionRoom: CGFloat = 0
    @State private var actionLabels: [CGFloat] = [0, 0]
    @State private var contentRunsUnder = false
    @State private var arrived: Bool
    @State private var figureShown: Bool
    @State private var interactive: Bool
    @State private var welcomePlayed = false
    @AccessibilityFocusState private var titleFocused: Bool
    @Environment(\.locale) private var locale
    @Environment(\.dynamicTypeSize) private var dynamicTypeSize
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @Environment(\.scenePhase) private var scenePhase
    @Environment(\.colorScheme) private var colorScheme

    static let grabZone: CGFloat = 18
    private static let scrollSpace = "BookingSuccessScroll"
    private static let landing = Duration.milliseconds(420)
    private static let settled = Duration.milliseconds(700)
    private static let welcomeLength = Duration.milliseconds(2100)
    private static let figureEntrance = Animation.timingCurve(0.22, 1, 0.36, 1, duration: 0.24)

    init(
        _ booking: BookingSuccess,
        loadOrder: @escaping @Sendable (String) async -> CustomerOrderDetail?,
        warmOrders: @escaping @Sendable () async -> Void = {},
        revealed: Bool = true,
        mascotTravels: Bool = false,
        onViewOrder: (() -> Void)?,
        onDone: @escaping () -> Void,
        onDetailsSettled: @escaping () -> Void = {},
        onHeroMascotFrame: @escaping (CGRect) -> Void = { _ in },
        loadsDetails: Bool = true
    ) {
        self.booking = booking
        self.revealed = revealed
        self.mascotTravels = mascotTravels
        self.onViewOrder = onViewOrder
        self.onDone = onDone
        self.onDetailsSettled = onDetailsSettled
        self.onHeroMascotFrame = onHeroMascotFrame
        self.loadsDetails = loadsDetails
        _orderVM = StateObject(wrappedValue: BookingSuccessViewModel(
            orderId: booking.orderId,
            fetch: loadOrder,
            warmOrders: warmOrders
        ))
        _arrived = State(initialValue: revealed)
        _figureShown = State(initialValue: revealed)
        _interactive = State(initialValue: revealed)
    }

    var body: some View {
        let size = Self.sizeClass(room: viewport, accessibilityText: dynamicTypeSize.isAccessibilitySize)
        let fit = decided.flatMap { $0.size == size ? $0.fit : nil } ?? fit(size)
        VStack(spacing: 0) {
            bounceOnlyWhenScrolling(ScrollView {
                content(size, fit: fit)
                    .frame(minHeight: viewport, alignment: .top)
                    .reporting {
                        $0.frame(in: .named(Self.scrollSpace)).maxY > viewport + 0.5
                    }
                    action: {
                        contentRunsUnder = $0
                    }
            })
            .coordinateSpace(name: Self.scrollSpace)
            .reporting {
                $0.size.height
            } action: {
                viewport = $0
            }
            actionBar(size)
        }
        .padding(.top, Self.grabZone)
        .ignoresSafeArea(.keyboard)
        .background(ground.ignoresSafeArea())
        .task { if loadsDetails { await orderVM.load() } }
        .task(id: revealed) { try? await arrive() }
        .onReceive(orderVM.$state) { state in
            if case .loading = state { return }
            onDetailsSettled()
        }
        .onAppear { if scenePhase != .active { welcomePlayed = true } }
        .onChange(of: scenePhase) { phase in
            if phase != .active { welcomePlayed = true }
        }
    }

    /// Spread from the title's and the rows' own heights, which no fit changes. The viewport ignores the
    /// keyboard, which can still be up from the form's notes at the hand-over.
    private func fit(_ size: SizeClass) -> (header: CGFloat, rowPad: CGFloat) {
        let tiers = size.tiers
        let header = max(tiers.hdMin, titleBlockHeight)
        guard viewport > 0, !rowHeights.contains(0) else { return (header, tiers.pMin) }
        let rulesAndEdges: CGFloat = 5
        let natural = header + rowHeights.reduce(0, +) + 8 * tiers.pMin + rulesAndEdges
        let spread = Self.distribute(available: viewport, natural: natural, tiers: Tiers(
            pMin: tiers.pMin, pNat: tiers.pNat, pMax: tiers.pMax, pOver: tiers.pOver,
            hdMin: header, hdCap: max(tiers.hdCap, header)
        ), restCap: size.restCap)
        return (spread.header, spread.rowPad)
    }

    /// Fixed once, as the busy overlay starts to leave: the confirmation has been laid out under it with
    /// whatever the order has said by then, and details that land later never move a row.
    private func decideFit() {
        let size = Self.sizeClass(room: viewport, accessibilityText: dynamicTypeSize.isAccessibilitySize)
        guard decided == nil, viewport > 0, !rowHeights.contains(0) else { return }
        decided = (size, fit(size))
    }

    private func arrive() async throws {
        guard revealed else { return }
        let start = ContinuousClock.now
        guard !arrived else {
            try await Task.sleep(until: start + Self.settled, clock: .continuous)
            decideFit()
            try await Task.sleep(until: start + Self.welcomeLength, clock: .continuous)
            welcomePlayed = true
            return
        }
        decideFit()
        arrived = true
        let welcomeStart = mascotTravels ? Self.landing : .zero
        if mascotTravels {
            try await Task.sleep(until: start + Self.landing, clock: .continuous)
            figureShown = true
        } else {
            withAnimation(Self.figureEntrance) { figureShown = true }
        }
        try await Task.sleep(until: start + Self.settled, clock: .continuous)
        interactive = true
        titleFocused = true
        try await Task.sleep(until: start + welcomeStart + Self.welcomeLength, clock: .continuous)
        welcomePlayed = true
    }

    private func content(_ size: SizeClass, fit: (header: CGFloat, rowPad: CGFloat)) -> some View {
        VStack(spacing: 0) {
            header(size, height: fit.header).zIndex(1)
            ticket(size, pad: fit.rowPad)
            if size == .accessibility, onViewOrder != nil {
                CleansiaOutlinedButton(L10n.Booking.successGoHome, action: onDone)
                    .allowsHitTesting(interactive)
                    .padding(.vertical, Spacing.m)
            }
        }
        .padding(.horizontal, Spacing.m)
        .frame(maxWidth: .infinity)
    }

    private func header(_ size: SizeClass, height: CGFloat) -> some View {
        let figureHeight = size == .accessibility ? size.tiers.hdMin : height
        let laidOut = viewport > 0 && !rowHeights.contains(0)
        return HStack(alignment: .bottom, spacing: Spacing.s) {
            titleBlock
                .padding(.leading, Spacing.m)
                .padding(.bottom, 14)
                .reporting {
                    $0.size.height
                } action: {
                    titleBlockHeight = $0
                }
                .frame(maxWidth: .infinity, alignment: .leading)
            figure(height: figureHeight)
                .reporting {
                    laidOut ? $0.frame(in: .global) : .null
                } action: { box in
                    guard !box.isNull else { return }
                    onHeroMascotFrame(Self.mascotCanvas(figureHeight: box.height).offsetBy(
                        dx: box.minX,
                        dy: box.minY
                    ))
                }
                .padding(.trailing, 14)
                .padding(.bottom, -1)
        }
        .frame(minHeight: height, alignment: .bottom)
    }

    private func ticket(_ size: SizeClass, pad: CGFloat) -> some View {
        VStack(alignment: .leading, spacing: 0) {
            row(0, pad: pad, findingRow(size))
            rule
            row(
                1,
                pad: pad,
                answerRow(size, date: shownDate, minutes: shownMinutes, address: shownAddress, locale: locale)
            )
            rule
            row(2, pad: pad, bookedRow)
            rule
            row(3, pad: pad, moneyRow)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.horizontal, Spacing.m)
        .padding(.vertical, 1)
        .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.large))
        .overlay(RoundedRectangle(cornerRadius: CornerRadius.large).strokeBorder(CleansiaColors.outline, lineWidth: 1))
        .animation(.easeOut(duration: 0.25), value: orderVM.order)
        .animation(.easeOut(duration: 0.25), value: detailsFailed)
    }

    private func row(_ index: Int, pad: CGFloat, _ content: some View) -> some View {
        content
            .reporting {
                $0.size.height
            } action: {
                rowHeights[index] = $0
            }
            .padding(.vertical, pad)
    }

    private func actionBar(_ size: SizeClass) -> some View {
        Group {
            if size == .accessibility, let onViewOrder {
                CleansiaPrimaryButton(L10n.Orders.viewOrder, action: onViewOrder)
            } else {
                actions
            }
        }
        .allowsHitTesting(interactive)
        .padding(.horizontal, Spacing.m)
        .padding(.top, Spacing.s)
        .padding(.bottom, Spacing.xs)
        .background(alignment: .top) {
            if contentRunsUnder {
                Rectangle().fill(CleansiaColors.outline).frame(height: 1)
            }
        }
        .background(ground.ignoresSafeArea())
    }

    /// Back to home at its 1 : 1.4 share; else View order at no less than its own label, the rest to Back to
    /// home; else the stacked fallback.
    @ViewBuilder
    private var actions: some View {
        if let onViewOrder {
            let home = CleansiaOutlinedButton(
                L10n.Booking.successGoHome, horizontalPadding: Self.actionPadding, action: onDone
            )
            let view = CleansiaPrimaryButton(L10n.Orders.viewOrder, action: onViewOrder)
            ViewThatFits(in: .horizontal) {
                HStack(spacing: Spacing.s) {
                    home.frame(width: Self.homeActionWidth(
                        room: actionRoom, homeLabel: actionLabels[0], viewLabel: actionLabels[1]
                    ))
                    view
                }
                HStack(spacing: Spacing.s) {
                    home
                    view.fixedSize(horizontal: true, vertical: false)
                }
                VStack(spacing: Spacing.xs) {
                    view
                    CleansiaTextLink(L10n.Booking.successGoHome, action: onDone)
                        .frame(maxWidth: .infinity)
                }
            }
            .reporting { $0.size.width } action: { actionRoom = $0 }
            .background {
                HStack {
                    Text(L10n.Booking.successGoHome).reporting { $0.size.width } action: { actionLabels[0] = $0 }
                    Text(L10n.Orders.viewOrder).reporting { $0.size.width } action: { actionLabels[1] = $0 }
                }
                .font(CleansiaTypography.titleMedium)
                .lineLimit(1)
                .fixedSize()
                .hidden()
            }
        } else {
            CleansiaPrimaryButton(L10n.Booking.successGoHome, action: onDone)
        }
    }
}

private extension BookingSuccessView {
    var isLoading: Bool {
        if case .loading = orderVM.state { return true }
        return false
    }

    var detailsFailed: Bool {
        if case .error = orderVM.state { return true }
        return false
    }

    var shownDate: Date? {
        orderVM.order?.cleaningDateTime ?? booking.cleaningAt
    }

    var shownMinutes: Int {
        if let minutes = orderVM.order?.estimatedMinutes, minutes > 0 { return minutes }
        return booking.estimatedMinutes
    }

    var shownAddress: String? {
        let order = orderVM.order?.address
        return Self.addressLine(street: order?.street, city: order?.city)
            ?? Self.addressLine(street: booking.street, city: booking.city)
    }

    var ground: Color {
        colorScheme == .dark ? CleansiaColors.background : CleansiaColors.surfaceVariant
    }

    var seatInk: Color {
        CleansiaColors.onSurfaceVariant.opacity(colorScheme == .dark ? 0.8 : 0.62)
    }

    var rule: some View {
        Rectangle().fill(CleansiaColors.outline).frame(height: 1)
    }

    var titleBlock: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(L10n.Booking.successTitle)
                .cleansiaFont(CleansiaTypography.headlineMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .fixedSize(horizontal: false, vertical: true)
                .dynamicTypeSize(...DynamicTypeSize.xxxLarge)
                .accessibilityAddTraits(.isHeader)
                .accessibilityFocused($titleFocused)
            referenceLine
        }
    }

    @ViewBuilder
    var referenceLine: some View {
        let reference = orderVM.order?.displayOrderNumber.flatMap { $0.isBlank ? nil : $0 }
        if let reference {
            Text(reference)
                .font(CleansiaTypography.bodyMedium)
                .monospacedDigit()
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .textSelection(.enabled)
        } else if isLoading {
            barLine(width: 116, height: 15, font: CleansiaTypography.bodyMedium)
        }
    }

    func findingRow(_ size: SizeClass) -> some View {
        VStack(alignment: .leading, spacing: 0) {
            VStack(alignment: .leading, spacing: 0) {
                Text(L10n.Booking.successFinding)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                Group {
                    Text(L10n.Booking.successFindingSub)
                    Text(Self.remedy(booking))
                }
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                crewLine(size).padding(.top, 10)
            }
            .fixedSize(horizontal: false, vertical: true)
            .accessibilityElement(children: .ignore)
            .accessibilityLabel(Text(Self.findingSpoken(booking)))
            deadlineLine
                .padding(.top, Spacing.s)
        }
    }

    func crewLine(_ size: SizeClass) -> some View {
        HStack(spacing: 10) {
            HStack(spacing: -6) {
                ForEach(0 ..< max(1, booking.requiredEmployees), id: \.self) { _ in
                    seat(size.seat)
                }
            }
            Text(L10n.Booking.successCrew(booking.requiredEmployees))
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
    }

    func seat(_ diameter: CGFloat) -> some View {
        ZStack {
            Circle().fill(CleansiaColors.surface)
            Circle().strokeBorder(seatInk, style: StrokeStyle(lineWidth: 1.5, dash: [3, 2.5]))
            Image(systemName: "person")
                .resizable()
                .scaledToFit()
                .frame(width: diameter * 0.56, height: diameter * 0.56)
                .foregroundColor(seatInk)
        }
        .frame(width: diameter, height: diameter)
        .accessibilityHidden(true)
    }

    @ViewBuilder
    var deadlineLine: some View {
        let regular = CleansiaTypography.bodyMedium
        let now = Date()
        switch orderVM.state {
        case .loading:
            if Self.deadlineAheadOnEveryPlan(cleaningAt: shownDate, now: now) {
                freeUntil(Self.freeCancellationDeadline(
                    cleaningAt: shownDate, freeHours: CancellationPolicyBuilder.standardFreeHours, now: now,
                    locale: locale
                ) ?? "")
                    .reservingLines {
                        HStack(spacing: Spacing.xxs) {
                            Text(L10n.Booking.successFreeUntil("").trimmingCharacters(in: .whitespaces))
                                .font(regular)
                                .foregroundColor(CleansiaColors.onSurface)
                            placeholder(width: 164, height: 17)
                        }
                    }
            } else {
                freeWhile(regular).reservingLines { barLine(width: 250, height: 17, font: regular) }
            }
        case let .loaded(order):
            if let date = Self.freeCancellationDeadline(
                cleaningAt: shownDate, freeHours: order.freeCancellationHours, now: now, locale: locale
            ) {
                freeUntil(date)
            } else {
                freeWhile(regular)
            }
        case .error:
            freeWhile(regular)
        }
    }

    func placeholder(width: CGFloat, height: CGFloat) -> some View {
        RoundedRectangle(cornerRadius: 5)
            .fill(CleansiaColors.onSurface.opacity(colorScheme == .dark ? 0.12 : 0.08))
            .frame(width: width, height: height)
            .accessibilityHidden(true)
    }

    /// A bar at a line's own height, so the value that replaces it moves nothing.
    func barLine(width: CGFloat, height: CGFloat, font: Font, alignment: Alignment = .leading) -> some View {
        Text(verbatim: "0")
            .font(font)
            .hidden()
            .frame(width: width, alignment: alignment)
            .overlay(alignment: alignment) { placeholder(width: width, height: height) }
            .accessibilityHidden(true)
    }
}

private extension BookingSuccessView {
    var moneyRow: some View {
        let money = Self.paymentRows(orderVM.state, booking: booking)
        return VStack(alignment: .leading, spacing: 0) {
            VStack(alignment: .leading, spacing: Spacing.xxs) {
                ForEach(Array(money.rows.enumerated()), id: \.element.id) { index, row in
                    moneyLine(row, index: index)
                }
            }
            if detailsFailed {
                retryLine
            } else if let note = money.note {
                Text(note)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .fixedSize(horizontal: false, vertical: true)
                    .padding(.top, 2)
            }
        }
        .accessibilityElement(children: isLoading ? .ignore : .contain)
        .accessibilityLabel(isLoading ? Text(L10n.Booking.successDetailsLoading) : Text(verbatim: ""))
    }

    func moneyLine(_ row: SummaryRow, index: Int) -> some View {
        let value = index == 0 ? CleansiaTypography.titleMedium : CleansiaTypography.bodyMedium
        return HStack(alignment: .firstTextBaseline, spacing: Spacing.s) {
            Text(row.label)
                .font(index == 0 ? CleansiaTypography.bodyLarge : CleansiaTypography.bodyMedium)
                .foregroundColor(index == 0 ? CleansiaColors.onSurface : CleansiaColors.onSurfaceVariant)
                .frame(maxWidth: .infinity, alignment: .leading)
            if row.value.isEmpty {
                barLine(
                    width: [88, 58, 64][min(index, 2)],
                    height: index == 0 ? 20 : 16,
                    font: value,
                    alignment: .trailing
                )
            } else if detailsFailed {
                Text(row.value)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .accessibilityLabel(Text(L10n.Booking.successDetailsUnavailable))
            } else {
                Text(row.value).font(value).monospacedDigit().foregroundColor(CleansiaColors.onSurface).fixedSize()
            }
        }
        .padding(.bottom, index == 0 ? 2 : 0)
        .accessibilityElement(children: .combine)
    }

    var retryLine: some View {
        HStack(alignment: .firstTextBaseline, spacing: Spacing.s) {
            Text(L10n.Booking.successDetailsUnavailable)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .fixedSize(horizontal: false, vertical: true)
                .frame(maxWidth: .infinity, alignment: .leading)
                .accessibilityHidden(true)
            Button {
                Task { await orderVM.retry() }
            } label: {
                Text(L10n.retry)
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(CleansiaColors.primaryText)
                    .padding(.vertical, 11)
                    .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .padding(.vertical, -11)
            .allowsHitTesting(interactive)
        }
        .padding(.top, 6)
    }

    /// The figure's box is the welcoming art's union box at `height`; the 360 px canvas hangs off it, so
    /// the box can stand on the ticket while the drawing keeps its own margins.
    func figure(height: CGFloat) -> some View {
        let canvas = Self.mascotCanvas(figureHeight: height)
        return Color.clear
            .frame(width: canvas.width * 167 / 360, height: height)
            .overlay(alignment: .topLeading) {
                if figureShown || reduceMotion {
                    welcomeArt
                        .frame(width: canvas.width, height: canvas.height)
                        .offset(x: canvas.minX, y: canvas.minY)
                        .transition(mascotTravels ? .identity : .opacity.combined(with: .scale(scale: 0.92)))
                }
            }
            .allowsHitTesting(false)
            .accessibilityHidden(true)
    }

    /// The welcome plays once. Once played, or with motion off or the scene inactive, the thumbs-up
    /// still stands in, so nothing — a resume, a redraw — can start it from the wink again.
    @ViewBuilder
    var welcomeArt: some View {
        if reduceMotion || welcomePlayed || scenePhase != .active {
            Image(decorative: "mascot_welcoming_end").resizable().scaledToFit()
        } else {
            AnimatedMascotView(.welcoming, loop: false, fallback: .waving)
        }
    }
}

private extension View {
    /// The lines of the text that will replace a placeholder, kept empty under it so its arrival moves nothing.
    func reservingLines(_ placeholder: () -> some View) -> some View {
        hidden()
            .frame(maxWidth: .infinity, alignment: .leading)
            .overlay(alignment: .topLeading, content: placeholder)
    }

    /// Unlike `onGeometryChange`, which on iOS 16 reports changes only, this also reports the first value:
    /// a row that never changes height still has to be counted.
    func reporting<Value: Equatable>(
        _ value: @escaping (GeometryProxy) -> Value,
        action: @escaping (Value) -> Void
    ) -> some View {
        background(GeometryReader { proxy in
            Color.clear
                .onAppear { action(value(proxy)) }
                .onChange(of: value(proxy)) { action($0) }
        })
    }
}
