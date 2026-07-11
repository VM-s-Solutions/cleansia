import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct OrdersTab: View {
    @StateObject private var vm: OrdersListViewModel
    @Environment(\.scenePhase) private var scenePhase
    let onOrderClick: (String) -> Void
    let onBookCleaning: () -> Void

    init(
        repository: OrderRepository,
        snackbar: SnackbarController,
        onOrderClick: @escaping (String) -> Void,
        onBookCleaning: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: OrdersListViewModel(repository: repository, snackbar: snackbar))
        self.onOrderClick = onOrderClick
        self.onBookCleaning = onBookCleaning
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text(L10n.Orders.title)
                .font(CleansiaTypography.headlineMedium)
                .foregroundColor(CleansiaColors.onBackground)
                .padding(.horizontal, Spacing.ml)
                .padding(.vertical, Spacing.m)

            content
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
        .background(CleansiaColors.background.ignoresSafeArea())
        .task { await vm.onAppear() }
        .onChange(of: scenePhase) { phase in
            if phase == .active { Task { await vm.onForeground() } }
        }
    }

    @ViewBuilder
    private var content: some View {
        switch vm.state {
        case .loading:
            OrdersLoadingView()
        case .error:
            ScrollView {
                OrdersErrorView { Task { await vm.retry() } }
                    .frame(maxWidth: .infinity, minHeight: 360)
            }
            .refreshable { await vm.pullToRefresh() }
        case let .loaded(orders) where orders.isEmpty:
            ScrollView {
                OrdersEmptyView(onBookCleaning: onBookCleaning)
                    .frame(maxWidth: .infinity, minHeight: 420)
            }
            .refreshable { await vm.pullToRefresh() }
        case .loaded:
            OrdersListContent(
                vm: vm,
                onOrderClick: onOrderClick
            )
        }
    }
}

private struct OrdersListContent: View {
    @ObservedObject var vm: OrdersListViewModel
    let onOrderClick: (String) -> Void

    var body: some View {
        ScrollView {
            LazyVStack(spacing: Spacing.s) {
                FilterChipsRow(vm: vm)

                ForEach(vm.filteredOrders, id: \.id) { order in
                    Button {
                        if let id = order.id { onOrderClick(id) }
                    } label: {
                        OrderListCard(order: order)
                    }
                    .buttonStyle(.plain)
                    .onAppear {
                        if order.id == vm.filteredOrders.last?.id {
                            Task { await vm.loadNextPage() }
                        }
                    }
                }

                if vm.filteredOrders.isEmpty {
                    Text(verbatim: "\(vm.activeFilter.label) · 0")
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, Spacing.xl)
                }

                if vm.loadingMore {
                    ProgressView()
                        .tint(CleansiaColors.primary)
                        .padding(.vertical, Spacing.s)
                }
            }
            .padding(.horizontal, Spacing.ml)
            .padding(.top, Spacing.xxs)
        }
        .refreshable { await vm.pullToRefresh() }
    }
}

private struct FilterChipsRow: View {
    @ObservedObject var vm: OrdersListViewModel

    var body: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: Spacing.xs) {
                ForEach(OrdersFilter.allCases, id: \.self) { filter in
                    let count = vm.filterCount(filter)
                    OrderFilterChip(
                        label: count > 0 ? L10n.Orders.filterCount(filter.label, count) : filter.label,
                        selected: vm.activeFilter == filter
                    ) {
                        vm.activeFilter = filter
                    }
                }
            }
        }
    }
}

private struct OrderFilterChip: View {
    let label: String
    let selected: Bool
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Text(label)
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(selected ? .white : CleansiaColors.onSurface)
                .padding(.horizontal, Spacing.s)
                .padding(.vertical, Spacing.xs)
                .background(selected ? CleansiaColors.primary : CleansiaColors.surface, in: Capsule())
                .overlay(
                    Capsule().stroke(
                        selected ? CleansiaColors.primary : CleansiaColors.outlineVariant,
                        lineWidth: 1
                    )
                )
        }
        .buttonStyle(.plain)
    }
}

struct OrderListCard: View {
    @Environment(\.locale) private var locale
    let order: OrderListItem

    var body: some View {
        HStack(spacing: 0) {
            Rectangle()
                .fill(OrderStatusPresentation.color(order.orderStatus))
                .frame(width: 4)

            VStack(alignment: .leading, spacing: Spacing.xs) {
                HStack {
                    Text(verbatim: order.displayOrderNumber.map { "#\($0)" } ?? "—")
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    Spacer()
                    OrderStatusPill(
                        label: OrderStatusPresentation.label(order.orderStatus),
                        color: OrderStatusPresentation.color(order.orderStatus)
                    )
                }
                Text(OrdersFormat.dateRange(
                    order.cleaningDateTime,
                    estimatedMinutes: order.estimatedTime ?? 0,
                    locale: locale
                ))
                .font(CleansiaTypography.titleLarge)
                .foregroundColor(CleansiaColors.onBackground)

                if let address = order.customerAddress, !address.isBlank {
                    Label(address, systemImage: "mappin.and.ellipse")
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .lineLimit(1)
                }

                HStack(alignment: .firstTextBaseline) {
                    Text(OrdersFormat.servicesSummary(order, locale: locale))
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                        .lineLimit(2)
                    Spacer()
                    Text(OrdersFormat.price(order.totalPrice ?? 0, currencyCode: order.currency?.code))
                        .font(CleansiaTypography.titleLarge)
                        .foregroundColor(CleansiaColors.onBackground)
                }
            }
            .padding(Spacing.m)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.surface)
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.large)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
    }
}

private struct OrdersLoadingView: View {
    var body: some View {
        VStack(spacing: Spacing.s) {
            ForEach(0 ..< 3, id: \.self) { _ in
                RoundedRectangle(cornerRadius: CornerRadius.large)
                    .fill(CleansiaColors.surfaceVariant)
                    .frame(height: 120)
            }
            Spacer()
        }
        .padding(.horizontal, Spacing.ml)
    }
}

private struct OrdersErrorView: View {
    let onRetry: () -> Void

    var body: some View {
        VStack(spacing: Spacing.s) {
            Image(systemName: "wifi.slash")
                .font(.system(size: 40))
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(L10n.Orders.errorTitle)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            CleansiaOutlinedButton(L10n.Orders.errorRetry, size: .medium, action: onRetry)
                .fixedSize()
        }
        .padding(Spacing.xl)
    }
}

private struct OrdersEmptyView: View {
    let onBookCleaning: () -> Void

    var body: some View {
        MascotEmptyState(
            image: Mascot.idea.image,
            text: L10n.Orders.emptyTitle,
            subtitle: L10n.Orders.emptySubtitle,
            verticallyCentered: true,
            imageSize: 160,
            titleFont: CleansiaTypography.headlineSmall
        ) {
            CleansiaPrimaryButton(L10n.Orders.emptyCta, action: onBookCleaning)
                .fixedSize()
        }
    }
}

#if DEBUG
    struct OrderListCard_Previews: PreviewProvider {
        static var previews: some View {
            OrderListCard(order: OrderListItem(
                id: "1",
                customerAddress: "Karlovo náměstí 10, Praha",
                displayOrderNumber: "1042",
                totalPrice: 1290,
                orderStatus: Code(type: "OrderStatus", name: "OnTheWay", value: 3),
                currency: CurrencyListItem(code: "CZK")
            ))
            .padding()
            .background(CleansiaColors.background)
        }
    }
#endif
