import CleansiaCore
import SwiftUI
import UIKit

struct CustomerShellView: View {
    @StateObject var model = CustomerShellModel()
    @StateObject var bookingVM = BookingViewModel()
    @StateObject private var membershipVM: MembershipViewModel
    @StateObject private var profileVM: ProfileViewModel
    @ObservedObject private var preferences: CustomerPreferencesModel
    @EnvironmentObject private var pushNavigation: PushNavigationModel
    @Environment(\.snackbarController) var snackbar
    let container: CustomerAppContainer
    private let onSignedOut: () -> Void
    private let onNeedsOnboarding: () -> Void

    init(
        container: CustomerAppContainer,
        preferences: CustomerPreferencesModel,
        onSignedOut: @escaping () -> Void,
        onNeedsOnboarding: @escaping () -> Void
    ) {
        self.container = container
        self.preferences = preferences
        self.onSignedOut = onSignedOut
        self.onNeedsOnboarding = onNeedsOnboarding
        _membershipVM = StateObject(wrappedValue: MembershipViewModel(
            repository: container.membershipRepository,
            snackbar: container.snackbar
        ))
        _profileVM = StateObject(wrappedValue: ProfileViewModel(
            repository: container.userProfileRepository,
            settings: container.appSettings,
            snackbar: container.snackbar
        ))
    }

    var body: some View {
        ZStack(alignment: .bottom) {
            CleansiaColors.background.ignoresSafeArea()
            NavigationStack(path: $model.path) {
                tabs
                    .background(InteractivePopGestureEnabler(onPop: { model.pop() }))
                    .toolbar(.hidden, for: .navigationBar)
                    .navigationDestination(for: ShellRoute.self) { route in
                        destination(route)
                    }
            }
            // Tab roots only — a pushed child covers the shell, so the FAB (like
            // Android's, which lives on the covered MainShell) is gone on detail
            // screens. A blank `.book` placeholder tab reserves the center of the
            // five bar slots, so the FAB docks on its own evenly-spaced slot
            // (screen-center) rather than crammed into a gap. Center-docked onto
            // the 49pt tab bar's top edge so it overlaps the bar center without
            // covering a real tab icon on the iPhone 17 (26.x) or iPhone 14 (16.4).
            if model.path.isEmpty {
                bookFab
            }
        }
        .tint(CleansiaColors.primary)
        .sheet(isPresented: $model.isBookingPresented) {
            BookingSheetView(
                vm: bookingVM,
                geocoding: container.geocodingService,
                mapProvider: container.mapProvider,
                serviceArea: container.serviceArea,
                paymentSheet: StripePaymentController(),
                orderClient: container.orderClient,
                warmOrders: { await container.orderRepository.refresh() },
                onDismiss: { model.isBookingPresented = false },
                onViewOrder: { orderId in
                    model.isBookingPresented = false
                    model.openOrder(orderId)
                },
                onCompleteProfile: {
                    model.isBookingPresented = false
                    model.openEditProfile(showBookingHint: true)
                }
            )
        }
        .sheet(isPresented: $model.isAddressManagerPresented) {
            AddressManagerView(
                repository: container.savedAddressRepository,
                geocoding: container.geocodingService,
                mapProvider: container.mapProvider,
                serviceArea: container.serviceArea,
                snackbar: snackbar,
                onBack: { model.isAddressManagerPresented = false },
                onSelected: { _ in model.isAddressManagerPresented = false }
            )
            .snackbarHost(snackbar, bottomInset: Spacing.m)
        }
        .onChange(of: model.selection) { _ in
            if model.resolveSelection() { openBooking() }
        }
        .onChange(of: pushNavigation.pendingDestination) { destination in
            guard let destination else { return }
            model.applyPushTap(CustomerPushTapRouting.plan(for: destination))
            _ = pushNavigation.consume()
        }
        .task { await prefetch() }
        .onAppear {
            if let destination = pushNavigation.pendingDestination {
                model.applyPushTap(CustomerPushTapRouting.plan(for: destination))
                _ = pushNavigation.consume()
            }
            snackbar.setBottomInset(ShellSnackbarInset.inset(pathDepth: model.path.count))
        }
        .onChange(of: model.path.count) { depth in
            snackbar.setBottomInset(ShellSnackbarInset.inset(pathDepth: depth))
        }
        .onDisappear { snackbar.resetBottomInset() }
    }

