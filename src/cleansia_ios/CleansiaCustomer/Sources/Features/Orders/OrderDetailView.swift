import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct OrderDetailView: View {
    @StateObject private var vm: OrderDetailViewModel
    @State private var showCancelSheet = false
    @State private var showReviewSheet = false
    @State private var showPhotos = false
    @State private var receiptURL: ReceiptFile?

    private let client: OrderClient
    private let snackbar: SnackbarController
    private let paymentSheet: PaymentSheetPresenting

    init(
        orderId: String,
        client: OrderClient,
        repository: OrderRepository,
        snackbar: SnackbarController,
        eventBus: OrderEventBus,
        paymentSheet: PaymentSheetPresenting
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
        self.client = client
        self.snackbar = snackbar
        self.paymentSheet = paymentSheet
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

    private var orderId: String {
        vm.state.loadedValue?.id ?? ""
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
                } else if OrderStatusGroup.isCancellable(order.status) {
                    CancelFooter(enabled: !vm.cancelState.isSubmitting) {
                        showCancelSheet = true
                    }
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

private struct CancelFooter: View {
    let enabled: Bool
    let onCancel: () -> Void

    var body: some View {
        VStack {
            CleansiaOutlinedButton(
                L10n.OrderDetail.actionCancel,
                leadingIcon: "xmark.circle",
                enabled: enabled,
                action: onCancel
            )
            .tint(CleansiaColors.error)
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
