import CleansiaCore
import CleansiaPartnerApi
import Foundation

/// The Orders board's pure rules: what a card, the hero, the take sheet and History say, decided from the rows
/// alone. `now` and `locale` come in from the caller, so every rule is testable without a clock or a device.
/// Android's `OrdersBoardRules.kt` holds the same rules.
enum OrdersBoardRules {
    /// How early a cleaner may set off: the server refuses "on the way" before the start minus this.
    /// → docs/product/business-rules.md (start grace window); mirrors `BookingPolicy.StartGraceWindowMinutes`.
    static let startGraceWindowMinutes = 60

    /// The board's " · ", bound to the word before it so a wrapped line never starts with the dot.
    static let dot = "\u{00A0}· "

    enum LiveFlag: Equatable {
        case underWay(until: Date)
        case takeUntil(Date)
    }

    enum CancelLine: Equatable {
        case freeUntil(Date)
        case underWay
        case startPassed
    }

    enum CancelPhase {
        case beforeTake
        case taken
    }

    enum HeroControl: Equatable {
        case setOff(opensAt: Date?, enabled: Bool)
        case start
        case openJob
    }

    /// A row's decision flags, in the order the row draws them. Under way is not one: it sits beside the window.
    enum Flag: Equatable {
        case overlap(String)
        case takeUntil(String)
        case cash(String)
        case team(String)
        case dirt(String)

        var text: String {
            switch self {
            case let .overlap(text), let .takeUntil(text), let .cash(text), let .team(text), let .dirt(text): text
            }
        }

        var isAmber: Bool {
            switch self {
            case .cash: true
            case .overlap, .takeUntil, .team, .dirt: false
            }
        }
    }

    // MARK: - Time

    /// "09:00–13:00"; the start alone when the estimate is not positive.
    static func window(start: Date?, minutes: Int?, locale: Locale) -> String? {
        guard let start else { return nil }
        return OrdersFormat.timeWindow(start, minutes: minutes ?? 0, locale: locale)
    }

    static func window(of row: OrderListItem, locale: Locale) -> String? {
        window(start: row.cleaningDateTime, minutes: row.estimatedTime, locale: locale)
    }

    static func bookedEnd(_ row: OrderListItem) -> Date? {
        row.cleaningDateTime.map { $0.addingTimeInterval(TimeInterval(max(row.estimatedTime ?? 0, 0)) * 60) }
    }

    /// The server stops offering a job whose booked slot has ended, so the board hides it at render.
    static func isExpired(_ row: OrderListItem, now: Date) -> Bool {
        guard let end = bookedEnd(row) else { return false }
        return end <= now
    }

    static func liveFlag(_ row: OrderListItem, now: Date) -> LiveFlag? {
        guard let end = bookedEnd(row), let start = row.cleaningDateTime else { return nil }
        switch row.status {
        case ._3, ._4: return .underWay(until: end)
        case ._0, ._2: return start <= now ? .takeUntil(end) : nil
        default: return nil
        }
    }

    /// The server's own overlap rule: windows that only touch do not clash, and there is no buffer. A row with no
    /// start never clashes; a row with no estimate is a point. The earliest clashing own job is the one named.
    static func clashes(_ candidate: OrderListItem, own: [OrderListItem]) -> OrderListItem? {
        guard let start = candidate.cleaningDateTime, let end = bookedEnd(candidate) else { return nil }
        return clash(start: start, end: end, own: own)
    }

    // MARK: - Active hero

    /// Status first (in progress, then on the way, then confirmed), then the earliest start.
    static func heroPick(_ own: [OrderListItem]) -> OrderListItem? {
        own.filter { rank($0) > 0 }.min { lhs, rhs in
            if rank(lhs) != rank(rhs) { return rank(lhs) > rank(rhs) }
            return (lhs.cleaningDateTime ?? .distantFuture) < (rhs.cleaningDateTime ?? .distantFuture)
        }
    }

    static func setOffOpensAt(_ row: OrderListItem) -> Date? {
        row.cleaningDateTime?.addingTimeInterval(-TimeInterval(startGraceWindowMinutes) * 60)
    }