    private func prefetch() async {
        async let orders = container.orderRepository.refresh()
        async let loyalty = container.loyaltyRepository.refresh()
        async let referrals = container.referralRepository.refresh()
        async let membership = container.membershipRepository.refresh()
        async let plans = container.membershipRepository.refreshPlans()
        async let addresses = container.savedAddressRepository.refresh()
        async let recurring = container.recurringRepository.refresh()
        async let catalog: Void = bookingVM.loadCatalog()
        // The gate refreshes the profile itself (`MainShell.kt:157-181` — once
        // per shell entry, on the fresh server snapshot, never a stale cache).
        async let needsOnboarding = profileVM.needsOnboarding()
        _ = await (orders, loyalty, referrals, membership, plans, addresses, recurring, catalog)
        if await needsOnboarding { onNeedsOnboarding() }
    }

    private var tabs: some View {
        TabView(selection: $model.selection) {
            HomeTab(
                orderRepository: container.orderRepository,
                recurringRepository: container.recurringRepository,
                loyaltyRepository: container.loyaltyRepository,
                membershipRepository: container.membershipRepository,
                savedAddressRepository: container.savedAddressRepository,
                notificationBadge: container.notificationBadge,
                notificationFeedClient: container.notificationFeedClient,
                bookingVM: bookingVM,
                snackbar: snackbar,
                onBookCleaning: openBooking,
                onOpenAddressManager: { model.isAddressManagerPresented = true },
                onOrderClick: { model.path.append(ShellRoute.orderDetail($0)) },
                onSeeAllOrders: model.openOrders,
                onSubscribePlus: { model.path.append(ShellRoute.subscribePlus) },
                onOpenReferral: { model.select(.rewards) },
                onBookPackage: bookPackage,
                onRebookOrder: rebookOrder,
                // Pre-seeded: the createRecurring destination pops on creation, so the
                // wizard must sit ON TOP of the list or creation lands on the tab root
                // (Android's fixed Path B) — mirrors the membershipSuccess wiring.
                onSetupRecurring: {
                    model.path = NavigationPath([ShellRoute.recurringList, ShellRoute.createRecurring(orderId: nil)])
                },
                onManageRecurring: { model.path.append(ShellRoute.recurringList) },
                // Feed-row taps land exactly where a push tap does — the same
                // resolver, the same routing plan (FD-AC9).
                onNotificationDestination: { model.applyPushTap(CustomerPushTapRouting.plan(for: $0)) }
            )
            .tabItem { tabLabel(.home) }
            .tag(CustomerShellTab.home)

            OrdersTab(
                repository: container.orderRepository,
                snackbar: snackbar,
                onOrderClick: { model.path.append(ShellRoute.orderDetail($0)) },
                onBookCleaning: openBooking
            )
            .tabItem { tabLabel(.orders) }
            .tag(CustomerShellTab.orders)

            Color.clear
                .tabItem { tabLabel(.book) }
                .tag(CustomerShellTab.book)
                .accessibilityHidden(true)

            RewardsTab(
                loyaltyRepository: container.loyaltyRepository,
                referralRepository: container.referralRepository,
                snackbar: snackbar,
                onOpenActivity: { model.path.append(ShellRoute.rewardsActivity) }
            )
            .tabItem { tabLabel(.rewards) }
            .tag(CustomerShellTab.rewards)

            ProfileTab(
                profileVM: profileVM,
                membershipVM: membershipVM,
                preferences: preferences,
                onOpen: { model.path.append($0) },
                onSignOut: signOut
            )
            .tabItem { tabLabel(.profile) }
            .tag(CustomerShellTab.profile)
        }
    }

