import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct OrderDetailView: View {
    @StateObject private var vm: OrderDetailViewModel
    @State private var showCancelSheet = false
    @State private var showReviewSheet = false
    @State private var showPhotos = false
    @State private var receiptURL: ReceiptFile?
    @State private var snapAnchor: SnapAnchor = .peek

    private let routeOrderId: String
    private let client: OrderClient
    private let snackbar: SnackbarController
    private let paymentSheet: PaymentSheetPresenting
    private let mapProvider: MapProvider
    private let onReportIssue: (String) -> Void
    private let onRebook: (String) -> Void
    private let onMakeRecurring: (String) -> Void
    private let openReviewOnLoad: Bool
    private let onReviewPromptConsumed: () -> Void
    @State private var reviewAutoOpened = false

    init(
        orderId: String,
        openReviewOnLoad: Bool = false,
        onReviewPromptConsumed: @escaping () -> Void = {},
        client: OrderClient,
        repository: OrderRepository,
        membershipRepository: MembershipRepository,
        snackbar: SnackbarController,
        eventBus: OrderEventBus,
        paymentSheet: PaymentSheetPresenting,
        mapProvider: MapProvider,
        onReportIssue: @escaping (String) -> Void,
        onRebook: @escaping (String) -> Void,
        onMakeRecurring: @escaping (String) -> Void
    ) {
        _vm = StateObject(
            wrappedValue: OrderDetailViewModel(
                orderId: orderId,
                client: client,
                repository: repository,
                membershipRepository: membershipRepository,
                snackbar: snackbar,
                eventBus: eventBus
            )
        )
        routeOrderId = orderId
        self.client = client
        self.snackbar = snackbar
        self.paymentSheet = paymentSheet
        self.mapProvider = mapProvider
        self.onReportIssue = onReportIssue
        self.onRebook = onRebook
        self.onMakeRecurring = onMakeRecurring
        self.openReviewOnLoad = openReviewOnLoad
        self.onReviewPromptConsumed = onReviewPromptConsumed
    }

    var body: some View {
        content
            .navigationTitle(navigationTitle)
            .navigationBarTitleDisplayMode(.inline)
            .background(CleansiaColors.background.ignoresSafeArea())
            .task { await vm.load() }
            // The prompted arrival. Waits for the order to actually resolve — raising the sheet over a
            // spinner shows a card with no date and no cleaner. Fires once, and consumes the shell's
            // flag either way so a later manual visit to the same order does not re-open it.
            .onReceive(vm.$state) { state in
                guard openReviewOnLoad, !reviewAutoOpened, case let .loaded(order) = state else { return }
                reviewAutoOpened = true
                onReviewPromptConsumed()
                // Server truth wins: a review left on another device between the prompt and this
                // screen means there is nothing left to ask for.
                if order.review == nil { showReviewSheet = true }
            }
            .onReceive(vm.cancelSucceeded) { _ in showCancelSheet = false }
            .onReceive(vm.reviewSucceeded) { _ in showReviewSheet = false }
            .onReceive(vm.receiptReady) { url in receiptURL = ReceiptFile(url: url) }
            .onReceive(vm.recurringCardPayment) { presentation in
                Task {
                    let outcome = await paymentSheet.present(presentation)
                    await vm.notifyRecurringPaymentResult(outcome)
                }
            }
            .navigationDestination(isPresented: $showPhotos) {
                OrderPhotosScreen(orderId: orderId, client: client, snackbar: snackbar)
            }
            .sheet(isPresented: $showCancelSheet) { cancelSheet }
            .sheet(isPresented: $showReviewSheet) { reviewSheet }
            .sheet(item: $receiptURL) { receipt in
                receiptPreview(receipt.url)
            }
    }

    /// The wire's `id` is optional, so fall back to the id this screen was routed
    /// with — they are the same order, and "" is never a usable id for the photos
    /// screen or the dispute route. This second source is why `CustomerOrderDetail`
    /// does not refuse a missing id the way the partner detail does.
    private var orderId: String {
        vm.state.loadedValue?.id ?? routeOrderId
    }

    private var navigationTitle: String {
        vm.state.loadedValue?.displayOrderNumber.map { "#\($0)" } ?? ""
    }

    @ViewBuilder
    private var content: some View {
        switch vm.state {
        case .loading:
            ProgressView()
                .tint(CleansiaColors.primary)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case let .error(error):
            OrderDetailErrorView(error: error) { Task { await vm.retry() } }
        case let .loaded(order):
            loadedShell(order)
        }
    }

    /// The map is always behind, the sheet is always over it, and the mascot rides
    /// the seam between them — the partner order-detail topology, which is what the
    /// customer screen is being brought to parity with.
    private func loadedShell(_ order: CustomerOrderDetail) -> some View {
        SnapSheet(anchor: $snapAnchor) {
            OrderDetailMapBackdrop(order: order, mapProvider: mapProvider)
        } ornament: {
            MascotPuck(OrderDetailMascotArt.art(for: order.status))
        } content: {
            VStack(spacing: 0) {
                OrderDetailContent(
                    order: order,
                    photos: vm.photos,
                    isDownloadingReceipt: vm.receiptState.isSubmitting,
                    onLeaveReview: { showReviewSheet = true },
                    onDownloadReceipt: { Task { await vm.downloadReceipt() } },
                    onViewPhotos: { showPhotos = true }
                )
                .task(id: order.id) { await vm.ensurePhotosLoaded() }

                footer(order)
            }
        }
    }

    @ViewBuilder
    private func footer(_ order: CustomerOrderDetail) -> some View {
        if OrderRecurringConfirm.needsConfirmation(order) {
            ConfirmRecurringFooter(submitting: vm.confirmRecurringState.isSubmitting) {
                Task { await vm.confirmRecurring() }
            }
        } else if OrderDetailFooterActions.showFooter(order.status, authoring: vm.recurringAuthoring) {
            OrderDetailActionsFooter(
                showRebook: OrderDetailFooterActions.showRebook(order.status),
                showMakeRecurring: OrderDetailFooterActions.showMakeRecurring(
                    order.status,
                    authoring: vm.recurringAuthoring
                ),
                showCancel: OrderStatusGroup.isCancellable(order.status),
                showReportIssue: OrderStatusGroup.isReportable(order.status),
                cancelEnabled: !vm.cancelState.isSubmitting,
                onRebook: { onRebook(orderId) },
                onMakeRecurring: { onMakeRecurring(orderId) },
                onCancel: { showCancelSheet = true },
                onReportIssue: { onReportIssue(orderId) }
            )
        }
    }

    private var cancelSheet: some View {
        CancelOrderSheet(
            quote: vm.cancellationQuote,
            currencyCode: vm.cancellationQuote.loadedValue?.currencyCode ?? vm.state.loadedValue?.currencyCode,
            isSubmitting: vm.cancelState.isSubmitting,
            errorMessage: vm.cancelState.errorMessage,
            onReasonChanged: vm.dismissCancelError,
            onRetryQuote: { Task { await vm.loadCancellationQuote() } },
            onConfirm: { reason in Task { await vm.cancel(reason: reason) } },
            onDismiss: {
                if !vm.cancelState.isSubmitting {
                    showCancelSheet = false
                    vm.dismissCancelError()
                }
            }
        )
        .task { await vm.loadCancellationQuote() }
        .snackbarHost(snackbar, bottomInset: SnackbarController.defaultBottomInset)
    }

    @ViewBuilder
    private var reviewSheet: some View {
        if let order = vm.state.loadedValue {
            SubmitReviewSheet(
                existingReview: order.review,
                isSubmitting: vm.reviewState.isSubmitting,
                errorMessage: vm.reviewState.errorMessage,
                onConfirm: { rating, comment, tags in
                    Task {
                        await vm.submitReview(
                            rating: rating,
                            comment: comment,
                            tags: tags,
                            isEdit: order.review != nil
                        )
                    }
                },
                onDismiss: {
                    if !vm.reviewState.isSubmitting {
                        showReviewSheet = false
                        vm.dismissReviewError()
                    }
                },
                // A prompt the customer did not ask for offers "Not now"; the card they tapped
                // themselves offers "Cancel". Same sheet, honest about which one it is.
                dismissLabel: reviewAutoOpened ? L10n.OrderReview.promptNotNow : L10n.OrderReview.cancel
            )
            .snackbarHost(snackbar, bottomInset: SnackbarController.defaultBottomInset)
        }
    }

    @ViewBuilder
    private func receiptPreview(_ url: URL) -> some View {
        #if canImport(UIKit)
            QuickLookPreview(url: url, deleteOnDismiss: true) {
                receiptURL = nil
            }
            .ignoresSafeArea()
        #else
            EmptyView()
        #endif
    }
}

