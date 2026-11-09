import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
import UIKit

// MARK: - Available

/// The stale strip, the reserved jobs, the finding and the order, then one panel a day with its leaf, the day line
/// and its jobs, then the page footer. An empty or failed board fills with her next jobs and the cash she holds.
struct AvailablePane: View {
    @ObservedObject var vm: OrdersListViewModel
    @Environment(\.locale) private var locale
    @Environment(\.dynamicTypeSize) private var typeSize
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @AccessibilityFocusState private var focusedCard: String?
    @State private var width: CGFloat = 0
    @State private var tileWidth: CGFloat = 108

    var body: some View {
        content
            .padding(.horizontal, Spacing.m)
            .padding(.top, Spacing.xs)
            .padding(.bottom, Spacing.l)
            .background(GeometryReader { space in
                Color.clear
                    .onAppear { width = space.size.width }
                    .onChange(of: space.size.width) { width = $0 }
            })
            .environment(\.ordersTileWidth, tileWidth)
            .onPreferenceChange(OrdersTileNeedKey.self) { tileWidth = OrdersTileNeedKey.column(tileWidth, after: $0) }
            .ordersScroll(onRefresh: vm.userRefresh)
    }

    @ViewBuilder
    private var content: some View {
        switch vm.state(of: .available) {
        case .loading:
            OrdersBoardSkeleton()
        case .error:
            OrdersShortBoard(vm: vm, failed: true)
        case .loaded:
            if vm.reserved.isEmpty, vm.availableCards.isEmpty {
                OrdersShortBoard(vm: vm, failed: false)
            } else {
                populated
            }
        }
    }

    private var populated: some View {
        let now = vm.now()
        let reserved = vm.reserved
        let cards = vm.availableCards
        let groups = OrdersBoardRules.dayGroups(cards)
        let range = pageRange(cards)
        let short = headerForm(groups.map(\.day))
        return LazyVStack(alignment: .leading, spacing: Spacing.s) {
            if let stale = staleText {
                OrdersStaleStrip(text: stale) { Task { await vm.userRefresh() } }
            }
            if !reserved.isEmpty {
                // Built as one block, so every pay tile reports its need before the column is first drawn.
                VStack(alignment: .leading, spacing: Spacing.s) {
                    ForEach(reserved, id: \.offer.id) { pair in
                        AvailableJobCard(
                            row: pair.row,
                            now: now,
                            clash: vm.clash(for: pair.row),
                            isTaking: vm.boardTake?.snapshot.id == pair.row.id,
                            reservedUntil: OfferLabels.reservedUntil(pair.offer.respondByUtc, now: now),
                            onOpen: { open(pair.row) },
                            onTake: { vm.take(pair.row) },
                            onDecline: { vm.askToDecline(pair.offer) }
                        )
                        .accessibilityFocused($focusedCard, equals: pair.row.id)
                    }
                }
            }
            OrdersToolbar(vm: vm)
            if let cash = vm.cashPanel, cash.over, let limit = cash.limit {
                OrdersCashNotice(held: cash.held, limit: limit, ratio: cash.ratio)
            }
            ForEach(groups, id: \.day) { group in
                dayPanel(group, range: range, short: short, now: now)
            }
            footer
        }
        .animation(reduceMotion ? nil : .easeOut(duration: 0.2), value: reserved.map(\.row.id) + cards.map(\.id))
        .onChange(of: vm.firstAppendedId) { focusedCard = $0 }
        .onChange(of: vm.contractRequest == nil) { closed in
            if closed, let next = vm.focusAfterTake { focusedCard = next }
        }
    }

    private var staleText: String? {
        let state = vm.pane(.available)
        guard state.error != nil, let loadedAt = state.loadedAt else { return nil }
        return L10n.Orders.staleSince(OrdersBoardRules.staleWhen(loadedAt, now: vm.now(), locale: locale))
    }

    private func open(_ row: OrderListItem) {
        guard let id = row.id else { return }
        vm.openDetail(id)
    }

    // MARK: Day panels

    /// One range for every day line of the page, so the days compare on one scale.
    private func pageRange(_ cards: [OrderListItem]) -> (from: Int, to: Int) {
        let rows = cards + vm.reserved.map(\.row) + vm.ownJobsForClash
        return OrdersBoardRules.dayRange(rows.compactMap(span))
    }

    private func span(_ row: OrderListItem) -> (start: Int, end: Int)? {
        guard let start = row.cleaningDateTime, let end = OrdersBoardRules.bookedEnd(row) else { return nil }
        return OrdersBoardRules.minuteSpan(start: start, end: end)
    }

