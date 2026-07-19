import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

/// The customer Home tab — a section-for-section port of `HomeTab.kt:217-307`:
/// address bar + bell, smart upsell carousel, order-again/trust strip,
/// recurring schedules (Plus), popular packages, recent bookings, loyalty
/// milestone, seasonal card, behind the first-paint skeleton gate.
struct HomeTab: View {
    @StateObject private var vm: HomeTabViewModel
    @ObservedObject private var notificationBadge: NotificationBadgeModel
    private let notificationFeedClient: NotificationFeedClient
    let onBookCleaning: () -> Void
    let onOpenAddressManager: () -> Void
    let onOrderClick: (String) -> Void
    let onSeeAllOrders: () -> Void
    let onSubscribePlus: () -> Void
    let onOpenReferral: () -> Void
    let onBookPackage: (String) -> Void
    let onRebookOrder: (String) -> Void
    let onSetupRecurring: () -> Void
    let onManageRecurring: () -> Void
    let onNotificationDestination: (CustomerNotificationDestination) -> Void
    @Environment(\.snackbarController) private var snackbar
    @Environment(\.scenePhase) private var scenePhase
    @State private var showNotifications = false

    /// All callbacks + sources are REQUIRED: an optional-defaulted callback
    /// here means a silently inert CTA — the failure class this phase fixed.
    init(
        orderRepository: OrderRepository,
        recurringRepository: RecurringBookingRepository,
        loyaltyRepository: LoyaltyRepository,
        membershipRepository: MembershipRepository,
        savedAddressRepository: SavedAddressRepository,
        notificationBadge: NotificationBadgeModel,
        notificationFeedClient: NotificationFeedClient,
        bookingVM: BookingViewModel,
        snackbar: SnackbarController,
        onBookCleaning: @escaping () -> Void,
        onOpenAddressManager: @escaping () -> Void,
        onOrderClick: @escaping (String) -> Void,
        onSeeAllOrders: @escaping () -> Void,
        onSubscribePlus: @escaping () -> Void,
        onOpenReferral: @escaping () -> Void,
        onBookPackage: @escaping (String) -> Void,
        onRebookOrder: @escaping (String) -> Void,
        onSetupRecurring: @escaping () -> Void,
        onManageRecurring: @escaping () -> Void,
        onNotificationDestination: @escaping (CustomerNotificationDestination) -> Void
    ) {
        _vm = StateObject(wrappedValue: HomeTabViewModel(
            orderRepository: orderRepository,
            recurringRepository: recurringRepository,
            loyaltyRepository: loyaltyRepository,
            membershipRepository: membershipRepository,
            savedAddressRepository: savedAddressRepository,
            catalogSource: bookingVM,
            snackbar: snackbar
        ))
        self.notificationBadge = notificationBadge
        self.notificationFeedClient = notificationFeedClient
        self.onBookCleaning = onBookCleaning
        self.onOpenAddressManager = onOpenAddressManager
        self.onOrderClick = onOrderClick
        self.onSeeAllOrders = onSeeAllOrders
        self.onSubscribePlus = onSubscribePlus
        self.onOpenReferral = onOpenReferral
        self.onBookPackage = onBookPackage
        self.onRebookOrder = onRebookOrder
        self.onSetupRecurring = onSetupRecurring
        self.onManageRecurring = onManageRecurring
        self.onNotificationDestination = onNotificationDestination
    }

    var body: some View {
        Group {
            if vm.firstPaintReady {
                content
                    .transition(.opacity)
            } else {
                HomeSkeleton()
                    .transition(.opacity)
            }
        }
        // Cross-fade the skeleton→content reveal instead of a hard cut, so the
        // dashboard resolves in (Wolt/Bolt feel) rather than popping. The gate
        // still flips exactly once per tab session (`firstPaintReady`).
        .animation(.easeInOut(duration: 0.3), value: vm.firstPaintReady)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(CleansiaColors.background.ignoresSafeArea())
        .sheet(isPresented: $showNotifications) {
            NotificationsInboxSheet(
                client: notificationFeedClient,
                badge: notificationBadge,
                snackbar: snackbar,
                onDestination: onNotificationDestination
            )
            .snackbarHost(snackbar, bottomInset: Spacing.m)
        }
        .task { await vm.runFirstPaintCeiling() }
        .task { await vm.refreshMembershipIfNeeded() }
        .task { await vm.refreshCatalogIfNeeded() }
        .task { await notificationBadge.refresh() }
        .task(id: vm.isPlus) { await vm.refreshRecurringIfPlus(vm.isPlus) }
        .onChange(of: scenePhase) { phase in
            if phase == .active {
                Task { await notificationBadge.refresh() }
            }
        }
    }

    private var content: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                AddressTopBar(
                    displayedAddress: vm.displayedAddress?.oneLine,
                    unreadBadge: notificationBadge.badgeLabel,
                    onAddressTap: onOpenAddressManager,
                    onNotificationTap: { showNotifications = true }
                )
                Spacer().frame(height: Spacing.xs)

                UpsellCarousel(
                    isPlus: vm.isPlus,
                    hasAnyOrders: vm.hasAnyOrders,
                    showSetupRecurring: vm.showSetupRecurringSlide,
                    onAction: handleUpsell
                )
                Spacer().frame(height: Spacing.ml)

                Group {
                    if let completed = vm.mostRecentCompleted {
                        OrderAgainCard(order: completed) {
                            if let id = completed.id { onRebookOrder(id) }
                        }
                    } else {
                        TrustStrip()
                    }
                }
                .padding(.horizontal, Spacing.ml)
                .transition(.opacity)
                Spacer().frame(height: Spacing.l)

