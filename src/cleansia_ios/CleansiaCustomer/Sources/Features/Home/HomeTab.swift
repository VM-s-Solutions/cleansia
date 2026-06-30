import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct HomeTab: View {
    @StateObject private var vm: HomeTabViewModel
    let onBookCleaning: () -> Void
    let onOrderClick: (String) -> Void
    let onSeeAllOrders: () -> Void
    let onCompleteProfile: () -> Void

    init(
        orderRepository: OrderRepository,
        snackbar: SnackbarController,
        onBookCleaning: @escaping () -> Void,
        onOrderClick: @escaping (String) -> Void,
        onSeeAllOrders: @escaping () -> Void,
        onCompleteProfile: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: HomeTabViewModel(orderRepository: orderRepository, snackbar: snackbar))
        self.onBookCleaning = onBookCleaning
        self.onOrderClick = onOrderClick
        self.onSeeAllOrders = onSeeAllOrders
        self.onCompleteProfile = onCompleteProfile
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.m) {
                GreetingHeader()

                ProfileNudgeCard(onComplete: onCompleteProfile)

                BookCard(onBook: onBookCleaning)

                if !vm.recentOrders.isEmpty {
                    RecentOrdersSection(
                        orders: Array(vm.recentOrders.prefix(3)),
                        onOrderClick: onOrderClick,
                        onSeeAll: onSeeAllOrders
                    )
                }

                Color.clear.frame(height: Spacing.xxl)
            }
            .padding(.horizontal, Spacing.ml)
            .padding(.top, Spacing.m)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(CleansiaColors.background.ignoresSafeArea())
        .task { await vm.refreshCatalog() }
    }
}

private struct GreetingHeader: View {
    var body: some View {
        Text(L10n.Home.greeting)
            .font(CleansiaTypography.headlineMedium)
            .foregroundColor(CleansiaColors.onBackground)
            .frame(maxWidth: .infinity, alignment: .leading)
    }
}

private struct ProfileNudgeCard: View {
    let onComplete: () -> Void

    var body: some View {
        Button(action: onComplete) {
            HStack(spacing: Spacing.s) {
                Image(systemName: "person.crop.circle.badge.exclamationmark")
                    .font(.system(size: 24))
                    .foregroundColor(CleansiaColors.primary)
                VStack(alignment: .leading, spacing: Spacing.hair) {
                    Text(L10n.Home.profileNudgeTitle)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    Text(L10n.Home.profileNudgeSubtitle)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .multilineTextAlignment(.leading)
                }
                Spacer()
                Image(systemName: "chevron.right")
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            .padding(Spacing.m)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(
                CleansiaColors.primaryContainer.opacity(0.4),
                in: RoundedRectangle(cornerRadius: CornerRadius.large)
            )
        }
        .buttonStyle(.plain)
    }
}

private struct BookCard: View {
    let onBook: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            Text(L10n.Home.bookTitle)
                .font(CleansiaTypography.headlineSmall)
                .foregroundColor(CleansiaColors.onBackground)
            Text(L10n.Home.bookSubtitle)
                .font(CleansiaTypography.bodyLarge)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            CleansiaPrimaryButton(L10n.Home.bookCta, leadingIcon: "sparkles", action: onBook)
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.large))
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.large)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
    }
}

private struct RecentOrdersSection: View {
    let orders: [OrderListItem]
    let onOrderClick: (String) -> Void
    let onSeeAll: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            HStack {
                Text(L10n.Home.recentOrdersTitle)
                    .font(CleansiaTypography.headlineSmall)
                    .foregroundColor(CleansiaColors.onBackground)
                Spacer()
                Button(action: onSeeAll) {
                    Text(L10n.Home.seeAll)
                        .font(CleansiaTypography.labelLarge)
                        .foregroundColor(CleansiaColors.primary)
                }
            }
            ForEach(orders, id: \.id) { order in
                Button {
                    if let id = order.id { onOrderClick(id) }
                } label: {
                    OrderListCard(order: order)
                }
                .buttonStyle(.plain)
            }
        }
    }
}
