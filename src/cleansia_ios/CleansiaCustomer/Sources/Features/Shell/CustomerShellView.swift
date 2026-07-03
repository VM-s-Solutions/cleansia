import CleansiaCore
import SwiftUI

@MainActor
final class CustomerShellModel: ViewModel {
    @Published var selection: CustomerShellTab = .home
    @Published var path = NavigationPath()
    @Published var isBookingPresented = false

    func book() {
        isBookingPresented = true
    }

    /// Pill taps animate the pager — the `animateScrollToPage` parity
    /// (`MainShell.kt:97-99`).
    func select(_ tab: CustomerShellTab) {
        withAnimation { selection = tab }
    }

    /// The T-0313 success→OrderDetail fold: jump to the Orders tab and open the
    /// new order's detail (Orders didn't exist when the success screen shipped).
    func openOrder(_ orderId: String) {
        selection = .orders
        path = NavigationPath([ShellRoute.orderDetail(orderId)])
    }

    func openOrders() {
        selection = .orders
        path = NavigationPath()
    }

    func openEditProfile() {
        selection = .profile
        path = NavigationPath([ShellRoute.editProfile])
    }

    func pop() {
        if !path.isEmpty { path.removeLast() }
    }
}

struct CustomerShellView: View {
    @StateObject private var model = CustomerShellModel()
    @StateObject private var bookingVM = BookingViewModel()
    @StateObject private var membershipVM: MembershipViewModel
    @StateObject private var profileVM: ProfileViewModel
    @ObservedObject private var preferences: CustomerPreferencesModel
    @Environment(\.snackbarController) private var snackbar
    private let container: CustomerAppContainer
    private let onSignedOut: () -> Void

    init(
        container: CustomerAppContainer,
        preferences: CustomerPreferencesModel,
        onSignedOut: @escaping () -> Void
    ) {
        self.container = container
        self.preferences = preferences
        self.onSignedOut = onSignedOut
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
        NavigationStack(path: $model.path) {
            pager
                .safeAreaInset(edge: .bottom) {
                    CustomerBottomBar(
                        selection: model.selection,
                        onSelect: { model.select($0) },
                        onBook: model.book
                    )
                }
                .toolbar(.hidden, for: .navigationBar)
                .navigationDestination(for: ShellRoute.self) { route in
                    destination(route)
                }
        }
        .tint(CleansiaColors.primary)
        .sheet(isPresented: $model.isBookingPresented) {
            BookingSheetView(
                vm: bookingVM,
                geocoding: container.geocodingService,
                mapProvider: container.mapProvider,
                paymentSheet: StripePaymentController(),
                onDismiss: { model.isBookingPresented = false },
                onViewOrder: { orderId in
                    model.isBookingPresented = false
                    model.openOrder(orderId)
                },
                onCompleteProfile: {
                    model.isBookingPresented = false
                    model.openEditProfile()
                }
            )
        }
        .task { await prefetch() }
        .onAppear { snackbar.setBottomInset(ShellSnackbarInset.inset(pathDepth: model.path.count)) }
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
        async let profile = profileVM.refresh()
        _ = await (orders, loyalty, referrals, membership, plans, profile)
    }

    private var pager: some View {
        TabView(selection: $model.selection) {
            HomeTab(
                orderRepository: container.orderRepository,
                membershipVM: membershipVM,
                snackbar: snackbar,
                onBookCleaning: model.book,
                onOrderClick: { model.path.append(ShellRoute.orderDetail($0)) },
                onSeeAllOrders: model.openOrders,
                onCompleteProfile: model.openEditProfile,
                onSubscribePlus: { model.path.append(ShellRoute.subscribePlus) },
                onManageRecurring: { model.path.append(ShellRoute.recurringList) }
            )
            .tag(CustomerShellTab.home)

            OrdersTab(
                repository: container.orderRepository,
                snackbar: snackbar,
                onOrderClick: { model.path.append(ShellRoute.orderDetail($0)) },
                onBookCleaning: model.book
            )
            .tag(CustomerShellTab.orders)

            RewardsTab(
                loyaltyRepository: container.loyaltyRepository,
                referralRepository: container.referralRepository,
                snackbar: snackbar,
                onOpenActivity: { model.path.append(ShellRoute.rewardsActivity) }
            )
            .tag(CustomerShellTab.rewards)

            ProfileTab(
                profileVM: profileVM,
                membershipVM: membershipVM,
                preferences: preferences,
                onOpen: { model.path.append($0) },
                onSignOut: signOut
            )
            .tag(CustomerShellTab.profile)
        }
        .tabViewStyle(.page(indexDisplayMode: .never))
    }

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
                snackbar: snackbar,
                onBack: { model.pop() }
            )
        case .editProfile:
            EditProfileView(vm: profileVM, onSaved: { model.pop() })
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

    private func signOut() {
        Task {
            await container.authClient.logout()
            onSignedOut()
        }
    }
}
