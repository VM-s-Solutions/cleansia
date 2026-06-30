import CleansiaCore
import SwiftUI

@MainActor
final class CustomerShellModel: ViewModel {
    @Published var selection: CustomerShellTab = .home
    @Published var ordersPath: [OrderRoute] = []
    @Published var homePath: [HomeRoute] = []
    @Published var rewardsPath: [RewardsRoute] = []
    @Published var profilePath: [ProfileRoute] = []
    @Published var isBookingPresented = false

    func book() {
        isBookingPresented = true
    }

    /// The T-0313 success→OrderDetail fold: jump to the Orders tab and open the
    /// new order's detail (Orders didn't exist when the success screen shipped).
    func openOrder(_ orderId: String) {
        selection = .orders
        ordersPath = [.detail(orderId)]
    }

    func openOrders() {
        selection = .orders
        ordersPath = []
    }

    func openProfile() {
        selection = .profile
    }
}

enum OrderRoute: Hashable {
    case detail(String)
}

enum HomeRoute: Hashable {
    case detail(String)
    case subscribePlus
    case membershipSuccess
    case recurringList
    case createRecurring(orderId: String?)
}

enum RewardsRoute: Hashable {
    case activity
}

enum ProfileRoute: Hashable {
    case disputes
    case createDispute(orderId: String?)
    case disputeDetail(String)
    case addresses
}

struct CustomerShellView: View {
    @StateObject private var model = CustomerShellModel()
    @StateObject private var membershipVM: MembershipViewModel
    @Environment(\.snackbarController) private var snackbar
    private let container: CustomerAppContainer
    private let onSignedOut: () -> Void

    init(container: CustomerAppContainer, onSignedOut: @escaping () -> Void) {
        self.container = container
        self.onSignedOut = onSignedOut
        _membershipVM = StateObject(wrappedValue: MembershipViewModel(
            repository: container.membershipRepository,
            snackbar: container.snackbar
        ))
    }

    var body: some View {
        tabs
            .overlay(alignment: .bottom) {
                BookFab(action: model.book)
                    .offset(y: -28)
            }
            .sheet(isPresented: $model.isBookingPresented) {
                BookingSheetView(
                    geocoding: container.geocodingService,
                    mapProvider: container.mapProvider,
                    paymentSheet: StripePaymentController(),
                    onDismiss: { model.isBookingPresented = false },
                    onViewOrder: { orderId in
                        model.isBookingPresented = false
                        model.openOrder(orderId)
                    }
                )
            }
            .task { await prefetch() }
    }

    private func prefetch() async {
        await container.orderRepository.refresh()
        await container.loyaltyRepository.refresh()
        await container.referralRepository.refresh()
        await container.membershipRepository.refresh()
        await container.membershipRepository.refreshPlans()
    }

    private var tabs: some View {
        TabView(selection: $model.selection) {
            NavigationStack(path: $model.homePath) {
                HomeTab(
                    orderRepository: container.orderRepository,
                    membershipVM: membershipVM,
                    snackbar: snackbar,
                    onBookCleaning: model.book,
                    onOrderClick: { model.homePath = [.detail($0)] },
                    onSeeAllOrders: model.openOrders,
                    onCompleteProfile: model.openProfile,
                    onSubscribePlus: { model.homePath.append(.subscribePlus) },
                    onManageRecurring: { model.homePath.append(.recurringList) }
                )
                .navigationDestination(for: HomeRoute.self) { route in
                    homeDestination(route)
                }
            }
            .tabItem { Label(CustomerShellTab.home.label, systemImage: CustomerShellTab.home.systemImage) }
            .tag(CustomerShellTab.home)

            NavigationStack(path: $model.ordersPath) {
                OrdersTab(
                    repository: container.orderRepository,
                    snackbar: snackbar,
                    onOrderClick: { model.ordersPath = [.detail($0)] },
                    onBookCleaning: model.book
                )
                .navigationDestination(for: OrderRoute.self) { route in
                    orderDestination(route)
                }
            }
            .tabItem { Label(CustomerShellTab.orders.label, systemImage: CustomerShellTab.orders.systemImage) }
            .tag(CustomerShellTab.orders)

            NavigationStack(path: $model.rewardsPath) {
                RewardsTab(
                    loyaltyRepository: container.loyaltyRepository,
                    referralRepository: container.referralRepository,
                    snackbar: snackbar,
                    onOpenActivity: { model.rewardsPath = [.activity] }
                )
                .navigationDestination(for: RewardsRoute.self) { route in
                    rewardsDestination(route)
                }
            }
            .tabItem { Label(CustomerShellTab.rewards.label, systemImage: CustomerShellTab.rewards.systemImage) }
            .tag(CustomerShellTab.rewards)

            NavigationStack(path: $model.profilePath) {
                ProfileHubView(
                    onOpenDisputes: { model.profilePath = [.disputes] },
                    onOpenAddresses: { model.profilePath = [.addresses] },
                    onSignedOut: onSignedOut
                )
                .navigationDestination(for: ProfileRoute.self) { route in
                    profileDestination(route)
                }
            }
            .tabItem { Label(CustomerShellTab.profile.label, systemImage: CustomerShellTab.profile.systemImage) }
            .tag(CustomerShellTab.profile)
        }
        .tint(CleansiaColors.primary)
    }

