import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct OrdersPaneView: View {
    @ObservedObject var vm: OrdersListViewModel
    let onOpen: (OrderListItem) -> Void

    var body: some View {
        switch vm.tab {
        case .available:
            AvailablePane(
                orders: vm.visibleOrders,
                searchQuery: searchBinding,
                hasSearch: !vm.searchQuery.isEmpty,
                sort: vm.availableSort,
                currentLocation: vm.currentLocation,
                inFlightOrderId: vm.inFlightActionOrderId,
                onSort: { sort in Task { await vm.setAvailableSort(sort) } },
                onTake: { order in Task { await vm.runInlineAction(vm.inlineAction(for: order), on: order) } },
                onOpen: onOpen
            )
        case .active:
            ActivePane(
                orders: vm.visibleOrders,
                inFlightOrderId: vm.inFlightActionOrderId,
                inlineAction: vm.inlineAction(for:),
                onAdvance: { action, order in Task { await vm.runInlineAction(action, on: order) } },
                onOpen: onOpen
            )
        case .history:
            HistoryPane(
                orders: vm.visibleOrders,
                period: vm.completedPeriod,
                onPeriod: { period in Task { await vm.setCompletedPeriod(period) } },
                onOpen: onOpen
            )
        }
    }

    private var searchBinding: Binding<String> {
        Binding(get: { vm.searchQuery }, set: vm.setSearchQuery)
    }
}

// MARK: - Available

private struct AvailablePane: View {
    let orders: [OrderListItem]
    @Binding var searchQuery: String
    let hasSearch: Bool
    let sort: AvailableSort
    let currentLocation: Coordinate?
    let inFlightOrderId: String?
    let onSort: (AvailableSort) -> Void
    let onTake: (OrderListItem) -> Void
    let onOpen: (OrderListItem) -> Void

    private var hotDealPay: Double? {
        guard orders.count >= 2 else { return nil }
        return orders.compactMap(\.estimatedCleanerPay).max().flatMap { $0 > 0 ? $0 : nil }
    }

    var body: some View {
        if orders.isEmpty, !hasSearch {
            OrdersEmptyState(mascot: .leaning, text: L10n.Orders.noOrdersAvailable)
        } else {
            List {
                OrdersSearchField(text: $searchQuery)
                AvailableSummaryRow(orders: orders, sort: sort, onSort: onSort)
                if orders.isEmpty {
                    Text(L10n.Orders.noMatchingOrders)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                ForEach(orders, id: \.id) { order in
                    AvailableOrderRow(
                        order: order,
                        distanceKm: order.distanceKm(from: currentLocation),
                        isHotDeal: hotDealPay.map { (order.estimatedCleanerPay ?? 0) >= $0 } ?? false,
                        isTaking: inFlightOrderId == order.id,
                        onTake: { onTake(order) },
                        onOpen: { onOpen(order) }
                    )
                    .ordersRow()
                }
            }
            .listStyle(.plain)
        }
    }
}

private struct AvailableSummaryRow: View {
    let orders: [OrderListItem]
    let sort: AvailableSort
    let onSort: (AvailableSort) -> Void

    var body: some View {
        HStack {
            Text(L10n.Orders.availableSummary(orders.count, OrdersFormat.totalEarnings(orders)))
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Spacer()
            Menu {
                ForEach(AvailableSort.allCases, id: \.self) { option in
                    Button(OrdersFormat.sortLabel(option)) { onSort(option) }
                }
            } label: {
                HStack(spacing: Spacing.xxs) {
                    Text(OrdersFormat.sortLabel(sort))
                        .font(CleansiaTypography.labelLarge)
                    Image(systemName: "chevron.down").font(.system(size: 12))
                }
                .foregroundColor(CleansiaColors.primary)
            }
        }
        .ordersRow()
    }
}

private struct AvailableOrderRow: View {
    let order: OrderListItem
    let distanceKm: Double?
    let isHotDeal: Bool
    let isTaking: Bool
    let onTake: () -> Void
    let onOpen: () -> Void

    private var isStartingSoon: Bool {
        guard let date = order.cleaningDateTime else { return false }
        let minutes = date.timeIntervalSinceNow / 60
        return minutes >= 0 && minutes <= 120
    }

    var body: some View {
        Button(action: onOpen) {
            VStack(alignment: .leading, spacing: Spacing.xs) {
                HStack(alignment: .top) {
                    Text(OrdersFormat.relativeDateTime(order.cleaningDateTime))
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    Spacer()
                    VStack(alignment: .trailing, spacing: 0) {
                        Text(OrdersFormat.pay(order))
                            .font(CleansiaTypography.titleLarge)
                            .foregroundColor(CleansiaColors.primary)
                        Text(L10n.Orders.youEarn)
                            .font(CleansiaTypography.labelSmall)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                }
                if let address = OrdersFormat.addressLine(order, distanceKm: distanceKm) {
                    Label(address, systemImage: "mappin.and.ellipse")
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .lineLimit(1)
                }
                ScopeChips(order: order)
                if isHotDeal || isStartingSoon {
                    HStack(spacing: Spacing.xs) {
                        if isHotDeal {
                            DecisionBadge(icon: "flame.fill", label: L10n.Orders.topPay, tint: CleansiaColors.error)
                        }
                        if isStartingSoon {
                            DecisionBadge(icon: "clock", label: L10n.Orders.startsSoon, tint: CleansiaColors.primary)
                        }
                    }
                }
                TakeButton(isTaking: isTaking, onTake: onTake)
            }
            .ordersCard()
        }
        .buttonStyle(.plain)
    }
}

/// Full-width Take CTA. A scanning cleaner can grab a job without opening the
/// detail; while taking, the label swaps for a spinner so the request landing
/// is visible without the button resizing.
private struct TakeButton: View {
    let isTaking: Bool
    let onTake: () -> Void

