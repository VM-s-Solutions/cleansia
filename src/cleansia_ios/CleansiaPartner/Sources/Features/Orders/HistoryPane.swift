import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// One panel: the period chips, the rewards and how many jobs, the month as a grid of days, the way to payouts,
/// then the ledger day by day. An empty This month shows last month as a reference; a failed period is not a zero
/// month, so it shows no total and no grid, and under the panel what the other calls still have.
struct HistoryPane: View {
    @ObservedObject var vm: OrdersListViewModel
    @Environment(\.locale) private var locale
    @Environment(\.dynamicTypeSize) private var typeSize
    let onOpenEarnings: () -> Void

    var body: some View {
        let state = vm.state(of: .history)
        return VStack(alignment: .leading, spacing: Spacing.s) {
            if let stale = staleText {
                OrdersStaleStrip(text: stale) { Task { await vm.userRefresh() } }
            }
            panel(state)
            if case .error = state {
                OrdersNextJobs(vm: vm)
                if let cash = vm.cashPanel {
                    OrdersCashPanel(held: cash.held, limit: cash.limit, ratio: cash.ratio)
                }
            }
        }
        .padding(.horizontal, Spacing.m)
        .padding(.top, Spacing.xs)
        .padding(.bottom, Spacing.l)
        .ordersScroll(onRefresh: vm.userRefresh)
    }

    private var staleText: String? {
        let state = vm.pane(.history)
        guard state.error != nil, let loadedAt = state.loadedAt else { return nil }
        return L10n.Orders.staleSince(OrdersBoardRules.staleWhen(loadedAt, now: vm.now(), locale: locale))
    }

    private func panel(_ state: UiState<[OrderListItem]>) -> some View {
        VStack(alignment: .leading, spacing: 0) {
            chips
            switch state {
            case .error:
                failure
                payouts
            case .loading:
                summary(rows: nil)
                grid(rows: [])
                payouts
                ForEach([150, 120, 170], id: \.self) { width in
                    rule
                    loadingDay(width: CGFloat(width))
                }
            case let .loaded(rows):
                summary(rows: rows)
                grid(rows: rows)
                payouts
                if rows.isEmpty {
                    emptyPeriod
                    reference
                }
                ledger(rows)
            }
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .ordersCard()
    }

    // MARK: The period, the rewards, the month

    private var chips: some View {
        ChipFlow(spacing: Spacing.xs) {
            ForEach(CompletedPeriod.allCases, id: \.self) { period in
                CleansiaChip(
                    label: OrdersQueryBuilder.periodLabel(period, locale: locale, now: vm.now()),
                    isSelected: period == vm.completedPeriod
                ) { Task { await vm.setCompletedPeriod(period) } }
                    .frame(minHeight: 44)
            }
        }
        .padding(.vertical, -Spacing.xxs)
    }

    private func summary(rows: [OrderListItem]?) -> some View {
        VStack(alignment: .leading, spacing: Spacing.hair) {
            Text(L10n.Orders.rewardsTitle)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.primaryText)
                .accessibilityAddTraits(.isHeader)
            if let rows {
                let totals = OrdersBoardRules.historyTotals(rows, emptySymbol: vm.knownCurrencySymbol)
                let jobs = vm.pane(.history).total ?? rows.count
                HistoryTotal(amount: totals.pay)
                    .accessibilityLabel(Text(verbatim: "\(L10n.Orders.rewardsTitle), \(periodName): \(totals.pay)"))
                OrdersDotFlow(items: summaryItems(jobs: jobs, hours: totals.hours, rows: rows).map { Text($0) })
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            } else {
                OrdersBar(width: 150, height: 30).padding(.vertical, Spacing.hair)
                OrdersBar(width: 200, height: 16)
            }
        }
        .padding(.top, Spacing.m)
    }

    /// "9 jobs · about 23 h booked"; an empty period says "0 jobs" and no hours.
    private func summaryItems(jobs: Int, hours: Int, rows: [OrderListItem]) -> [String] {
        guard !rows.isEmpty else { return [L10n.Orders.jobCount(0)] }
        return L10n.Orders.historySummary(L10n.Orders.jobCount(jobs), L10n.Orders.durationH(hours))
            .components(separatedBy: " · ")
    }

    @ViewBuilder
    private func grid(rows: [OrderListItem]) -> some View {
        if let range = OrdersBoardRules.historyGridRange(vm.completedPeriod, now: vm.now()) {
            OrdersMonthGrid(cells: OrdersBoardRules.monthGrid(
                from: range.from,
                through: range.through,
                rows: rows,
                now: vm.now()
            ))
            .padding(.top, Spacing.m)
        }
    }

