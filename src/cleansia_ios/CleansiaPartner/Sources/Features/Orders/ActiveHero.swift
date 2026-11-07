import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// The cleaner's own job that matters now — in progress first, then on the way, then the next confirmed one — as a
/// card holding the detail's head with the state's figure seated beside its first rows: the number and status, the
/// day and window, the status line and its four steps, the street beside the pay, and the day line until she starts.
/// Under it what, how to reach the customer, and the one move: set off or start by slide, or open the job in
/// progress. Never a Complete.
struct ActiveHero: View {
    @Environment(\.locale) private var locale
    @Environment(\.dynamicTypeSize) private var typeSize
    let row: OrderListItem
    let now: Date
    var run: OrdersBoardRules.RunFacts?
    let isBusy: Bool
    let onOpen: () -> Void
    let onAdvance: () -> Void

    /// The figure stands 5 from the card's top, 64 square, beside the number and calendar rows.
    static let figureClearance = MascotClearance(width: 88, foot: 53)

    private var facts: JobHeadFacts {
        JobHeadFacts(row: row, run: run, now: now, locale: locale)
    }

    private var sentence: String {
        let status = facts.status
        return OrdersAccessibility.heroSentence(
            row,
            eyebrow: status.first ?? "",
            rightText: status.dropFirst().first,
            now: now,
            locale: locale
        )
    }

    private var coordinate: Coordinate? {
        guard let lat = row.customerAddressLatitude, let lon = row.customerAddressLongitude else { return nil }
        return Coordinate(latitude: lat, longitude: lon)
    }

    private var figure: MascotArt? {
        guard !typeSize.isAccessibilitySize else { return nil }
        switch row.status {
        case ._4: return .still(.sprayAndCloth)
        case ._3: return .still(.ready)
        default: return .still(.waving)
        }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            Button(action: onOpen) {
                JobHead(facts: facts, line: dayLine).contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .environment(\.mascotClearance, figure == nil ? .none : Self.figureClearance)
            .accessibilityElement(children: .ignore)
            .accessibilityLabel(sentence)
            .accessibilityAddTraits(.isButton)
            scopeRow.padding(.top, Spacing.s)
            if let cash = OrdersBoardRules.cashFlag(row) {
                TintedFlag(icon: "banknote", text: cash, tone: .amber)
                    .padding(.top, 10)
                    .accessibilityHidden(true)
            }
            ContactPills(address: row.customerAddress, coordinate: coordinate, phone: row.customerPhone)
                .padding(.top, Spacing.s)
            control.padding(.top, Spacing.s)
        }
        .padding(Spacing.m)
        .overlay(alignment: .topTrailing) {
            if let figure {
                MascotPuck(figure)
                    .frame(width: 64, height: 64)
                    .padding(.top, 5)
                    .padding(.trailing, Spacing.m)
            }
        }
        .ordersCard()
        .accessibilityElement(children: .contain)
    }

    // MARK: - The day line, until she starts

    private var dayLine: AnyView? {
        guard row.status != ._4, let start = row.cleaningDateTime, let end = OrdersBoardRules.bookedEnd(row)
        else { return nil }
        let job = OrdersBoardRules.minuteSpan(start: start, end: end)
        let opensAt = start.addingTimeInterval(-TimeInterval(OrdersBoardRules.startGraceWindowMinutes) * 60)
        let band = row.status == ._2 ? (start: job.start - 60, end: job.start) : nil
        let today = Calendar.current.isDate(start, inSameDayAs: now)
        let nowMinute = today ? OrdersBoardRules.minuteSpan(start: now, end: now).start : nil
        var labels: [OrdersDayLine.Label] = []
        if let nowMinute { labels.append(.init(minute: nowMinute, text: OrdersFormat.timeOnly(now, locale: locale))) }
        if let band, now < opensAt {
            labels.append(.init(
                minute: band.start,
                text: OrdersFormat.timeOnly(opensAt, locale: locale),
                isSetOff: true
            ))
        }
        return AnyView(OrdersDayLine(
            range: OrdersBoardRules.dayRange([job] + (band.map { [$0] } ?? [])),
            mine: [job],
            setOff: band,
            now: nowMinute,
            labels: labels
        ))
    }

    // MARK: - What, how to reach them, the one move