    private func tabLabel(_ tab: CustomerShellTab) -> some View {
        Label(tab.label, systemImage: tab.systemImage)
    }

    private var bookFab: some View {
        BookFab(action: openBooking)
            .padding(.bottom, BookFabMetrics.bottomPadding)
    }

    private func signOut() {
        Task {
            await container.authClient.logout()
            onSignedOut()
        }
    }
}

extension CustomerShellView {
    @ViewBuilder
    private func destination(_ route: ShellRoute) -> some View {
        switch route {
        case let .orderDetail(orderId):
            orderDetail(orderId)
        case .subscribePlus:
            subscribePlus
        case .membershipSuccess:
            membershipSuccess
        case .recurringList:
            recurringList
        case let .createRecurring(orderId):
            createRecurring(orderId)
        case .rewardsActivity:
            RewardsActivityScreen(
                loyaltyRepository: container.loyaltyRepository,
                snackbar: snackbar
            )
        default:
            profileDestination(route)
        }
    }

    @ViewBuilder
    private func profileDestination(_ route: ShellRoute) -> some View {
        switch route {
        case .disputes:
            DisputesListView(
                repository: container.disputeRepository,
                snackbar: snackbar,
                onDisputeClick: { model.path.append(ShellRoute.disputeDetail($0)) },
                onCreateDispute: { model.path.append(ShellRoute.createDispute(orderId: nil)) },
                onBrowseOrders: model.openOrders
            )
        case let .createDispute(orderId):
            CreateDisputeView(
                orderId: orderId,
                repository: container.disputeRepository,
                snackbar: snackbar,
                onCreated: { id in
                    model.path = NavigationPath([ShellRoute.disputes, ShellRoute.disputeDetail(id)])
                }
            )
        case let .disputeDetail(disputeId):
            DisputeDetailView(
                disputeId: disputeId,
                repository: container.disputeRepository,
                snackbar: snackbar
            )
        case .addresses:
            AddressManagerView(
                repository: container.savedAddressRepository,
                geocoding: container.geocodingService,
                mapProvider: container.mapProvider,
                serviceArea: container.serviceArea,
                snackbar: snackbar,
                onBack: { model.pop() },
                onSelected: { _ in model.pop() }
            )
        case let .editProfile(showBookingHint):
            EditProfileView(vm: profileVM, showBookingHint: showBookingHint, onSaved: { model.pop() })
        default:
            settingsDestination(route)
        }
    }

    @ViewBuilder
    private func settingsDestination(_ route: ShellRoute) -> some View {
        switch route {
        case .devices:
            CustomerDevicesView(
                client: container.devicesClient,
                authClient: container.authClient,
                snackbar: snackbar,
                onSignedOut: onSignedOut
            )
        case .notifications:
            NotificationsView(client: container.notificationPreferencesClient)
        case .security:
            SecurityView(
                email: profileVM.currentUser?.email ?? "",
                language: preferences.languageTag,
                client: container.changePasswordClient,
                snackbar: snackbar,
                onChanged: { model.pop() }
            )
        case .language:
            LanguagePickerView(preferences: preferences, onSelected: { model.pop() })
        case .appearance:
            AppearancePickerView(preferences: preferences, onSelected: { model.pop() })
        case .help:
            HelpSupportView()
        case .deleteAccount:
            DeleteAccountView(
                userEmail: profileVM.currentUser?.email ?? "",
                client: container.gdprDeleteClient,
                authClient: container.authClient,
                snackbar: snackbar,
                onDeleted: onSignedOut
            )
        default:
            EmptyView()
        }
    }

    private func orderDetail(_ orderId: String) -> some View {
        OrderDetailView(
            orderId: orderId,
            client: container.orderClient,
            repository: container.orderRepository,
            snackbar: snackbar,
            eventBus: container.orderEventBus,
            paymentSheet: StripePaymentController()
        )
    }