    /// What the hero offers. Never a Complete: the detail carries it, with the photos and the crew it reads.
    static func heroControl(_ row: OrderListItem, now: Date) -> HeroControl? {
        switch row.status {
        case ._2:
            .setOff(opensAt: setOffOpensAt(row), enabled: setOffLock(start: row.cleaningDateTime, now: now) == nil)
        case ._3: .start
        case ._4: .openJob
        default: nil
        }
    }

    private static func rank(_ row: OrderListItem) -> Int {
        switch row.status {
        case ._4: 3
        case ._3: 2
        case ._2: 1
        default: 0
        }
    }

    // MARK: - Cancelling

    static func cancelLine(status: OrderStatus?, start: Date?, now: Date, version: String) -> CancelLine? {
        switch status {
        case ._3, ._4: return .underWay
        case ._0, ._2:
            guard let start else { return nil }
            guard start > now else { return .startPassed }
            return WorkContract.releaseVersions.contains(version) ? .freeUntil(start) : nil
        default: return nil
        }
    }

    /// Before the take the two "once you take it" lines; after it, only what is still true.
    static func cancelText(_ line: CancelLine?, phase: CancelPhase, locale: Locale) -> String? {
        switch (line, phase) {
        case let (.freeUntil(start)?, _):
            L10n.WorkContract.cancelFreeUntil(OrdersFormat.shortDayTime(start, locale: locale))
        case (.underWay?, .beforeTake): L10n.WorkContract.cancelUnderWay
        case (.startPassed?, .beforeTake): L10n.WorkContract.cancelStartPassed
        case (.underWay?, .taken), (.startPassed?, .taken): L10n.WorkContract.doneCannotCancel
        case (nil, _): nil
        }
    }

    static func summaryVisible(_ request: WorkContractRequest, version: String) -> Bool {
        switch request {
        case .take, .accept: WorkContract.summaryVersions.contains(version)
        case .read: false
        }
    }

    // MARK: - Where and what

    /// The server writes "{Street}, {City}, {ZipCode}"; the street is everything before the last two parts.
    static func streetLine(_ address: String?) -> String? {
        guard let address, !address.isBlank else { return nil }
        let parts = address.components(separatedBy: ", ")
        guard parts.count >= 3 else { return address }
        return parts.dropLast(2).joined(separator: ", ")
    }

    static func areaLine(_ row: OrderListItem) -> String? {
        guard let area = row.customerAddressApproximate, !area.isBlank else { return nil }
        return area
    }

    /// A compact row's place: the street on Active; the area on History, whose street is redacted a day after
    /// completion.
    static func rowPlace(_ row: OrderListItem, pane: OrdersPane) -> String? {
        pane == .history ? areaLine(row) : streetLine(row.customerAddress)
    }

    private static let extraOrder = [
        "inside-oven", "inside-fridge", "interior-windows", "laundry-ironing", "pet-hair-supplement"
    ]

    /// A fixed order, never the wire's: the list row's extras arrive as an unordered map.
    static func orderedExtraSlugs(_ slugs: [String]) -> [String] {
        let known = extraOrder.filter(slugs.contains)
        let others = Set(slugs).subtracting(extraOrder).sorted()
        return known + others
    }

    static func extraSlugs(_ extras: [String: Bool]?) -> [String] {
        orderedExtraSlugs((extras ?? [:]).filter(\.value).map(\.key))
    }

    static func extrasLine(_ slugs: [String]) -> String? {
        let names = orderedExtraSlugs(slugs).map(shortExtraName)
        return names.isEmpty ? nil : "+ " + names.joined(separator: ", ")
    }

    static func shortExtraName(_ slug: String) -> String {
        switch slug {
        case "inside-oven": L10n.Orders.extraShortOven
        case "inside-fridge": L10n.Orders.extraShortFridge
        case "interior-windows": L10n.Orders.extraShortInteriorWindows
        case "laundry-ironing": L10n.Orders.extraShortIroning
        default: OrderExtras.name(slug)
        }
    }

    static func leadName(_ row: OrderListItem, locale: Locale) -> String? {
        let lang = locale.language.languageCode?.identifier
        if let package = row.selectedPackages?.first,
           let name = ScopeTranslation.resolveTranslatedName(package.translations, lang: lang, fallback: package.name),
           !name.isBlank
        {
            return name
        }
        guard let service = row.selectedServices?.first else { return nil }
        let name = ScopeTranslation.resolveTranslatedName(service.translations, lang: lang, fallback: service.name)
        return name?.isBlank == false ? name : nil
    }