    @ViewBuilder
    private func profileDestination(_ route: ProfileRoute) -> some View {
        switch route {
        case .disputes:
            DisputesListView(
                repository: container.disputeRepository,
                snackbar: snackbar,
                onDisputeClick: { model.profilePath.append(.disputeDetail($0)) },
                onCreateDispute: { model.profilePath.append(.createDispute(orderId: nil)) },
                onBrowseOrders: model.openOrders
            )
        case let .createDispute(orderId):
            CreateDisputeView(
                orderId: orderId,
                repository: container.disputeRepository,
                snackbar: snackbar,
                onCreated: { id in model.profilePath = [.disputes, .disputeDetail(id)] }
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
                onBack: { model.profilePath.removeLast() }
            )
        }
    }

    @ViewBuilder
    private func orderDestination(_ route: OrderRoute) -> some View {
        switch route {
        case let .detail(orderId):
            OrderDetailView(
                orderId: orderId,
                client: container.orderClient,
                repository: container.orderRepository,
                snackbar: snackbar,
                eventBus: container.orderEventBus,
                paymentSheet: StripePaymentController()
            )
        }
    }

    @ViewBuilder
    private func homeDestination(_ route: HomeRoute) -> some View {
        switch route {
        case let .detail(orderId):
            OrderDetailView(
                orderId: orderId,
                client: container.orderClient,
                repository: container.orderRepository,
                snackbar: snackbar,
                eventBus: container.orderEventBus,
                paymentSheet: StripePaymentController()
            )
        case .subscribePlus:
            SubscribePlusScreen(
                repository: container.membershipRepository,
                snackbar: snackbar,
                paymentSheet: StripePaymentController(),
                onBack: { model.homePath.removeLast() },
                onSubscribed: { model.homePath.append(.membershipSuccess) }
            )
        case .membershipSuccess:
            MembershipSuccessScreen(
                onSetupRecurring: { model.homePath = [.recurringList, .createRecurring(orderId: nil)] },
                onBackHome: { model.homePath = [] }
            )
        case .recurringList:
            RecurringBookingsScreen(
                repository: container.recurringRepository,
                membershipRepository: container.membershipRepository,
                snackbar: snackbar,
                onCreateNew: { model.homePath.append(.createRecurring(orderId: nil)) },
                onSubscribePlus: { model.homePath.append(.subscribePlus) }
            )
        case let .createRecurring(orderId):
            CreateRecurringScreen(
                sourceOrderId: orderId,
                repository: container.recurringRepository,
                snackbar: snackbar,
                onCreated: { model.homePath.removeLast() }
            )
        }
    }

    @ViewBuilder
    private func rewardsDestination(_ route: RewardsRoute) -> some View {
        switch route {
        case .activity:
            RewardsActivityScreen(
                loyaltyRepository: container.loyaltyRepository,
                snackbar: snackbar
            )
        }
    }
}

private struct BookFab: View {
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Image(systemName: "sparkles")
                .font(.system(size: 30, weight: .semibold))
                .foregroundColor(CleansiaColors.onPrimary)
                .frame(width: 64, height: 64)
                .background(Circle().fill(CleansiaColors.primary))
                .overlay(Circle().stroke(CleansiaColors.background, lineWidth: 4))
        }
        .accessibilityLabel(Text(verbatim: L10n.Shell.book))
    }
}

private struct ProfileHubView: View {
    let onOpenDisputes: () -> Void
    let onOpenAddresses: () -> Void
    let onSignedOut: () -> Void

    var body: some View {
        List {
            Button(action: onOpenAddresses) {
                Label(L10n.AddressManager.profileRow, systemImage: "mappin.and.ellipse")
            }
            .foregroundColor(CleansiaColors.onSurface)

            Button(action: onOpenDisputes) {
                Label(L10n.Disputes.listTitle, systemImage: "exclamationmark.bubble")
            }
            .foregroundColor(CleansiaColors.onSurface)

            Button(role: .destructive, action: onSignedOut) {
                Label(L10n.signOut, systemImage: "rectangle.portrait.and.arrow.right")
            }
        }
        .navigationTitle(L10n.Shell.profile)
        .navigationBarTitleDisplayMode(.inline)
        .background(CleansiaColors.background.ignoresSafeArea())
    }
}