    private func mine(on day: Date) -> [OrderListItem] {
        vm.ownJobsForClash.filter { $0.cleaningDateTime.map { Calendar.current.isDate($0, inSameDayAs: day) } ?? false }
    }

    /// Decided once for the page: every header takes the short form if any long label would not fit beside its
    /// day name, and every one wraps under its name if a short one would not either. The room is the panel's text
    /// column beside the leaf, less the gap and the label's dot.
    private func headerForm(_ days: [Date]) -> (short: Bool, wrap: Bool) {
        guard !typeSize.isAccessibilitySize, width > 0 else { return (false, true) }
        let room = width - 4 * Spacing.m - 48 - Spacing.s - Spacing.xs - 8 - 6
        let fits = { (short: Bool) in
            days.allSatisfy { day in
                guard let label = OrdersBoardRules.dayHeaderLabel(mine(on: day), short: short, locale: locale)
                else { return true }
                let name = OrdersAccessibility.dayWord(day, now: vm.now(), locale: locale)
                return measure(name, CleansiaTypography.scale.titleMedium)
                    + measure(label, CleansiaTypography.scale.bodyMedium) <= room
            }
        }
        if fits(false) { return (false, false) }
        return (true, !fits(true))
    }

    private func measure(_ text: String, _ style: CleansiaTextStyle) -> CGFloat {
        let traits = UITraitCollection(preferredContentSizeCategory: UIContentSizeCategory(typeSize))
        let size = UIFontMetrics.default.scaledValue(for: style.size, compatibleWith: traits).rounded()
        let font = CleansiaFont.uiFont(style.family, weight: style.weight, size: size)
        return (text as NSString).size(withAttributes: [.font: font]).width
    }

    private func dayPanel(
        _ group: (day: Date, rows: [OrderListItem]),
        range: (from: Int, to: Int),
        short: (short: Bool, wrap: Bool),
        now: Date
    ) -> some View {
        let own = mine(on: group.day)
        let slots = group.rows + vm.reserved.map(\.row).filter {
            $0.cleaningDateTime.map { Calendar.current.isDate($0, inSameDayAs: group.day) } ?? false
        }
        return VStack(spacing: 0) {
            HStack(alignment: typeSize.isAccessibilitySize ? .top : .center, spacing: Spacing.s) {
                OrdersDateTile(date: group.day, size: .header, now: now)
                VStack(alignment: .leading, spacing: 6) {
                    dayTitle(group.day, own: own, short: short)
                    OrdersDayLine(
                        range: range,
                        mine: own.compactMap(span),
                        open: slots.compactMap(span),
                        clashes: slots.compactMap { OrdersBoardRules.clashSpan($0, own: own) }
                            .map { OrdersBoardRules.minuteSpan(start: $0.start, end: $0.end) },
                        now: Calendar.current.isDate(group.day, inSameDayAs: now)
                            ? OrdersBoardRules.minuteSpan(start: now, end: now).start : nil
                    )
                }
            }
            .padding(.horizontal, Spacing.m)
            .padding(.vertical, Spacing.s)
            ForEach(group.rows, id: \.id) { row in
                Divider().overlay(CleansiaColors.outlineVariant)
                AvailableJobCard(row: row, now: now, clash: vm.clash(for: row), onOpen: { open(row) })
                    .accessibilityFocused($focusedCard, equals: row.id)
            }
        }
        .ordersCard()
    }

    private func dayTitle(_ day: Date, own: [OrderListItem], short: (short: Bool, wrap: Bool)) -> some View {
        let name = Text(OrdersAccessibility.dayWord(day, now: vm.now(), locale: locale))
            .font(CleansiaTypography.titleMedium)
            .foregroundColor(CleansiaColors.onSurface)
            .accessibilityAddTraits(.isHeader)
        let label = OrdersBoardRules.dayHeaderLabel(own, short: short.short, locale: locale)
        let mark = label.map { text in
            HStack(alignment: .firstTextBaseline, spacing: 6) {
                Circle()
                    .fill(CleansiaColors.primary)
                    .frame(width: 8, height: 8)
                    .xHeightAligned(of: CleansiaTypography.scale.bodyMedium)
                Text(OrdersBoardRules.unbreakable(text))
                    .font(CleansiaTypography.bodyMedium)
                    .monospacedDigit()
                    .foregroundColor(CleansiaColors.primaryText)
                    .fixedSize(horizontal: !short.wrap, vertical: true)
            }
        }
        let layout = short.wrap ? AnyLayout(VStackLayout(alignment: .leading, spacing: Spacing.hair))
            : AnyLayout(HStackLayout(alignment: .firstTextBaseline, spacing: Spacing.xs))
        return layout {
            name
            if !short.wrap { Spacer(minLength: 0) }
            mark
        }
        .accessibilityElement(children: .combine)
        .accessibilityAddTraits(.isHeader)
    }