    static func scopeLine(_ row: OrderListItem, locale: Locale) -> String? {
        let parts = scopeParts(row, locale: locale)
        return parts.isEmpty ? nil : parts.joined(separator: dot)
    }

    /// The scope's items for the separator flow: the package or service, rooms, baths, the extras.
    static func scopeParts(_ row: OrderListItem, locale: Locale) -> [String] {
        [
            leadName(row, locale: locale),
            (row.rooms ?? 0) > 0 ? L10n.Orders.rooms(row.rooms ?? 0) : nil,
            (row.bathrooms ?? 0) > 0 ? L10n.Orders.baths(row.bathrooms ?? 0) : nil,
            extrasLine(extraSlugs(row.extras))
        ].compactMap { $0 }
    }

    static func durationText(_ minutes: Int) -> String {
        let minutes = max(minutes, 0)
        if minutes < 60 { return L10n.Orders.durationMin(minutes) }
        if minutes % 60 == 0 { return L10n.Orders.durationH(minutes / 60) }
        return L10n.Orders.durationHMin(minutes / 60, minutes % 60)
    }

    /// "3–6 km from home"; nothing when the server sent no band (an admin, no home, no coordinates yet).
    static func bandText(_ band: HomeDistanceBand?) -> String? {
        band.map(L10n.Orders.distanceBand)
    }

    // MARK: - Team and money

    /// Seats, never names: `assignedEmployees` holds seat ids, so the count is the only honest figure.
    static func teamFlag(_ row: OrderListItem) -> String? {
        teamWords(required: row.requiredEmployees ?? 0, taken: row.assignedEmployeesCount ?? 0)
    }

    /// "2 cleaners needed · 1 already on it": the board's row and the detail of a job not taken.
    static func teamWords(required: Int, taken: Int) -> String? {
        guard required > 1 else { return nil }
        let filled = taken > 0 ? L10n.Orders.crewTaken(taken) : L10n.Orders.crewNobody
        return "\(L10n.Orders.crewSize(required)) · \(filled)"
    }

    /// The cleaner's own taken job is never "needed": it says the team's size.
    static func heroTeam(_ row: OrderListItem) -> String? {
        let required = row.requiredEmployees ?? 0
        return required > 1 ? L10n.Orders.heroTeam(required) : nil
    }

    static func isCash(_ row: OrderListItem) -> Bool {
        row.paymentType?.value == 1
    }

    static func pay(_ row: OrderListItem) -> String? {
        row.estimatedCleanerPay.map { OrdersFormat.money($0, symbol: OrdersFormat.symbolOf(row.currency)) }
    }

    static func cashAmount(_ row: OrderListItem) -> String? {
        guard isCash(row), let due = row.amountDueOnCard else { return nil }
        return nonBreaking(OrdersFormat.money(due, symbol: OrdersFormat.symbolOf(row.currency)))
    }

    static func flags(_ row: OrderListItem, clash: OrderListItem?, now: Date, locale: Locale) -> [Flag] {
        var flags: [Flag] = []
        if let clash, let window = window(of: clash, locale: locale) {
            flags.append(.overlap(L10n.Orders.flagOverlap(window)))
        }
        if case let .takeUntil(until)? = liveFlag(row, now: now) {
            flags.append(.takeUntil(L10n.Orders.flagTakeUntil(OrdersFormat.timeOnly(until, locale: locale))))
        }
        if let cash = cashFlag(row) { flags.append(.cash(cash)) }
        if let team = teamFlag(row) { flags.append(.team(team)) }
        if row.dirtinessLevel == ._1 || row.dirtinessLevel == ._2,
           let dirt = L10n.Orders.dirtinessLevel(row.dirtinessLevel)
        {
            flags.append(.dirt(dirt))
        }
        return flags
    }

    /// An amount inside a sentence keeps its number and its currency on one line.
    static func nonBreaking(_ amount: String) -> String {
        amount.replacingOccurrences(of: " ", with: "\u{00A0}")
    }