private struct ReceiptFile: Identifiable {
    let url: URL
    var id: String {
        url.path
    }
}

enum OrderRecurringConfirm {
    /// A recurring-generated order awaiting customer confirmation: it carries a
    /// `recurringTemplateId` and its payment status is Pending (value 1).
    static func needsConfirmation(_ order: CustomerOrderDetail) -> Bool {
        guard let templateId = order.recurringTemplateId, !templateId.isBlank else { return false }
        return order.paymentStatus?.value == 1
    }
}

private struct ConfirmRecurringFooter: View {
    let submitting: Bool
    let onConfirm: () -> Void

    var body: some View {
        VStack {
            CleansiaPrimaryButton(
                L10n.Recurring.confirmCta,
                leadingIcon: "checkmark.circle",
                loading: submitting,
                enabled: !submitting,
                action: onConfirm
            )
        }
        .padding(.horizontal, Spacing.m)
        .padding(.vertical, Spacing.s)
        .frame(maxWidth: .infinity)
        .background(CleansiaColors.surface.ignoresSafeArea(edges: .bottom))
    }
}

/// Which of the footer's four CTAs a given order offers (`canRebook` /
/// `canMakeRecurring` in `OrderDetailScreen.kt`). Pulled out of the view so
/// the gating is checkable: "Book again on a cancelled order" and "the Plus-only
/// recurring CTA shown to a free customer" both render perfectly and are both wrong.
enum OrderDetailFooterActions {
    /// Only a finished cleaning is worth repeating. Deliberately NOT widened to
    /// Cancelled: a cancelled order never happened, so there is no delivered
    /// service to book again.
    static func showRebook(_ status: OrderStatus?) -> Bool {
        OrderStatusGroup.isCompleted(status)
    }

