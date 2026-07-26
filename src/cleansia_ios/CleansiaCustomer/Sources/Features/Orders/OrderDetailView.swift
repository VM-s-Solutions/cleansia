import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct OrderDetailView: View {
    @StateObject private var vm: OrderDetailViewModel
    @State private var showCancelSheet = false
    @State private var showReviewSheet = false
    @State private var showPhotos = false
    @State private var receiptURL: ReceiptFile?

    private let routeOrderId: String
    private let client: OrderClient
    private let snackbar: SnackbarController
    private let paymentSheet: PaymentSheetPresenting
    private let onReportIssue: (String) -> Void
    private let onRebook: (String) -> Void
    private let onMakeRecurring: (String) -> Void
    /// Read from the shell, which already observes the membership repository and
    /// warms it in `prefetch()`. Deliberately not refreshed here: a network call
    /// on every order open to decide one button's visibility is a bad trade, and
    /// Android has the identical cold-deep-link exposure.
    private let hasMembership: Bool

    init(
        orderId: String,
        client: OrderClient,
        repository: OrderRepository,
        snackbar: SnackbarController,
        eventBus: OrderEventBus,
        paymentSheet: PaymentSheetPresenting,
        hasMembership: Bool,
        onReportIssue: @escaping (String) -> Void,
        onRebook: @escaping (String) -> Void,
        onMakeRecurring: @escaping (String) -> Void
    ) {
        _vm = StateObject(
            wrappedValue: OrderDetailViewModel(
                orderId: orderId,
                client: client,
                repository: repository,
                snackbar: snackbar,
                eventBus: eventBus
            )
        )
        routeOrderId = orderId
        self.client = client
        self.snackbar = snackbar
        self.paymentSheet = paymentSheet
        self.hasMembership = hasMembership
        self.onReportIssue = onReportIssue
        self.onRebook = onRebook
        self.onMakeRecurring = onMakeRecurring
    }

    var body: some View {
        content
            .navigationTitle(navigationTitle)
            .navigationBarTitleDisplayMode(.inline)
            .background(CleansiaColors.background.ignoresSafeArea())
            .task { await vm.load() }
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

    /// `OrderItem.id` is optional on the wire, so fall back to the id this
    /// screen was routed with — they are the same order, and "" is never a
    /// usable id for the photos screen or the dispute route.
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

                if OrderRecurringConfirm.needsConfirmation(order) {
                    ConfirmRecurringFooter(submitting: vm.confirmRecurringState.isSubmitting) {
                        Task { await vm.confirmRecurring() }
                    }
                } else if OrderDetailFooterActions.showFooter(order.status, hasMembership: hasMembership) {
                    OrderDetailActionsFooter(
                        showRebook: OrderDetailFooterActions.showRebook(order.status),
                        showMakeRecurring: OrderDetailFooterActions.showMakeRecurring(
                            order.status,
                            hasMembership: hasMembership
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
        }
    }

    @ViewBuilder
    private var cancelSheet: some View {
        if let order = vm.state.loadedValue {
            CancelOrderSheet(
                order: order,
                isSubmitting: vm.cancelState.isSubmitting,
                errorMessage: vm.cancelState.errorMessage,
                onReasonChanged: vm.dismissCancelError,
                onConfirm: { reason in Task { await vm.cancel(reason: reason) } },
                onDismiss: {
                    if !vm.cancelState.isSubmitting {
                        showCancelSheet = false
                        vm.dismissCancelError()
                    }
                }
            )
            .snackbarHost(snackbar, bottomInset: SnackbarController.defaultBottomInset)
        }
    }

    @ViewBuilder
    private var reviewSheet: some View {
        if let order = vm.state.loadedValue {
            SubmitReviewSheet(
                existingReview: order.review,
                isSubmitting: vm.reviewState.isSubmitting,
                errorMessage: vm.reviewState.errorMessage,
                onConfirm: { rating, comment in
                    Task { await vm.submitReview(rating: rating, comment: comment, isEdit: order.review != nil) }
                },
                onDismiss: {
                    if !vm.reviewState.isSubmitting {
                        showReviewSheet = false
                        vm.dismissReviewError()
                    }
                }
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
    static func needsConfirmation(_ order: OrderItem) -> Bool {
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
/// `canMakeRecurring`, `OrderDetailScreen.kt:243-250`). Pulled out of the view so
/// the gating is checkable: "Book again on a cancelled order" and "the Plus-only
/// recurring CTA shown to a free customer" both render perfectly and are both wrong.
enum OrderDetailFooterActions {
    /// Only a finished cleaning is worth repeating. Deliberately NOT widened to
    /// Cancelled: a cancelled order never happened, so there is no delivered
    /// service to book again.
    static func showRebook(_ status: OrderStatus?) -> Bool {
        OrderStatusGroup.isCompleted(status)
    }

    /// Same Completed-only gate as rebook, plus the Plus gate — recurring
    /// bookings are a membership perk, so offering the CTA without one would
    /// walk the customer into a paywall from a button that promised a schedule.
    static func showMakeRecurring(_ status: OrderStatus?, hasMembership: Bool) -> Bool {
        showRebook(status) && hasMembership
    }

    /// The footer renders if any of its four actions would. Completed used to
    /// arrive here only via `isReportable`, which is an accident of the dispute
    /// window happening to extend past completion rather than a statement about
    /// re-booking; naming all four keeps the gate honest if that window narrows.
    static func showFooter(_ status: OrderStatus?, hasMembership: Bool) -> Bool {
        OrderStatusGroup.isCancellable(status)
            || OrderStatusGroup.isReportable(status)
            || showRebook(status)
            || showMakeRecurring(status, hasMembership: hasMembership)
    }
}

/// The order-detail footer (`ActionsFooter`, `OrderDetailScreen.kt:393-500`).
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
                    leadingIcon: "calendar",
                    action: onMakeRecurring
                )
                .tint(CleansiaColors.primary)
            }
            if showCancel {
                CleansiaOutlinedButton(
                    L10n.OrderDetail.actionCancel,
                    leadingIcon: "xmark.circle",
                    enabled: cancelEnabled,
                    action: onCancel
                )
                .tint(CleansiaColors.error)
            }
            if showReportIssue {
                CleansiaOutlinedButton(
                    L10n.OrderDetail.actionReportIssue,
                    leadingIcon: "exclamationmark.triangle",
                    action: onReportIssue
                )
                .tint(CleansiaColors.primary)
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