    /// The period's rewards per currency and its booked hours: whole windows, never divided by the crew. An empty
    /// period has no row to name its currency, so it takes the one the board knows ("0 Kč").
    static func historyTotals(_ rows: [OrderListItem], emptySymbol: String? = nil) -> (pay: String, hours: Int) {
        let minutes = rows.reduce(0) { $0 + max($1.estimatedTime ?? 0, 0) }
        let pay = rows.isEmpty ? OrdersFormat.money(0, symbol: emptySymbol) : OrdersFormat.totalEarnings(rows)
        return (pay, Int((Double(minutes) / 60).rounded()))
    }

    // MARK: - Lists

    static func badge(_ count: Int?) -> String? {
        guard let count, count > 0 else { return nil }
        return count > 99 ? "99+" : "\(count)"
    }

    /// The server sorts pay within a page only, so the local pay sort is honest only over every row.
    static func bestPayOffered(loaded: Int, total: Int?) -> Bool {
        guard let total else { return false }
        return loaded >= total
    }

    static func sortedByPay(_ rows: [OrderListItem]) -> [OrderListItem] {
        rows.sorted { lhs, rhs in
            switch (lhs.estimatedCleanerPay, rhs.estimatedCleanerPay) {
            case let (left?, right?) where left != right: left > right
            case (.some, nil): true
            case (nil, .some): false
            default: (lhs.cleaningDateTime ?? .distantFuture) < (rhs.cleaningDateTime ?? .distantFuture)
            }
        }
    }

    /// A page appended to the rows already held, each id once.
    static func appendPage(_ existing: [OrderListItem], _ page: [OrderListItem]) -> [OrderListItem] {
        var seen = Set(existing.compactMap(\.id))
        return existing + page.filter { row in
            guard let id = row.id else { return true }
            return seen.insert(id).inserted
        }
    }

    /// Paging to completion stops at the total or on an empty page, whichever comes first.
    static func pagesComplete(loaded: Int, total: Int?, lastPageCount: Int) -> Bool {
        lastPageCount == 0 || loaded >= (total ?? 0)
    }

    /// Live reservations in deadline order, each with its board row when one is loaded.
    static func reservedCards(
        _ offers: [PendingOfferItem],
        rows: [OrderListItem],
        now: Date
    ) -> [(offer: PendingOfferItem, row: OrderListItem?)] {
        offers
            .filter { ($0.respondByUtc ?? .distantPast) > now }
            .sorted { ($0.respondByUtc ?? .distantPast) < ($1.respondByUtc ?? .distantPast) }
            .map { offer in (offer, rows.first { $0.id != nil && $0.id == offer.id }) }
    }

    /// "10:14" when the last success was today; "Fri 30 Oct, 10:14" when it was not.
    static func staleWhen(_ last: Date, now: Date, locale: Locale, calendar: Calendar = .current) -> String {
        calendar.isDate(last, inSameDayAs: now)
            ? OrdersFormat.timeOnly(last, locale: locale)
            : OrdersFormat.shortDayTime(last, locale: locale)
    }
}

// MARK: - The working day

extension OrdersBoardRules {
    /// Where an own job is in its run: the step the status line names.
    enum RunStep: Equatable {
        case setOff
        case getIn
        case clean
        case finish
        case done
        case cancelled
    }

    /// The cash she holds: the amount, the limit when there is one, the bar's share, and whether she is over it.
    struct CashPanel: Equatable {
        let held: String
        let limit: String?
        let ratio: Double
        let over: Bool
    }

    /// What the status line is built from, on the Active hero and the detail head alike.
    struct RunFacts: Equatable {
        var status: OrderStatus?
        var start: Date
        var end: Date
        var crew = false
        var ownMark: Date?
        var hasAfterPhoto = false
        var lockoutReportedAt: Date?
        var finishedAt: Date?
        var cancelledAt: Date?
        var cancellationReason: String?
    }

    /// Cleaning closes at the first after photo on a solo job, and at her own mark on a crew job: a crew-mate's
    /// mark never closes hers.
    static func runStep(_ facts: RunFacts) -> RunStep? {
        switch facts.status {
        case ._2: .setOff
        case ._3: .getIn
        case ._4: (facts.crew ? facts.ownMark != nil : facts.hasAfterPhoto) ? .finish : .clean
        case ._5: .done
        case ._6: .cancelled
        default: nil
        }
    }