    // MARK: Footer: the page, and the next one

    @ViewBuilder
    private var footer: some View {
        let state = vm.pane(.available)
        if let total = state.total, state.rows.count < total {
            VStack(spacing: Spacing.xxs) {
                if state.pagingFailed {
                    Text(L10n.Orders.loadMoreFailed)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    CleansiaTextLink(L10n.retry) { Task { await vm.loadMore() } }
                } else {
                    Text(L10n.Orders.showingOf(state.rows.count, total))
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    if state.isPaging {
                        ProgressView().tint(CleansiaColors.primaryText).frame(minHeight: 48)
                    } else {
                        CleansiaOutlinedButton(
                            L10n.Orders.loadMore,
                            size: .medium,
                            contentColor: CleansiaColors.primaryText,
                            borderColor: CleansiaColors.primary
                        ) { Task { await vm.loadMore() } }
                    }
                }
            }
            .multilineTextAlignment(.center)
            .frame(maxWidth: .infinity)
            .padding(.top, Spacing.xxs)
        }
        if (vm.availableCards + vm.reserved.map(\.row)).contains(where: { $0.homeDistanceBand != nil }) {
            Text(L10n.tie(L10n.Orders.distanceFootnote))
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .fixedSize(horizontal: false, vertical: true)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
    }
}

/// One finding and the order: cash hidden in amber, else the overlap in words beside a red mark, else nothing;
/// the sort a menu once every row is loaded. When the finding needs two lines the sort shrinks to its glyph, on the
/// finding's first line.
struct OrdersToolbar: View {
    @ObservedObject var vm: OrdersListViewModel

    var body: some View {
        ViewThatFits(in: .horizontal) {
            HStack(alignment: .firstTextBaseline, spacing: Spacing.s) {
                finding.fixedSize()
                Spacer(minLength: 0)
                sort(compact: false)
            }
            HStack(alignment: .firstTextBaseline, spacing: Spacing.s) {
                finding
                Spacer(minLength: 0)
                sort(compact: true)
            }
        }
        .frame(minHeight: 24)
        .padding(.vertical, Spacing.xxs)
    }

    @ViewBuilder
    private var finding: some View {
        if vm.cashPanel?.over == true {
            label(icon: "banknote", text: L10n.Orders.cashHidden, iconInk: CleansiaColors.pendingInk)
                .foregroundColor(CleansiaColors.pendingInk)
        } else if let overlap = vm.overlapFinding {
            label(icon: "exclamationmark.circle", text: overlap, iconInk: CleansiaColors.error)
                .foregroundColor(CleansiaColors.onSurface)
        }
    }

    private func label(icon: String, text: String, iconInk: Color) -> some View {
        HStack(alignment: .firstTextBaseline, spacing: Spacing.xs) {
            Image(systemName: icon)
                .font(CleansiaTypography.titleLarge)
                .foregroundColor(iconInk)
                .capBandAligned(of: CleansiaTypography.scale.bodyMedium)
                .accessibilityHidden(true)
            Text(L10n.tie(OrdersBoardRules.unbreakable(text)))
                .font(CleansiaTypography.bodyMedium)
                .fixedSize(horizontal: false, vertical: true)
        }
    }