                if vm.showRecurringSection {
                    Group {
                        RecurringSchedulesSection(templates: vm.activeRecurring, onManage: onManageRecurring)
                            .padding(.horizontal, Spacing.ml)
                        Spacer().frame(height: Spacing.l)
                    }
                    .transition(.opacity)
                }

                if !vm.popularPackages.isEmpty {
                    Group {
                        PopularPackagesSection(packages: vm.popularPackages, onPackageTap: onBookPackage)
                            .padding(.horizontal, Spacing.ml)
                        Spacer().frame(height: Spacing.l)
                    }
                    .transition(.opacity)
                }

                if vm.showRecent {
                    Group {
                        RecentBookingsSection(
                            orders: vm.recentForDisplay,
                            onOrderTap: onOrderClick,
                            onSeeAll: onSeeAllOrders
                        )
                        .padding(.horizontal, Spacing.ml)
                        Spacer().frame(height: Spacing.l)
                    }
                    .transition(.opacity)
                }

                if let account = vm.milestoneAccount {
                    Group {
                        MilestoneProgressCard(account: account)
                            .padding(.horizontal, Spacing.ml)
                        Spacer().frame(height: Spacing.m)
                    }
                    .transition(.opacity)
                }

                SeasonalCard(onTap: onBookCleaning)
                    .padding(.horizontal, Spacing.ml)
            }
            .padding(.top, Spacing.s)
            // One animation keyed on the section fingerprint: any late-arriving
            // section crossfades in instead of shoving the layout down.
            .animation(.easeInOut(duration: 0.3), value: vm.sectionVisibility)
        }
    }

    /// Slide CTA → callback mapping (`MainShell.kt:265-280`).
    private func handleUpsell(_ action: UpsellSlide.Action) {
        switch action {
        case .subscribePlus:
            onSubscribePlus()
        case .book:
            onBookCleaning()
        case .openReferral:
            onOpenReferral()
        case .setupRecurring:
            onSetupRecurring()
        }
    }
}

/// "Cleaning at / <address> ▾" + the notification bell (`AddressTopBar`,
/// `HomeTab.kt:313-365`). The bell opens the notifications inbox and carries
/// the unread badge ("99+" capped, hidden at zero — FD-AC5); the
/// row is center-aligned so the bell sits mid-height against the two-line
/// address block, matching Android's `verticalAlignment = CenterVertically`.
/// The pin leading and the bell's visible-disc trailing both land on the
/// `Spacing.ml` content gutter shared by the cards below.
private struct AddressTopBar: View {
    @Environment(\.locale) private var locale
    let displayedAddress: String?
    let unreadBadge: String?
    let onAddressTap: () -> Void
    let onNotificationTap: () -> Void

    var body: some View {
        HStack(alignment: .center, spacing: Spacing.xs) {
            Button(action: onAddressTap) {
                HStack(spacing: 6) {
                    Image(systemName: "mappin.and.ellipse")
                        .font(.system(size: 18))
                        .foregroundColor(CleansiaColors.primary)
                    VStack(alignment: .leading, spacing: 0) {
                        Text(L10n.Home.addressLabel)
                            .font(CleansiaTypography.labelSmall)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                        HStack(spacing: Spacing.xxs) {
                            Text(displayedAddress ?? L10n.Home.addressPlaceholder)
                                .font(CleansiaTypography.titleMedium)
                                .foregroundColor(CleansiaColors.onBackground)
                                .lineLimit(1)
                            Image(systemName: "chevron.down")
                                .font(.system(size: 13, weight: .semibold))
                                .foregroundColor(CleansiaColors.onSurfaceVariant)
                        }
                    }
                    Spacer(minLength: 0)
                }
                .padding(.vertical, 6)
                .contentShape(Rectangle())
            }
            .buttonStyle(.plain)

            Button(action: onNotificationTap) {
                Image(systemName: "bell")
                    .font(.system(size: 18))
                    .foregroundColor(CleansiaColors.onSurface)
                    .frame(width: 40, height: 40)
                    .background(Circle().fill(CleansiaColors.surface))
                    .overlay(Circle().stroke(CleansiaColors.outlineVariant, lineWidth: 1))
                    .overlay(alignment: .topTrailing) {
                        if let unreadBadge {
                            Text(verbatim: unreadBadge)
                                .font(CleansiaTypography.labelSmall)
                                .foregroundColor(.white)
                                .padding(.horizontal, 5)
                                .padding(.vertical, 1)
                                .background(CleansiaColors.error, in: Capsule())
                                .offset(x: 4, y: -2)
                        }
                    }
                    .padding(Spacing.xxs)
                    .contentShape(Circle())
            }
            .buttonStyle(.plain)
            .accessibilityLabel(Text(verbatim: L10n.NotificationsInbox.title))
            .accessibilityValue(
                Text(verbatim: unreadBadge.map { L10n.NotificationsInbox.unreadA11y($0) } ?? "")
            )
        }
        .padding(.leading, Spacing.ml)
        .padding(.trailing, Spacing.m)
        .padding(.top, Spacing.s)
        .padding(.bottom, Spacing.xxs)
        .id(locale.identifier)
    }
}

#if DEBUG
    struct AddressTopBar_Previews: PreviewProvider {
        static var previews: some View {
            VStack(spacing: 0) {
                AddressTopBar(
                    displayedAddress: "Zenklova 6, Praha",
                    unreadBadge: "3",
                    onAddressTap: {},
                    onNotificationTap: {}
                )
                AddressTopBar(displayedAddress: nil, unreadBadge: nil, onAddressTap: {}, onNotificationTap: {})
            }
            .background(CleansiaColors.background)
            .previewLayout(.sizeThatFits)
        }
    }
#endif
