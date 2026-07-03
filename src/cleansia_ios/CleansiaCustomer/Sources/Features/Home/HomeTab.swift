import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct HomeTab: View {
    @StateObject private var vm: HomeTabViewModel
    @ObservedObject private var membershipVM: MembershipViewModel
    let onBookCleaning: () -> Void
    let onOrderClick: (String) -> Void
    let onSeeAllOrders: () -> Void
    let onCompleteProfile: () -> Void
    let onSubscribePlus: () -> Void
    let onManageRecurring: () -> Void
    let onOpenReferral: (() -> Void)?
    let onSetupRecurring: (() -> Void)?

    init(
        orderRepository: OrderRepository,
        membershipVM: MembershipViewModel,
        snackbar: SnackbarController,
        recurringRepository: RecurringBookingRepository? = nil,
        onBookCleaning: @escaping () -> Void,
        onOrderClick: @escaping (String) -> Void,
        onSeeAllOrders: @escaping () -> Void,
        onCompleteProfile: @escaping () -> Void,
        onSubscribePlus: @escaping () -> Void,
        onManageRecurring: @escaping () -> Void,
        onOpenReferral: (() -> Void)? = nil,
        onSetupRecurring: (() -> Void)? = nil
    ) {
        _vm = StateObject(wrappedValue: HomeTabViewModel(
            orderRepository: orderRepository,
            recurringRepository: recurringRepository,
            snackbar: snackbar
        ))
        self.membershipVM = membershipVM
        self.onBookCleaning = onBookCleaning
        self.onOrderClick = onOrderClick
        self.onSeeAllOrders = onSeeAllOrders
        self.onCompleteProfile = onCompleteProfile
        self.onSubscribePlus = onSubscribePlus
        self.onManageRecurring = onManageRecurring
        self.onOpenReferral = onOpenReferral
        self.onSetupRecurring = onSetupRecurring
    }

    private var isPlus: Bool {
        membershipVM.current?.hasMembership == true
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.m) {
                GreetingHeader()
                    .padding(.horizontal, Spacing.ml)

                ProfileNudgeCard(onComplete: onCompleteProfile)
                    .padding(.horizontal, Spacing.ml)

                UpsellCarousel(
                    slides: UpsellSlide.slides(
                        isPlus: isPlus,
                        hasAnyOrders: vm.hasAnyOrders,
                        showSetupRecurring: vm.showSetupRecurringSlide(isPlus: isPlus)
                    ),
                    onAction: handleUpsell
                )

                MembershipManagementCard(vm: membershipVM, onSubscribeClick: onSubscribePlus)
                    .padding(.horizontal, Spacing.ml)

                if isPlus {
                    RecurringEntryRow(onManage: onManageRecurring)
                        .padding(.horizontal, Spacing.ml)
                }

                if !vm.recentOrders.isEmpty {
                    RecentOrdersSection(
                        orders: Array(vm.recentOrders.prefix(3)),
                        onOrderClick: onOrderClick,
                        onSeeAll: onSeeAllOrders
                    )
                    .padding(.horizontal, Spacing.ml)
                }
            }
            .padding(.top, Spacing.m)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(CleansiaColors.background.ignoresSafeArea())
        .task {
            await vm.refreshCatalog()
            await membershipVM.load()
        }
        .task(id: isPlus) {
            await vm.refreshRecurringIfPlus(isPlus)
        }
    }

    /// Slide CTA → callback mapping (`MainShell.kt:265-280`). Until the shell
    /// wires the two new callbacks: setup-recurring falls back to the recurring
    /// list (which carries its own create CTA), referral is inert.
    private func handleUpsell(_ action: UpsellSlide.Action) {
        switch action {
        case .subscribePlus:
            onSubscribePlus()
        case .book:
            onBookCleaning()
        case .openReferral:
            onOpenReferral?()
        case .setupRecurring:
            (onSetupRecurring ?? onManageRecurring)()
        }
    }
}

private struct RecurringEntryRow: View {
    let onManage: () -> Void

    var body: some View {
        Button(action: onManage) {
            HStack(spacing: Spacing.s) {
                Image(systemName: "repeat")
                    .font(.system(size: 22))
                    .foregroundColor(CleansiaColors.primary)
                VStack(alignment: .leading, spacing: Spacing.hair) {
                    Text(L10n.Recurring.bookingsTitle)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    Text(L10n.Membership.perkRecurringDesc)
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
            .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.large))
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.large)
                    .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
            )
        }
        .buttonStyle(.plain)
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