    /// One option is plain text; the menu appears only once every row is loaded and pay can be sorted honestly.
    @ViewBuilder
    private func sort(compact: Bool) -> some View {
        let current = OrdersFormat.sortLabel(vm.shownSort)
        let face = HStack(alignment: .firstTextBaseline, spacing: Spacing.hair) {
            if compact {
                Image(systemName: "arrow.up.arrow.down").font(CleansiaTypography.titleLarge)
            } else {
                Text(current).font(CleansiaTypography.labelLarge)
                if vm.bestPayOffered {
                    Image(systemName: "chevron.down")
                        .font(CleansiaTypography.labelLarge)
                        .imageScale(.small)
                        .capBandAligned(of: CleansiaTypography.scale.labelLarge)
                }
            }
        }
        .lineLimit(1)
        .fixedSize()
        .frame(minWidth: compact ? 44 : nil, minHeight: 44)
        Group {
            if vm.bestPayOffered {
                Menu {
                    ForEach(AvailableSort.allCases, id: \.self) { option in
                        Button(OrdersFormat.sortLabel(option)) { vm.setAvailableSort(option) }
                    }
                } label: {
                    face.foregroundColor(CleansiaColors.primaryText)
                }
                .accessibilityLabel(L10n.Orders.sortA11y(current))
            } else {
                face
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .accessibilityElement(children: .ignore)
                    .accessibilityLabel(L10n.Orders.sortA11y(current))
            }
        }
        .capBandAligned(of: CleansiaTypography.scale.bodyMedium)
        .padding(.vertical, -12)
    }
}

// MARK: - Short boards: empty, failed, loading

/// No open job, or the board failed: what happened and Check again (or Retry) beside the mascot, then what the
/// other calls still have: her next jobs and the cash she holds.
struct OrdersShortBoard: View {
    @ObservedObject var vm: OrdersListViewModel
    let failed: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            OrdersShortPanel(vm: vm, failed: failed)
            let cash = vm.cashPanel
            if let cash, cash.over, let limit = cash.limit {
                OrdersCashNotice(held: cash.held, limit: limit, ratio: cash.ratio)
            }
            OrdersNextJobs(vm: vm)
            if let cash, !cash.over {
                OrdersCashPanel(held: cash.held, limit: cash.limit, ratio: cash.ratio)
            }
        }
    }
}

/// What happened and Check again (or Retry) beside the mascot.
struct OrdersShortPanel: View {
    @ObservedObject var vm: OrdersListViewModel
    @Environment(\.locale) private var locale
    @Environment(\.dynamicTypeSize) private var typeSize
    let failed: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            let layout = typeSize.isAccessibilitySize ? AnyLayout(VStackLayout(alignment: .leading, spacing: 12))
                : AnyLayout(HStackLayout(alignment: .center, spacing: 12))
            layout {
                VStack(alignment: .leading, spacing: Spacing.xxs) {
                    Text(failed ? L10n.Orders.loadError : L10n.Orders.emptyTitle)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                        .fixedSize(horizontal: false, vertical: true)
                        .accessibilityAddTraits(.isHeader)
                    Text(L10n.tie(subtitle))
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .fixedSize(horizontal: false, vertical: true)
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                (failed ? Mascot.resting : Mascot.leaning).image
                    .resizable()
                    .scaledToFit()
                    .frame(width: 112, height: 112)
                    .accessibilityHidden(true)
            }
            CleansiaOutlinedButton(
                failed ? L10n.retry : L10n.Orders.checkAgain,
                size: .medium,
                leadingIcon: "arrow.clockwise",
                contentColor: CleansiaColors.primaryText,
                borderColor: CleansiaColors.primary
            ) { Task { await vm.userRefresh() } }
        }
        .padding(Spacing.m)
        .ordersCard()
        .onAppear { if failed { OrdersAccessibility.layoutChanged(L10n.Orders.loadError) } }
    }

    private var subtitle: String {
        guard !failed, let checked = vm.pane(.available).loadedAt else { return L10n.Orders.emptySubtitle }
        return "\(L10n.Orders.checkedAt(OrdersFormat.timeOnly(checked, locale: locale))). \(L10n.Orders.emptySubtitle)"
    }
}

/// "Your next jobs · 3 jobs" and her own rows, when the Active call has them.
struct OrdersNextJobs: View {
    @ObservedObject var vm: OrdersListViewModel

    var body: some View {
        let rows = vm.nextJobs
        if !rows.isEmpty {
            OrdersSectionHeader(title: L10n.Orders.yourNextJobs, trailing: L10n.Orders.jobCount(rows.count))
            OrdersRowList(rows: rows, now: vm.now(), showsFlags: false) { row in
                if let id = row.id { vm.openDetail(id) }
            }
        }
    }
}

/// The board while it loads: the final layout as flat bars at the real size of each value, the labels at once.
struct OrdersBoardSkeleton: View {
    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            HStack {
                OrdersBar(width: 170, height: 16, fill: CleansiaColors.outline)
                Spacer(minLength: 0)
                Text(L10n.Orders.sortSoonestFirst)
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            .frame(minHeight: 24)
            .padding(.vertical, Spacing.xxs)
            day
            day
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(L10n.WorkContract.loading)
    }