    private var scopeRow: some View {
        let parts = OrdersBoardRules.scopeParts(row, locale: locale) + [OrdersBoardRules.heroTeam(row)]
            .compactMap { $0 }
        let scope = OrdersDotFlow(items: parts.map { Text($0) }, tiesLast: true)
            .foregroundColor(CleansiaColors.onSurfaceVariant)
            .accessibilityHidden(true)
        return Group {
            if row.status == ._4 {
                scope
            } else {
                ViewThatFits(in: .horizontal) {
                    HStack(alignment: .firstTextBaseline, spacing: Spacing.s) {
                        scope.frame(minWidth: 170, idealWidth: 170, maxWidth: .infinity, alignment: .leading)
                        details
                    }
                    VStack(alignment: .leading, spacing: 0) {
                        scope
                        details.frame(maxWidth: .infinity, alignment: .trailing)
                    }
                }
            }
        }
    }

    private var details: some View {
        CleansiaTextLink(L10n.Orders.heroDetails, trailingIcon: "chevron.right", action: onOpen)
            .fixedSize()
            .padding(.vertical, -Spacing.s)
            .padding(.trailing, -Spacing.xxs)
    }

    @ViewBuilder
    private var control: some View {
        switch OrdersBoardRules.heroControl(row, now: now) {
        case let .setOff(opensAt?, false)?:
            SlideToConfirm(
                lockedReason: L10n.tie(L10n.Orders.setOffFrom(OrdersFormat.timeOnly(opensAt, locale: locale)))
            )
        case .setOff?:
            slide(idle: L10n.Orders.swipeToNotifyOnTheWay, busy: L10n.Orders.customerNotifiedOnTheWay)
        case .start?:
            slide(idle: L10n.Orders.slideToStart, busy: L10n.Orders.startingOrder)
        case .openJob?:
            CleansiaOutlinedButton(
                L10n.Orders.openJob,
                size: .medium,
                contentColor: CleansiaColors.primaryText,
                borderColor: CleansiaColors.primary,
                action: onOpen
            )
        case nil:
            EmptyView()
        }
    }

    private func slide(idle: String, busy: String) -> some View {
        SlideToConfirm(
            idleLabel: L10n.tie(idle),
            busyLabel: L10n.tie(busy),
            isBusy: isBusy,
            maxLabelLines: 2,
            thumbLanes: true,
            onConfirm: onAdvance
        )
    }
}

/// The take was refused: the server's own reason as a red flag, and the way back to the board.
struct RefusedTake: View {
    let reason: String
    let onBack: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            TintedFlag(icon: "exclamationmark.circle", text: reason, tone: .error)
            CleansiaPrimaryButton(L10n.WorkContract.backToJobs, action: onBack)
        }
        .onAppear { OrdersAccessibility.layoutChanged(reason) }
    }
}

/// The take sheet's facts in the head's own slots, in a well: how far off the day is, the calendar line, and where;
/// under it, before the take, what and the reward.
struct TakeFacts<Footer: View>: View {
    @Environment(\.locale) private var locale
    let date: Date?
    let window: String?
    let place: String?
    var detail: String?
    var live = false
    var now = Date()
    @ViewBuilder var footer: () -> Footer

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            if let date {
                OrdersDotFlow(
                    items: OrdersBoardRules.howFarOff(date, live: live, now: now, locale: locale).map { Text($0) },
                    style: CleansiaTypography.scale.titleLarge
                )
                .foregroundColor(CleansiaColors.onSurface)
                .accessibilityAddTraits(.isHeader)
                OrderCalendarLine(OrdersFormat.shortDay(date, locale: locale), window: window)
                    .padding(.top, Spacing.hair)
            }
            if place != nil || detail != nil {
                VStack(alignment: .leading, spacing: Spacing.hair) {
                    if let place {
                        Text(place)
                            .font(CleansiaTypography.bodyLarge)
                            .foregroundColor(CleansiaColors.onSurface)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                    if let detail {
                        Text(detail)
                            .font(CleansiaTypography.bodyMedium)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                }
                .padding(.top, Spacing.xs)
            }
            footer()
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.surfaceVariant, in: RoundedRectangle(cornerRadius: CornerRadius.medium))
    }
}

/// What the Job taken panel says beside the contract: the area, her reward, the status its cancel rule reads, and the
/// strip.
struct TakenJob: Equatable {
    var area: String?
    var reward: String?
    var status: OrderStatus?
    var strip: TakenContract.Strip?

    init(board: BoardTake) {
        let row = board.taken ?? board.snapshot
        area = OrdersBoardRules.areaLine(row)
        reward = OrdersBoardRules.pay(row)
        status = row.status
        strip = TakenContract.strip(board)
    }