    private var payouts: some View {
        CleansiaTextLink(
            L10n.Orders.payoutsLink(L10n.Earnings.title),
            trailingIcon: "chevron.right",
            action: onOpenEarnings
        )
        .padding(.leading, -Spacing.xxs)
        .padding(.top, Spacing.xxs)
    }

    private var periodName: String {
        OrdersQueryBuilder.periodLabel(vm.completedPeriod, locale: locale, now: vm.now())
    }

    private var rule: some View {
        Divider().overlay(CleansiaColors.outlineVariant).padding(.horizontal, -Spacing.m)
    }

    // MARK: Empty, failed, loading

    private var failure: some View {
        HStack(alignment: .center, spacing: Spacing.s) {
            VStack(alignment: .leading, spacing: Spacing.xxs) {
                HStack(alignment: .firstTextBaseline, spacing: Spacing.xs) {
                    Image(systemName: "exclamationmark.circle")
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.error)
                        .capBandAligned(of: CleansiaTypography.scale.titleMedium)
                        .accessibilityHidden(true)
                    Text(L10n.Orders.loadError).font(CleansiaTypography.titleMedium)
                }
                .foregroundColor(CleansiaColors.onSurface)
                Text(L10n.tie(L10n.Orders.emptySubtitle))
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .fixedSize(horizontal: false, vertical: true)
                CleansiaOutlinedButton(
                    L10n.retry,
                    size: .medium,
                    leadingIcon: "arrow.clockwise",
                    contentColor: CleansiaColors.primaryText,
                    borderColor: CleansiaColors.primary
                ) { Task { await vm.userRefresh() } }
                    .fixedSize()
                    .padding(.top, Spacing.s)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            if !typeSize.isAccessibilitySize {
                Mascot.resting.image
                    .resizable()
                    .scaledToFit()
                    .frame(width: 120, height: 120)
                    .accessibilityHidden(true)
            }
        }
        .padding(.vertical, Spacing.s)
        .onAppear { OrdersAccessibility.layoutChanged(L10n.Orders.loadError) }
    }

    private var emptyPeriod: some View {
        VStack(alignment: .leading, spacing: 0) {
            rule
            HStack(alignment: .center, spacing: Spacing.s) {
                VStack(alignment: .leading, spacing: Spacing.hair) {
                    Text(vm.completedPeriod == .thisWeek ? L10n.Orders.historyEmptyWeek : L10n.Orders.historyEmptyMonth)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                        .fixedSize(horizontal: false, vertical: true)
                    Text(L10n.tie(L10n.Orders.historyEmptyBody))
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .fixedSize(horizontal: false, vertical: true)
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                if !typeSize.isAccessibilitySize {
                    Mascot.resting.image
                        .resizable()
                        .scaledToFit()
                        .frame(width: 88, height: 88)
                        .accessibilityHidden(true)
                }
            }
            .padding(.vertical, Spacing.s)
        }
    }

    @ViewBuilder
    private var reference: some View {
        if let rows = vm.referenceMonth, !rows.isEmpty,
           let range = OrdersBoardRules.historyGridRange(.lastMonth, now: vm.now())
        {
            let totals = OrdersBoardRules.historyTotals(rows)
            let month = OrdersQueryBuilder.periodLabel(.lastMonth, locale: locale, now: vm.now())
            rule
            VStack(alignment: .leading, spacing: Spacing.hair) {
                Text(L10n.Orders.historyLastMonth)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                HStack(alignment: .firstTextBaseline) {
                    Text(month).font(CleansiaTypography.titleMedium).foregroundColor(CleansiaColors.onSurface)
                    Spacer(minLength: Spacing.xs)
                    Text(totals.pay)
                        .font(CleansiaTypography.titleMedium)
                        .monospacedDigit()
                        .foregroundColor(CleansiaColors.onSurface)
                        .lineLimit(1)
                        .fixedSize()
                }
                OrdersDotFlow(items: summaryItems(jobs: rows.count, hours: totals.hours, rows: rows).map { Text($0) })
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                OrdersMonthGrid(cells: OrdersBoardRules.monthGrid(
                    from: range.from,
                    through: range.through,
                    rows: rows,
                    now: vm.now()
                ))
                .padding(.top, 10)
                CleansiaTextLink(
                    L10n.Orders.historyShowPeriod(lastMonthInSentence),
                    trailingIcon: "chevron.right"
                ) { Task { await vm.setCompletedPeriod(.lastMonth) } }
                    .padding(.leading, -Spacing.xxs)
            }
            .padding(.top, Spacing.s)
        }
    }

    /// The month as a sentence takes it: "Show October", "Zobrazit říjen".
    private var lastMonthInSentence: String {
        guard let start = OrdersQueryBuilder.periodToDateRange(.lastMonth, now: vm.now()).from else { return "" }
        return OrdersFormat.pattern("LLLL", start, locale: locale)
    }

    private func loadingDay(width: CGFloat) -> some View {
        HStack(alignment: .top, spacing: Spacing.s) {
            RoundedRectangle(cornerRadius: CornerRadius.small)
                .fill(CleansiaColors.surfaceVariant)
                .frame(width: 64, height: 64)
            VStack(alignment: .leading, spacing: 6) {
                HStack {
                    OrdersBar(width: 118, height: 22)
                    Spacer(minLength: 0)
                    OrdersBar(width: 56, height: 22)
                }
                OrdersBar(width: width, height: 14)
            }
        }
        .padding(.vertical, Spacing.s)
    }

    // MARK: The ledger

    private func ledger(_ rows: [OrderListItem]) -> some View {
        ForEach(OrdersBoardRules.dayGroups(rows), id: \.day) { group in
            rule
            HStack(alignment: .top, spacing: Spacing.s) {
                OrdersDateTile(date: group.day, size: .row, now: vm.now(), closed: true)
                VStack(alignment: .leading, spacing: Spacing.s) {
                    ForEach(group.rows, id: \.id) { row in
                        HistoryLedgerRow(row: row, now: vm.now()) {
                            if let id = row.id { vm.openDetail(id) }
                        }
                    }
                }
            }
            .padding(.vertical, Spacing.s)
        }
    }
}

/// The period's total: the page's one money hero, one string.
private struct HistoryTotal: View {
    let amount: String