    private var day: some View {
        VStack(spacing: 0) {
            HStack(spacing: Spacing.s) {
                RoundedRectangle(cornerRadius: CornerRadius.small).fill(CleansiaColors.surfaceVariant)
                    .frame(width: 48, height: 48)
                VStack(alignment: .leading, spacing: Spacing.s) {
                    OrdersBar(width: 90, height: 20)
                    Capsule().fill(CleansiaColors.surfaceVariant).frame(height: 10)
                }
            }
            .padding(.horizontal, Spacing.m)
            .padding(.vertical, Spacing.s)
            ForEach([150, 170], id: \.self) { width in
                Divider().overlay(CleansiaColors.outlineVariant)
                VStack(alignment: .leading, spacing: 6) {
                    HStack(alignment: .top, spacing: Spacing.s) {
                        OrdersBar(width: 116, height: 22)
                        Spacer(minLength: 0)
                        OrdersBar(width: 64, height: 20)
                    }
                    OrdersBar(width: CGFloat(width), height: 16)
                    OrdersBar(width: CGFloat(340 - width), height: 14).padding(.top, Spacing.xxs)
                }
                .padding(.leading, Spacing.m)
                .padding(.trailing, AvailableJobCard.chevronColumn + Spacing.s)
                .padding(.top, Spacing.s)
                .padding(.bottom, Spacing.m)
                .frame(maxWidth: .infinity, alignment: .leading)
                .overlay(alignment: .trailing) { ChevronEdge(showsChevron: false) }
            }
        }
        .ordersCard()
    }
}

// MARK: - Her own rows: Later, her next jobs

/// The card of compact rows her own jobs share on Active and under an empty or failed tab.
struct OrdersRowList: View {
    let rows: [OrderListItem]
    let now: Date
    var showsFlags = true
    let onOpen: (OrderListItem) -> Void

    var body: some View {
        VStack(spacing: 0) {
            ForEach(Array(rows.enumerated()), id: \.offset) { index, row in
                if index > 0 { Divider().overlay(CleansiaColors.outlineVariant) }
                OrdersJobRow(row: row, now: now, showsFlags: showsFlags) { onOpen(row) }
            }
        }
        .ordersCard()
    }
}

/// One of her own jobs: the leaf with its weekday, the window, the street and what, the team and the cash, the
/// pay. At accessibility sizes the pay leaves the right edge so the column keeps its words whole.
struct OrdersJobRow: View {
    @Environment(\.locale) private var locale
    @Environment(\.dynamicTypeSize) private var typeSize
    let row: OrderListItem
    let now: Date
    var showsFlags = true
    let onOpen: () -> Void

    var body: some View {
        Button(action: onOpen) {
            HStack(alignment: .top, spacing: Spacing.s) {
                if let start = row.cleaningDateTime {
                    OrdersDateTile(date: start, size: .row, now: now)
                }
                VStack(alignment: .leading, spacing: Spacing.hair) {
                    Text(OrdersBoardRules.window(of: row, locale: locale) ?? "")
                        .font(CleansiaTypography.bodyLarge)
                        .monospacedDigit()
                        .foregroundColor(CleansiaColors.onSurface)
                    OrdersDotFlow(
                        items: [
                            OrdersBoardRules.streetLine(row.customerAddress),
                            OrdersBoardRules.leadName(row, locale: locale)
                        ].compactMap { $0 }.map { Text(L10n.tie($0)) }
                    )
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    if showsFlags { flags }
                    if typeSize.isAccessibilitySize { pay.padding(.top, 6) }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                if !typeSize.isAccessibilitySize { pay }
            }
            .padding(.horizontal, Spacing.m)
            .padding(.vertical, Spacing.s)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(OrdersAccessibility.rowSentence(row, pane: .active, now: now, locale: locale))
        .accessibilityAddTraits(.isButton)
    }

    @ViewBuilder
    private var flags: some View {
        let team = OrdersBoardRules.heroTeam(row)
        let cash = OrdersBoardRules.cashFlag(row)
        if team != nil || cash != nil {
            ChipFlow(spacing: 6, wrapsWideItems: true) {
                if let team { TintedFlag(icon: "person.2", text: team) }
                if let cash { TintedFlag(icon: "banknote", text: cash, tone: .amber) }
            }
            .padding(.top, 6)
        }
    }

    @ViewBuilder
    private var pay: some View {
        if let pay = OrdersBoardRules.pay(row) {
            VStack(alignment: typeSize.isAccessibilitySize ? .leading : .trailing, spacing: 0) {
                Text(pay)
                    .font(CleansiaTypography.titleMedium)
                    .monospacedDigit()
                    .foregroundColor(CleansiaColors.onSurface)
                    .lineLimit(1)
                    .fixedSize()
                if (row.requiredEmployees ?? 0) > 1 {
                    Text(L10n.Orders.yourShare)
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
        }
    }
}