    /// The offer's take, before the job is read back: its area and reward, the strip waiting.
    init(offer: PendingOfferItem?) {
        area = offer?.customerAddressApproximate
        reward = offer?.estimatedCleanerPay.map {
            OrdersFormat.money($0, symbol: EarningsFormat.currencySymbol(offer?.currencyCode))
        }
        strip = .waiting
    }

    /// The detail's take: the order as the detail holds it, the strip waiting until it has been read back as hers.
    init(detail order: OrderDetail?, readBack: Bool) {
        area = order?.area
        reward = order?.pay.map { OrdersFormat.money($0, symbol: order?.currencySymbol) }
        status = order?.status
        guard readBack else {
            strip = .waiting
            return
        }
        guard let order,
              case let .pin(coordinate, street) = JobBandMap.mode(
                  location: order.location,
                  area: order.area,
                  taken: true
              )
        else {
            strip = nil
            return
        }
        strip = .pin(coordinate, street: street)
    }
}

/// The take sheet's taken state: Job taken with the thumbs-up, the address now known on a strip
/// of map, the day and window beside its leaf with the area and her reward, whether it can still be cancelled, and the
/// two ways on, pinned. The strip waits while the taken job is looked up and is left out when it has no coordinate.
struct TakenContract: View {
    enum Strip: Equatable {
        case waiting
        case pin(Coordinate, street: String?)
    }

    static func strip(_ board: BoardTake?) -> Strip? {
        guard let board else { return nil }
        guard board.takenLookupDone else { return .waiting }
        guard let taken = board.taken,
              let latitude = taken.customerAddressLatitude,
              let longitude = taken.customerAddressLongitude
        else { return nil }
        return .pin(
            Coordinate(latitude: latitude, longitude: longitude),
            street: OrdersBoardRules.streetLine(taken.customerAddress)
        )
    }

    @Environment(\.locale) private var locale
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @State private var appeared = false
    @State private var interactive = false
    @State private var contentHeight: CGFloat = 0
    @State private var buttonsHeight: CGFloat = 0
    let contract: WorkContract
    let job: TakenJob?
    let onBack: () -> Void
    let onOpenJob: () -> Void
    var onHeight: (CGFloat) -> Void = { _ in }
    var now = Date()

    private var facts: WorkContractJobFacts {
        contract.facts
    }

    private var area: String? {
        job?.area ?? facts.locationApproximate
    }

    private var cancel: String? {
        let line = OrdersBoardRules.cancelLine(
            status: job?.status,
            start: facts.cleaningDateTimeUtc,
            now: now,
            version: contract.version
        )
        return OrdersBoardRules.cancelText(line, phase: .taken, locale: locale)
    }

