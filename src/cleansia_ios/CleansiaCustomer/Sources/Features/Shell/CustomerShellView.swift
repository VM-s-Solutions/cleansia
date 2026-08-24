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
                    .background(InteractivePopGestureEnabler())
                    .toolbar(.hidden, for: .navigationBar)
                    .navigationDestination(for: ShellRoute.self) { route in
                        destination(route)
                            // EVERY pushed screen, not just the ones that remembered to ask.
                            //
                            // The enabler above is mounted on the stack ROOT, and installing there is
                            // not enough: the root controller stops appearing once something is pushed
                            // over it, so nothing re-asserts the delegate on the screen the customer is
                            // actually looking at. SubscribePlusScreen carried its own copy for that
                            // reason and every other route silently did not — order detail among them,
                            // which is where it was reported: no swipe-back at all.
                            //
                            // Safe to mount everywhere because the delegate is STATIC and install() is
                            // idempotent, which the enabler documents as the point of holding it that
                            // way. Screens that genuinely refuse to be left still are:
                            // MembershipSuccessScreen sets navigationBarBackButtonHidden, and the
                            // delegate reads hidesBackButton rather than guessing.
                            .background(InteractivePopGestureEnabler())
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
        // The order-detail hero's 63-frame cleaning mascot is the app's costliest decode. Warm it here, at shell
        // entry — seconds to minutes ahead of any tap into a detail, from the list, Home or a push deep
        // link — whenever the customer has a clean in flight to open.
        if container.orderRepository.orders.contains(where: { OrderStatusGroup.isActive($0.status) }) {
            AnimatedMascotView.prewarm(.cleaningInProgress)
        }
        // RETURN, not fall through: this shell is being replaced by onboarding, so a prompt raised now
        // is never presented — and raiseReviewPromptIfDue stamps the order as asked BEFORE it opens the
        // sheet, so falling through spends the one chance to ask and shows nothing.
        if await needsOnboarding {
            onNeedsOnboarding()
            return
        }
        await raiseReviewPromptIfDue()
    }

    /// Ask for a review of the most recently finished clean, once. Runs AFTER the awaited fan-out
    /// above so it decides against the fresh order snapshot rather than an empty cache, and after the
    /// onboarding gate so a brand-new customer is never asked two things at once.
    private func raiseReviewPromptIfDue() async {
        guard let userId = profileVM.currentUser?.id, !userId.isBlank else { return }
        let settings = container.appSettings
        let orders = container.orderRepository.orders
        let prompted = Set(
            orders.map(\.id).filter {
                settings.hasAnsweredPrompt(ReviewPrompt.settingsKey(orderId: $0), userId: userId)
            }
        )
        guard let candidate = ReviewPrompt.candidate(orders: orders, alreadyPrompted: prompted) else {
            return
        }
        // Stamped when the prompt is SHOWN, not when it is answered: declining is an answer, and
        // asking twice about the same clean is what makes this pattern feel like nagging.
        settings.markPromptAnswered(ReviewPrompt.settingsKey(orderId: candidate.id), userId: userId)
        model.openOrderForReview(candidate.id)
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
                avatarCache: container.avatarCache,
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
        case let .editRecurring(templateId):
            editRecurring(templateId)
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
            EditProfileView(
                vm: profileVM,
                avatarCache: container.avatarCache,
                showBookingHint: showBookingHint,
                onSaved: { model.pop() }
            )
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
            // Only the order the prompt named, and only until it is consumed — a later manual visit to
            // the same order must not re-raise the sheet.
            openReviewOnLoad: model.reviewPromptOrderId == orderId,
            onReviewPromptConsumed: { model.consumeReviewPrompt() },
            client: container.orderClient,
            repository: container.orderRepository,
            membershipRepository: container.membershipRepository,
            snackbar: snackbar,
            eventBus: container.orderEventBus,
            paymentSheet: StripePaymentController(),
            mapProvider: container.mapProvider,
            // The footer hands back the id of the order on screen — the dispute
            // form is only reachable with one, which is the whole fix.
            onReportIssue: { model.path.append(ShellRoute.createDispute(orderId: $0)) },
            // Pop FIRST. `rebookOrder` presents the booking sheet at the shell
            // root, so leaving the detail pushed underneath drops the customer
            // back onto the old order when the sheet dismisses — Android pops to
            // MainShell for the same reason (`CleansiaNavHost.kt:636-643`).
            // Clearing the path also lets the shell's "some items are no longer
            // available" snackbar be seen.
            onRebook: { orderId in
                model.path = NavigationPath()
                rebookOrder(orderId)
            },
            // Pre-seeded, exactly like the Home and membership-success entries:
            // the createRecurring destination pops on creation, so without the
            // list beneath it a new schedule lands on the tab root instead of on
            // the list it was just added to.
            onMakeRecurring: { orderId in
                model.path = NavigationPath([
                    ShellRoute.recurringList,
                    ShellRoute.createRecurring(orderId: orderId)
                ])
            }
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
            showExpressPerk: membershipVM.expressWaiverAdvertised,
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
            onEdit: { model.path.append(ShellRoute.editRecurring(templateId: $0.id)) },
            onSubscribePlus: { model.path.append(ShellRoute.subscribePlus) }
        )
    }

    private func createRecurring(_ orderId: String?) -> some View {
        CreateRecurringScreen(
            sourceOrderId: orderId,
            repository: container.recurringRepository,
            savedAddressRepository: container.savedAddressRepository,
            geocoding: container.geocodingService,
            mapProvider: container.mapProvider,
            serviceArea: container.serviceArea,
            snackbar: snackbar,
            onCreated: { model.pop() }
        )
    }

    /// The route carries the id, not the template: a `NavigationPath` entry is `Codable` and outlives the
    /// list it came from, so the form re-reads the live row from the repository cache on push.
    @ViewBuilder
    private func editRecurring(_ templateId: String) -> some View {
        if let template = container.recurringRepository.templates.first(where: { $0.id == templateId }) {
            CreateRecurringScreen(
                sourceOrderId: nil,
                editing: template,
                repository: container.recurringRepository,
                savedAddressRepository: container.savedAddressRepository,
                geocoding: container.geocodingService,
                mapProvider: container.mapProvider,
                serviceArea: container.serviceArea,
                snackbar: snackbar,
                onCreated: { model.pop() }
            )
        } else {
            recurringList
        }
    }
}