    /// Same Completed-only gate as rebook, plus the authoring half of Plus —
    /// recurring bookings are a membership perk. The gate resolves permissively
    /// from a nullable membership: a resolved non-member is walked into no
    /// paywall, and an answer still in flight costs a member nothing, because
    /// the server refuses an unentitled create with its own localized message.
    static func showMakeRecurring(_ status: OrderStatus?, authoring: RecurringAuthoringGate) -> Bool {
        showRebook(status) && authoring == .allowed
    }

    /// The footer renders if any of its four actions would. Completed used to
    /// arrive here only via `isReportable`, which is an accident of the dispute
    /// window happening to extend past completion rather than a statement about
    /// re-booking; naming all four keeps the gate honest if that window narrows.
    static func showFooter(_ status: OrderStatus?, authoring: RecurringAuthoringGate) -> Bool {
        OrderStatusGroup.isCancellable(status)
            || OrderStatusGroup.isReportable(status)
            || showRebook(status)
            || showMakeRecurring(status, authoring: authoring)
    }
}

/// Glyph + tint for the footer's three outlined actions, hoisted out of the view
/// because both are plain arguments there and so invisible to every check
/// available without a snapshot harness — `OutlinedButtonColorsTests` proves the
/// component honours whatever colour it is handed, never which colour this
/// screen hands it.
///
/// Report issue is error-tinted by owner decision although it destroys nothing.
/// It borrows the destructive *palette*, deliberately not the destructive
/// *component*: `CleansiaDangerButton` is a `Button(role: .destructive)`, and
/// claiming that role for a form that files a complaint would put a false
/// promise in the accessibility tree to settle a colour question. It also stays
/// outlined rather than filled so it cannot out-rank the primary Book again CTA
/// above it on a completed order.
///
/// Cancel carries the same tint, and Confirmed is the one status that offers
/// both, so on that screen the glyphs are the entire differentiator between
/// cancelling a booking and filing a complaint.
struct OrderDetailFooterStyle {
    let icon: String
    let tint: Color