    var body: some View {
        Text(amount)
            .cleansiaFont(CleansiaTypography.headlineMedium)
            .monospacedDigit()
            .foregroundColor(CleansiaColors.onSurface)
            .lineLimit(1)
            .minimumScaleFactor(0.6)
            .dynamicTypeSize(...DynamicTypeSize.xxxLarge)
    }
}

/// One completed job in the ledger: the window and the pay, where and what, and what happened to its cash: a
/// collected cash job in amber; an unpaid one in two parts on two lines, the fact and what it means for her pay.
private struct HistoryLedgerRow: View {
    @Environment(\.locale) private var locale
    let row: OrderListItem
    let now: Date
    let onOpen: () -> Void

    var body: some View {
        Button(action: onOpen) {
            VStack(alignment: .leading, spacing: Spacing.hair) {
                HStack(alignment: .firstTextBaseline, spacing: Spacing.xs) {
                    Text(OrdersBoardRules.window(of: row, locale: locale) ?? "")
                        .font(CleansiaTypography.bodyLarge)
                        .monospacedDigit()
                        .foregroundColor(CleansiaColors.onSurface)
                    Spacer(minLength: 0)
                    if let pay = OrdersBoardRules.pay(row) {
                        Text(pay)
                            .font(CleansiaTypography.titleMedium)
                            .monospacedDigit()
                            .foregroundColor(CleansiaColors.onSurface)
                            .lineLimit(1)
                            .fixedSize()
                    }
                }
                OrdersDotFlow(
                    items: [
                        OrdersBoardRules.areaLine(row),
                        OrdersBoardRules.leadName(row, locale: locale),
                        OrdersBoardRules.heroTeam(row)
                    ].compactMap { $0 }.map { Text($0) }
                )
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                if let cash = OrdersBoardRules.historyCash(row) {
                    cashFlag(cash).padding(.top, 6)
                }
            }
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(OrdersAccessibility.rowSentence(row, pane: .history, now: now, locale: locale))
        .accessibilityAddTraits(.isButton)
    }

    @ViewBuilder
    private func cashFlag(_ cash: (items: [String], collected: Bool)) -> some View {
        if cash.collected || cash.items.count < 2 {
            TintedFlag(icon: "banknote", text: cash.items.joined(separator: OrdersBoardRules.dot), tone: .amber)
        } else {
            HStack(alignment: .firstTextBaseline, spacing: 6) {
                Image(systemName: "banknote")
                    .font(CleansiaTypography.bodyMedium)
                    .capBandAligned(of: CleansiaTypography.scale.bodyMedium)
                    .accessibilityHidden(true)
                VStack(alignment: .leading, spacing: Spacing.hair) {
                    ForEach(Array(cash.items.enumerated()), id: \.offset) { index, part in
                        Text(index == 0 ? part : part.prefix(1).uppercased(with: locale) + part.dropFirst())
                            .fixedSize(horizontal: false, vertical: true)
                    }
                }
                .font(CleansiaTypography.bodyMedium)
                .frame(maxWidth: .infinity, alignment: .leading)
            }
            .foregroundColor(CleansiaColors.onSurfaceVariant)
            .padding(.horizontal, 10)
            .padding(.vertical, Spacing.xxs)
            .background(CleansiaColors.surfaceVariant, in: RoundedRectangle(cornerRadius: CornerRadius.small))
        }
    }
}