    /// One deduped destination for both the Home banner and the Profile card.
    /// On subscribe, Success REPLACES the paywall so back never lands on it —
    /// the `CleansiaNavHost.kt:540-546` popUpTo-inclusive parity.
    private var subscribePlus: some View {
        SubscribePlusScreen(
            repository: container.membershipRepository,
            snackbar: snackbar,
            paymentSheet: StripePaymentController(),
            onBack: { model.pop() },
            onSubscribed: {
                Task { await membershipVM.refresh() }
                model.pop()
                model.path.append(ShellRoute.membershipSuccess)
            }
        )
    }

    private var membershipSuccess: some View {
        MembershipSuccessScreen(
            onSetupRecurring: {
                model.path = NavigationPath([
                    ShellRoute.recurringList,
                    ShellRoute.createRecurring(orderId: nil)
                ])
            },
            onBackHome: { model.path = NavigationPath() }
        )
    }

    private var recurringList: some View {
        RecurringBookingsScreen(
            repository: container.recurringRepository,
            membershipRepository: container.membershipRepository,
            snackbar: snackbar,
            onCreateNew: { model.path.append(ShellRoute.createRecurring(orderId: nil)) },
            onSubscribePlus: { model.path.append(ShellRoute.subscribePlus) }
        )
    }

    private func createRecurring(_ orderId: String?) -> some View {
        CreateRecurringScreen(
            sourceOrderId: orderId,
            repository: container.recurringRepository,
            snackbar: snackbar,
            onCreated: { model.pop() }
        )
    }
}

/// Edge-only swipe-to-go-back. `.toolbar(.hidden, for: .navigationBar)` on the shell root disables the
/// system pop gesture, and re-enabling it by replacing that recognizer's delegate strips its private
/// left-edge scoping — which makes the WHOLE page draggable sideways (worst on tall/scrollable pages).
/// So we leave the system recognizer OFF and install our OWN `UIScreenEdgePanGestureRecognizer(edges:
/// .left)`, which is edge-scoped by construction (it physically cannot begin mid-page), and pop through
/// the SwiftUI `NavigationPath` via `onPop` so navigation state stays in sync (no `popViewController`
/// desync, no private API).
private struct InteractivePopGestureEnabler: UIViewControllerRepresentable {
    let onPop: () -> Void

    func makeUIViewController(context _: Context) -> GestureController {
        let controller = GestureController()
        controller.onPop = onPop
        return controller
    }

    /// Re-run install on update too: if this attaches before the ancestor UINavigationController joins
    /// the parent chain, `navigationController` is nil at `didMove` and the gesture would never install.
    func updateUIViewController(_ controller: GestureController, context _: Context) {
        controller.onPop = onPop
        controller.installIfNeeded()
    }

    final class GestureController: UIViewController {
        var onPop: (() -> Void)?
        private weak var edgeGesture: UIScreenEdgePanGestureRecognizer?

        override func didMove(toParent parent: UIViewController?) {
            super.didMove(toParent: parent)
            installIfNeeded()
        }

        func installIfNeeded() {
            guard let navView = navigationController?.view, edgeGesture == nil else { return }
            // Keep the system pop recognizer OFF — re-enabling it under a hidden nav bar needs a
            // delegate swap that strips its edge scoping and lets the whole page drag. Our own
            // left-edge gesture below is edge-scoped by construction instead.
            navigationController?.interactivePopGestureRecognizer?.isEnabled = false
            let gesture = UIScreenEdgePanGestureRecognizer(target: self, action: #selector(handleEdgePan(_:)))
            gesture.edges = .left
            navView.addGestureRecognizer(gesture)
            edgeGesture = gesture
        }

        @objc private func handleEdgePan(_ gesture: UIScreenEdgePanGestureRecognizer) {
            guard gesture.state == .ended else { return }
            let translation = gesture.translation(in: gesture.view)
            let velocity = gesture.velocity(in: gesture.view)
            // A committed rightward edge-swipe (dragged far enough, or a flick) pops.
            if translation.x > 60 || velocity.x > 600 {
                onPop?()
            }
        }
    }
}