    static let makeRecurring = Self(icon: "calendar", tint: CleansiaColors.primary)
    static let cancel = Self(icon: "xmark.circle", tint: CleansiaColors.error)
    static let reportIssue = Self(icon: "exclamationmark.triangle", tint: CleansiaColors.error)
}

/// The order-detail footer (`ActionsFooter` in `OrderDetailScreen.kt`).
/// Several actions overlap on one status, so they are stacked in Android's order
/// rather than each owning its own footer: Book again (primary) on top, then
/// Make recurring, then Cancel, then Report issue.
private struct OrderDetailActionsFooter: View {
    let showRebook: Bool
    let showMakeRecurring: Bool
    let showCancel: Bool
    let showReportIssue: Bool
    let cancelEnabled: Bool
    let onRebook: () -> Void
    let onMakeRecurring: () -> Void
    let onCancel: () -> Void
    let onReportIssue: () -> Void

    var body: some View {
        VStack(spacing: Spacing.s) {
            if showRebook {
                CleansiaPrimaryButton(
                    L10n.OrderDetail.actionRebook,
                    leadingIcon: "arrow.clockwise",
                    action: onRebook
                )
            }
            if showMakeRecurring {
                CleansiaOutlinedButton(
                    L10n.OrderDetail.actionMakeRecurring,
                    leadingIcon: OrderDetailFooterStyle.makeRecurring.icon,
                    contentColor: OrderDetailFooterStyle.makeRecurring.tint,
                    action: onMakeRecurring
                )
            }
            if showCancel {
                CleansiaOutlinedButton(
                    L10n.OrderDetail.actionCancel,
                    leadingIcon: OrderDetailFooterStyle.cancel.icon,
                    contentColor: OrderDetailFooterStyle.cancel.tint,
                    enabled: cancelEnabled,
                    action: onCancel
                )
            }
            if showReportIssue {
                CleansiaOutlinedButton(
                    L10n.OrderDetail.actionReportIssue,
                    leadingIcon: OrderDetailFooterStyle.reportIssue.icon,
                    contentColor: OrderDetailFooterStyle.reportIssue.tint,
                    action: onReportIssue
                )
            }
        }
        .padding(.horizontal, Spacing.m)
        .padding(.vertical, Spacing.s)
        .frame(maxWidth: .infinity)
        .background(CleansiaColors.surface.ignoresSafeArea(edges: .bottom))
    }
}

private struct OrderDetailErrorView: View {
    let error: ApiError
    let onRetry: () -> Void

    var body: some View {
        VStack(spacing: Spacing.m) {
            Image(systemName: "wifi.slash")
                .font(.system(size: 48))
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(L10n.OrderDetail.errorTitle)
                .font(CleansiaTypography.titleLarge)
                .foregroundColor(CleansiaColors.onBackground)
            Text(L10n.OrderDetail.errorMessage)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
            CleansiaPrimaryButton(L10n.OrderDetail.errorRetry, action: onRetry)
                .fixedSize()
        }
        .padding(Spacing.xl)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(CleansiaColors.background.ignoresSafeArea())
    }
}