    /// `<step> · <time>`, the one status line of the Active hero and the detail head; ink once the job is closed.
    static func runStatus(
        _ facts: RunFacts,
        now: Date,
        locale: Locale,
        calendar: Calendar = .current
    ) -> (items: [String], isInk: Bool)? {
        let time = { OrdersFormat.timeOnly($0, locale: locale) }
        let until = { (end: Date) in durationText(Int((end.timeIntervalSince(now) / 60).rounded(.up))) }
        switch runStep(facts) {
        case .setOff?:
            return (setOffLine(facts, now: now, calendar: calendar, time: time, until: until), false)
        case .getIn?:
            if let reported = facts.lockoutReportedAt {
                return ([L10n.Orders.waitingCompany, L10n.Orders.reportedAt(time(reported))], false)
            }
            if now < facts
                .start { return ([L10n.Orders.statusLabel(._3), L10n.Orders.heroIn(until(facts.start))], false) }
            return ([L10n.Orders.runStepGetIn, L10n.Orders.heroDue(time(facts.start))], false)
        case let step? where step == .clean || step == .finish:
            let name = step == .clean ? L10n.Orders.runStepClean : L10n.Orders.runStepFinish
            let left = now < facts.end ? L10n.Orders.left(until(facts.end)) : L10n.Orders.heroUntil(time(facts.end))
            return ([name, left], false)
        case .done?:
            guard let finished = facts.finishedAt else { return ([L10n.Orders.trackerHeadlineDone], true) }
            let past = now.timeIntervalSince(finished) >= 24 * 3600
            let when = past ? OrdersFormat.shortDayTime(finished, locale: locale) : time(finished)
            return ([L10n.Orders.trackerHeadlineDone, L10n.Orders.finishedAt(when)], true)
        case .cancelled?:
            let head = facts.cancelledAt.map { L10n.Orders.cancelledAt(time($0)) } ?? L10n.Orders.statusLabel(._6)
            return ([head] + [L10n.Orders.cancellationReason(facts.cancellationReason)].compactMap { $0 }, true)
        default:
            return nil
        }
    }

    private static func setOffLine(
        _ facts: RunFacts,
        now: Date,
        calendar: Calendar,
        time: (Date) -> String,
        until: (Date) -> String
    ) -> [String] {
        let days = calendar.dateComponents(
            [.day],
            from: calendar.startOfDay(for: now),
            to: calendar.startOfDay(for: facts.start)
        ).day ?? 0
        let opensAt = facts.start.addingTimeInterval(-TimeInterval(startGraceWindowMinutes) * 60)
        let when = if days > 0 {
            days == 1 ? L10n.Orders.dayTomorrow : L10n.Orders.inDays(days)
        } else if now < opensAt {
            L10n.Orders.heroIn(until(opensAt))
        } else if now < facts.start {
            L10n.Orders.setOffNow
        } else {
            L10n.Orders.heroDue(time(facts.start))
        }
        return [L10n.Orders.runStepSetOff, when]
    }

    /// The facts of one own job: once its detail is read, the detail's own, so the hero names the step the detail
    /// head names; until then, or when that read failed, its row and its photos.
    static func runFacts(
        _ row: OrderListItem,
        detail: OrderDetail? = nil,
        photos: [OrderPhoto] = [],
        ownEmployeeId: String? = nil
    ) -> RunFacts? {
        if let facts = detail?.runFacts(myEmployeeId: ownEmployeeId, photos: photos) { return facts }
        guard let start = row.cleaningDateTime, let end = bookedEnd(row),
              [._2, ._3, ._4, ._5, ._6].contains(row.status)
        else { return nil }
        return RunFacts(
            status: row.status,
            start: start,
            end: end,
            crew: (row.requiredEmployees ?? 0) > 1,
            hasAfterPhoto: photos.contains { $0.photoType == ._2 }
        )
    }

    /// "Cash: collect 1 240 Kč for the company", "Cash: collected …" once she marks it: the cash is never hers.
    static func cashFlag(_ row: OrderListItem) -> String? {
        let collected = row.paymentStatus?.value == PaymentStatusCode.paid.rawValue
        return cashAmount(row).map { L10n.Orders.cashFlag($0, collected: collected) }
    }