    var body: some View {
        Button(action: onTake) {
            ZStack {
                if isTaking {
                    ProgressView().tint(CleansiaColors.onPrimary)
                } else {
                    Text(L10n.Orders.takeOrder)
                        .font(CleansiaTypography.labelLarge)
                        .foregroundColor(CleansiaColors.onPrimary)
                }
            }
            .frame(maxWidth: .infinity)
            .padding(.vertical, Spacing.s)
            .background(CleansiaColors.primary, in: Capsule())
        }
        .buttonStyle(.plain)
        .disabled(isTaking)
    }
}

// MARK: - Active

private struct ActivePane: View {
    let orders: [OrderListItem]
    let inFlightOrderId: String?
    let inlineAction: (OrderListItem) -> OrderPrimaryAction
    let onAdvance: (OrderPrimaryAction, OrderListItem) -> Void
    let onOpen: (OrderListItem) -> Void

    private var grouped: [(ActiveDayBucket, [OrderListItem])] {
        OrdersGrouping.byDayBucket(orders.filter { !$0.isInProgress })
    }

    var body: some View {
        if orders.isEmpty {
            OrdersEmptyState(mascot: .ready, text: L10n.Orders.noActiveOrders)
        } else {
            List {
                ForEach(grouped, id: \.0) { bucket, rows in
                    Section(OrdersFormat.dayBucketLabel(bucket)) {
                        ForEach(rows, id: \.id) { order in
                            ActiveOrderRow(
                                order: order,
                                action: inlineAction(order),
                                isBusy: inFlightOrderId == order.id,
                                onAdvance: { onAdvance(inlineAction(order), order) },
                                onOpen: { onOpen(order) }
                            )
                            .ordersRow()
                        }
                    }
                }
            }
            .listStyle(.plain)
        }
    }
}

private struct ActiveOrderRow: View {
    let order: OrderListItem
    let action: OrderPrimaryAction
    let isBusy: Bool
    let onAdvance: () -> Void
    let onOpen: () -> Void

    var body: some View {
        VStack(spacing: Spacing.s) {
            CompactOrderRow(order: order, onOpen: onOpen)
            if let labels = swipeLabels {
                SlideToConfirm(
                    idleLabel: labels.idle,
                    busyLabel: labels.busy,
                    isBusy: isBusy,
                    onConfirm: onAdvance
                )
            }
        }
    }

    private var swipeLabels: (idle: String, busy: String)? {
        switch action {
        case .notifyOnTheWay: (L10n.Orders.swipeToNotifyOnTheWay, L10n.Orders.customerNotifiedOnTheWay)
        case .start: (L10n.Orders.swipeToStart, L10n.Orders.startingOrder)
        case .complete: (L10n.Orders.swipeToComplete, L10n.Orders.completingOrder)
        case .take, .completeBlocked, .none: nil
        }
    }
}

// MARK: - History

private struct HistoryPane: View {
    let orders: [OrderListItem]
    let period: CompletedPeriod
    let onPeriod: (CompletedPeriod) -> Void
    let onOpen: (OrderListItem) -> Void

    private var grouped: [(Date?, [OrderListItem])] {
        OrdersGrouping.byDay(orders)
    }

    var body: some View {
        List {
            PeriodFilterRow(period: period, onPeriod: onPeriod)
            HistorySummaryRow(orders: orders)
            if orders.isEmpty {
                Text(L10n.Orders.noCompletedOrders)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            ForEach(grouped, id: \.0) { day, rows in
                Section(OrdersFormat.dayHeader(day)) {
                    ForEach(rows, id: \.id) { order in
                        CompactOrderRow(order: order, onOpen: { onOpen(order) }).ordersRow()
                    }
                }
            }
        }
        .listStyle(.plain)
    }
}

private struct PeriodFilterRow: View {
    let period: CompletedPeriod
    let onPeriod: (CompletedPeriod) -> Void

    var body: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: Spacing.xs) {
                ForEach(CompletedPeriod.allCases, id: \.self) { option in
                    let selected = option == period
                    Button { onPeriod(option) } label: {
                        Text(OrdersFormat.periodLabel(option))
                            .font(CleansiaTypography.labelLarge)
                            .foregroundColor(selected ? CleansiaColors.onPrimary : CleansiaColors.onSurface)
                            .padding(.horizontal, Spacing.s)
                            .padding(.vertical, Spacing.xs)
                            .background(selected ? CleansiaColors.primary : CleansiaColors.surface, in: Capsule())
                    }
                    .buttonStyle(.plain)
                }
            }
        }
        .ordersRow()
    }
}

private struct HistorySummaryRow: View {
    let orders: [OrderListItem]

    var body: some View {
        HStack {
            SummaryStat(value: OrdersFormat.totalEarnings(orders), label: L10n.Orders.earnings)
            Spacer()
            SummaryStat(value: "\(orders.count)", label: L10n.Orders.jobs)
        }
        .ordersCard()
    }
}

private struct SummaryStat: View {
    let value: String
    let label: String

    var body: some View {
        VStack(spacing: 2) {
            Text(value)
                .font(CleansiaTypography.headlineSmall)
                .foregroundColor(CleansiaColors.primary)
            Text(label)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
        .frame(maxWidth: .infinity)
    }
}