    var body: some View {
        VStack(spacing: 0) {
            ScrollView {
                VStack(alignment: .leading, spacing: 14) {
                    title
                    strip
                    ticket
                    if let cancel {
                        HStack(alignment: .firstTextBaseline, spacing: Spacing.xs) {
                            Image(systemName: "info.circle")
                                .font(CleansiaTypography.bodyMedium)
                                .capBandAligned(of: CleansiaTypography.scale.bodyMedium)
                                .accessibilityHidden(true)
                            Text(L10n.tie(cancel))
                                .font(CleansiaTypography.bodyMedium)
                                .fixedSize(horizontal: false, vertical: true)
                        }
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                }
                .padding(.horizontal, Spacing.m)
                .padding(.top, Spacing.l)
                .padding(.bottom, Spacing.m)
                .background(GeometryReader { space in
                    Color.clear
                        .onAppear { report(content: space.size.height) }
                        .onChange(of: space.size.height) { report(content: $0) }
                })
            }
            VStack(spacing: Spacing.xxs) {
                CleansiaPrimaryButton(L10n.Orders.openJob, action: onOpenJob)
                CleansiaTextLink(L10n.WorkContract.backToJobs, action: onBack)
            }
            .padding(.horizontal, Spacing.m)
            .padding(.top, Spacing.s)
            .padding(.bottom, Spacing.xs)
            .background(GeometryReader { space in
                Color.clear
                    .onAppear { report(buttons: space.size.height) }
                    .onChange(of: space.size.height) { report(buttons: $0) }
            })
        }
        .allowsHitTesting(interactive)
        .opacity(appeared ? 1 : 0)
        .offset(y: appeared || reduceMotion ? 0 : 12)
        .onAppear(perform: enter)
    }

    private var title: some View {
        HStack(alignment: .bottom) {
            Text(L10n.WorkContract.doneTitle)
                .cleansiaFont(CleansiaTypography.headlineSmall)
                .foregroundColor(CleansiaColors.onSurface)
                .accessibilityAddTraits(.isHeader)
                .padding(.bottom, Spacing.xs)
            Spacer(minLength: Spacing.s)
            Mascot.thumbsUp.image
                .resizable()
                .scaledToFit()
                .frame(width: 84, height: 84)
                .padding(.vertical, -Spacing.xs)
                .accessibilityHidden(true)
        }
    }

    @ViewBuilder
    private var strip: some View {
        switch job?.strip {
        case .waiting:
            RoundedRectangle(cornerRadius: CornerRadius.medium)
                .fill(CleansiaColors.surfaceVariant)
                .frame(height: 120)
                .accessibilityHidden(true)
        case let .pin(coordinate, street):
            PlaceMap(coordinate: coordinate, anchor: 0.78, label: street)
                .frame(height: 120)
                .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
        case nil:
            EmptyView()
        }
    }

    /// The leaf, then "Tomorrow · 14:15–14:22" over "Praha 8 · Your reward 805 Kč".
    private var ticket: some View {
        let date = facts.cleaningDateTimeUtc
        return HStack(alignment: .center, spacing: 14) {
            OrdersDateTile(date: date, size: .ticket, now: now)
            VStack(alignment: .leading, spacing: Spacing.hair) {
                OrdersDotFlow(
                    items: [
                        Text(OrdersAccessibility.dayWord(date, now: now, locale: locale)),
                        Text(OrdersFormat.timeWindow(date, minutes: facts.estimatedMinutes, locale: locale))
                            .monospacedDigit()
                    ],
                    style: CleansiaTypography.scale.titleMedium
                )
                .foregroundColor(CleansiaColors.onSurface)
                OrdersDotFlow(
                    items: [
                        area.map { Text($0) },
                        job?.reward.map { Text(L10n.tie("\(L10n.WorkContract.reward) \($0)")) }
                    ].compactMap { $0 },
                    style: CleansiaTypography.scale.bodyMedium
                )
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .accessibilityElement(children: .combine)
    }

    private func report(content: CGFloat? = nil, buttons: CGFloat? = nil) {
        if let content { contentHeight = content }
        if let buttons { buttonsHeight = buttons }
        if contentHeight > 0, buttonsHeight > 0 { onHeight(contentHeight + buttonsHeight) }
    }

    /// Fades up over 320 ms (opacity only under Reduce Motion); taps wait 300 ms so the swipe's finger cannot
    /// land on a button.
    private func enter() {
        let animation: Animation = reduceMotion
            ? .easeOut(duration: 0.2)
            : .timingCurve(0.22, 1, 0.36, 1, duration: 0.32)
        withAnimation(animation) { appeared = true }
        OrdersAccessibility.screenChanged(L10n.WorkContract.doneTitle)
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) { interactive = true }
    }
}

extension JobHeadFacts {
    /// The Active hero's head, from her own job's row: what the detail's head says, as it says it. The customer's
    /// name rides the area, since the hero draws no customer row.
    init(row: OrderListItem, run: OrdersBoardRules.RunFacts?, now: Date, locale: Locale) {
        let named = row.customerName.flatMap { $0.isBlank ? nil : $0 }
        number = row.displayOrderNumber.flatMap { $0.isBlank ? nil : "#\($0)" } ?? ""
        statusWire = row.status?.rawValue
        statusLabel = L10n.Orders.statusLabel(row.status)
        day = row.cleaningDateTime.map { OrdersFormat.shortDay($0, locale: locale) }
        window = OrdersBoardRules.window(of: row, locale: locale)
        status = run.flatMap { OrdersBoardRules.runStatus($0, now: now, locale: locale)?.items } ?? []
        progress = Self.progress(runFacts: run, isMine: true, now: now)
        street = OrdersBoardRules.streetLine(row.customerAddress) ?? OrdersBoardRules.areaLine(row)
        sub = [OrdersBoardRules.streetLine(row.customerAddress) == nil ? nil : OrdersBoardRules.areaLine(row), named]
            .compactMap { $0 }
        pay = OrdersBoardRules.pay(row)
        caption = (row.requiredEmployees ?? 0) > 1 ? L10n.Orders.yourShare : L10n.WorkContract.reward
    }
}