    /// What happened to a completed cash job's cash, by its payment: collected, or not paid with her reward
    /// whole; any other status says nothing.
    static func historyCash(_ row: OrderListItem) -> (items: [String], collected: Bool)? {
        guard let amount = cashAmount(row) else { return nil }
        switch row.paymentStatus?.value {
        case 2: return ([L10n.Orders.historyCashCollected(amount)], true)
        case 1: return ([L10n.Orders.historyCashNotPaid(amount), L10n.Orders.historyPaidInFull], false)
        default: return nil
        }
    }

    /// The cash she holds, from the currency whose cash jobs are hidden first: no balance, no panel; no cap, the
    /// amount alone. Over the cap the tick sits at the cap's share of the bar.
    static func cashPanel(
        _ cashHeld: [CashHeld],
        locale: Locale = .current
    ) -> CashPanel? {
        guard let row = cashHeld.first(where: \.cashJobsHidden) ?? cashHeld.first else { return nil }
        let symbol = EarningsFormat.currencySymbol(row.currencyCode)
        let held = nonBreaking(OrdersFormat.money(row.amount, symbol: symbol, locale: locale))
        guard let cap = row.floatCap, cap > 0 else { return CashPanel(held: held, limit: nil, ratio: 0, over: false) }
        let over = row.amount > cap
        let limit = nonBreaking(OrdersFormat.money(cap, symbol: symbol, locale: locale))
        return CashPanel(
            held: held,
            limit: limit,
            ratio: over ? cap / row.amount : max(row.amount, 0) / cap,
            over: over
        )
    }

    static func dayGroups(
        _ rows: [OrderListItem],
        calendar: Calendar = .current
    ) -> [(day: Date, rows: [OrderListItem])] {
        var groups: [(day: Date, rows: [OrderListItem])] = []
        for row in rows {
            guard let start = row.cleaningDateTime else { continue }
            let day = calendar.startOfDay(for: start)
            if let index = groups.firstIndex(where: { $0.day == day }) {
                groups[index].rows.append(row)
            } else {
                groups.append((day, [row]))
            }
        }
        return groups
    }

    static func clashSpan(_ row: OrderListItem, own: [OrderListItem]) -> (start: Date, end: Date)? {
        guard let start = row.cleaningDateTime, let end = bookedEnd(row), let mine = clashes(row, own: own),
              let mineStart = mine.cleaningDateTime, let mineEnd = bookedEnd(mine)
        else { return nil }
        return (max(start, mineStart), min(end, mineEnd))
    }

    /// "Tomorrow: 1 job overlaps your 15:00–18:00 job" when the clashes fall on one day; the count otherwise.
    static func overlapFinding(
        _ rows: [OrderListItem],
        own: [OrderListItem],
        now: Date,
        locale: Locale,
        calendar: Calendar = .current
    ) -> String? {
        let clashing = rows.compactMap { row in clashes(row, own: own).map { (row: row, mine: $0) } }
        guard let first = clashing.first, let start = first.row.cleaningDateTime else { return nil }
        let days = Set(clashing.compactMap { $0.row.cleaningDateTime.map(calendar.startOfDay) })
        guard days.count == 1, let mine = window(of: first.mine, locale: locale) else {
            return L10n.Orders.overlapCount(clashing.count)
        }
        let day = OrdersAccessibility.dayWord(start, now: now, locale: locale, calendar: calendar)
        return L10n.Orders.overlapDay(clashing.count, day: day, window: mine)
    }

    /// "your job 15:00–18:00", "your jobs 08:00–10:00, 14:00–16:00", or the short form "yours …".
    static func dayHeaderLabel(_ mine: [OrderListItem], short: Bool, locale: Locale) -> String? {
        let windows = mine
            .sorted { ($0.cleaningDateTime ?? .distantFuture) < ($1.cleaningDateTime ?? .distantFuture) }
            .compactMap { window(of: $0, locale: locale) }
        guard !windows.isEmpty else { return nil }
        let joined = windows.joined(separator: ", ")
        return short
            ? L10n.Orders.dayYoursShort(joined, count: windows.count)
            : L10n.Orders.dayYourJob(joined, count: windows.count)
    }
}
