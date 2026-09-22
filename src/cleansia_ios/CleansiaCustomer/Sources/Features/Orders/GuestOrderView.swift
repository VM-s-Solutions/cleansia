import CleansiaCore
import SwiftUI

struct GuestOrderView: View {
    @StateObject private var vm: GuestOrderViewModel
    @Environment(\.locale) private var locale
    let onBack: () -> Void

    @State private var number = ""
    @State private var email = ""
    @State private var code = ""

    init(makeViewModel: @escaping () -> GuestOrderViewModel, onBack: @escaping () -> Void) {
        _vm = StateObject(wrappedValue: makeViewModel())
        self.onBack = onBack
    }

    private var isSubmitting: Bool {
        vm.cancelState.isSubmitting
    }

    private var cancellationPresented: Binding<Bool> {
        Binding(
            get: { vm.isCancellationPresented },
            set: { if !$0 { vm.dismissCancellation() } }
        )
    }

    var body: some View {
        GuestOrderContent(
            state: vm.state,
            isSubmitting: isSubmitting,
            number: $number,
            email: $email,
            code: $code,
            locale: locale,
            onBack: onBack,
            onCredentialsChanged: vm.onCredentialsChanged,
            onLookup: { Task { await vm.lookup(number: number, email: email, code: code) } },
            onCancel: { Task { await vm.openCancellation() } }
        )
        .onChange(of: vm.state) { state in
            guard case .cancelled = state else { return }
            number = ""
            email = ""
            code = ""
        }
        .sheet(isPresented: cancellationPresented) { cancelSheet }
    }

    private var cancelSheet: some View {
        CancelOrderSheet(
            quote: vm.quote,
            currencyCode: vm.quote.loadedValue?.currencyCode ?? vm.state.loadedOrder?.currencyCode,
            isSubmitting: isSubmitting,
            errorMessage: vm.cancelState.errorMessage,
            onReasonChanged: {},
            onRetryQuote: { Task { await vm.loadQuote() } },
            onConfirm: { reason in Task { await vm.cancel(reason: reason) } },
            onDismiss: vm.dismissCancellation,
            requiresQuote: true
        )
    }
}

private struct GuestOrderContent: View {
    let state: GuestOrderUiState
    let isSubmitting: Bool
    @Binding var number: String
    @Binding var email: String
    @Binding var code: String
    let locale: Locale
    let onBack: () -> Void
    let onCredentialsChanged: () -> Void
    let onLookup: () -> Void
    let onCancel: () -> Void

    private var canLookup: Bool {
        !isSubmitting && state != .loading && !number.isBlank && !email.isBlank && !code.isBlank
    }

    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: Spacing.xs) {
                Button(action: onBack) {
                    Image(systemName: "chevron.backward")
                        .font(.system(size: 18, weight: .semibold))
                        .foregroundColor(CleansiaColors.onBackground)
                }
                .accessibilityLabel(L10n.Auth.back)
                .disabled(isSubmitting)
                Text(L10n.GuestOrder.title)
                    .cleansiaFont(CleansiaTypography.headlineSmall)
                    .foregroundColor(CleansiaColors.onBackground)
                Spacer()
            }
            .padding(Spacing.s)

            ScrollView {
                VStack(alignment: .leading, spacing: Spacing.s) {
                    Text(L10n.GuestOrder.intro)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    credentialFields
                    CleansiaPrimaryButton(
                        L10n.GuestOrder.lookup,
                        loading: state == .loading,
                        enabled: canLookup,
                        action: onLookup
                    )
                    outcome
                }
                .padding(.horizontal, Spacing.l)
                .padding(.vertical, Spacing.m)
            }
        }
        .background(CleansiaColors.background.ignoresSafeArea())
    }

    private var credentialFields: some View {
        VStack(spacing: Spacing.xs) {
            CleansiaTextField(
                value: $number.onChange(onCredentialsChanged),
                label: L10n.GuestOrder.number,
                enabled: !isSubmitting
            )
            CleansiaTextField(
                value: $email.onChange(onCredentialsChanged),
                label: L10n.Auth.email,
                keyboardType: .emailAddress,
                textContentType: .emailAddress,
                enabled: !isSubmitting
            )
            CleansiaTextField(
                value: $code.onChange(onCredentialsChanged),
                label: L10n.GuestOrder.code,
                textContentType: .oneTimeCode,
                enabled: !isSubmitting
            )
        }
    }

    @ViewBuilder
    private var outcome: some View {
        switch state {
        case .empty:
            Text(L10n.GuestOrder.empty)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        case .loading:
            Text(L10n.GuestOrder.loading)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        case let .error(message):
            Text(message)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.error)
        case let .cancelled(message):
            Text(message)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.primary)
        case let .loaded(order):
            GuestOrderCard(order: order, isSubmitting: isSubmitting, locale: locale, onCancel: onCancel)
        }
    }
}

private struct GuestOrderCard: View {
    let order: GuestOrder
    let isSubmitting: Bool
    let locale: Locale
    let onCancel: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(order.displayOrderNumber)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
            labelled(L10n.Booking.summaryDate, OrdersFormat.dateTime(order.cleaningDateTime, locale: locale))
            labelled(L10n.OrderDetail.total, OrdersFormat.price(order.totalPrice, currencyCode: order.currencyCode))
            if let status = order.status {
                Text(L10n.Orders.statusLabel(status))
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            if order.isCancellable {
                CleansiaOutlinedButton(L10n.GuestOrder.cancel, enabled: !isSubmitting, action: onCancel)
            } else {
                Text(L10n.GuestOrder.cannotCancel)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.medium))
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.medium)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
    }

    private func labelled(_ label: String, _ value: String) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xxs) {
            Text(label)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(value)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
        }
    }
}

private extension Binding where Value == String {
    func onChange(_ handler: @escaping () -> Void) -> Binding<String> {
        Binding(
            get: { wrappedValue },
            set: { value in
                guard value != wrappedValue else { return }
                wrappedValue = value
                handler()
            }
        )
    }
}

#if DEBUG
    struct GuestOrderView_Previews: PreviewProvider {
        private static let order = GuestOrder(
            id: "o-1",
            displayOrderNumber: "CZ-123",
            cleaningDateTime: Date(timeIntervalSince1970: 1_800_000_000),
            totalPrice: 1990,
            currencyCode: "CZK",
            statusValue: 2
        )

        static var previews: some View {
            Group {
                preview(.empty).previewDisplayName("Empty")
                preview(.loading).previewDisplayName("Loading")
                preview(.error("Booking not found.")).previewDisplayName("Error")
                preview(.loaded(order)).previewDisplayName("Loaded")
                preview(.cancelled("Order cancelled.")).previewDisplayName("Cancelled")
            }
        }

        private static func preview(_ state: GuestOrderUiState) -> some View {
            GuestOrderContent(
                state: state,
                isSubmitting: false,
                number: .constant("CZ-123"),
                email: .constant("guest@example.test"),
                code: .constant("ABCD"),
                locale: Locale(identifier: "en"),
                onBack: {},
                onCredentialsChanged: {},
                onLookup: {},
                onCancel: {}
            )
        }
    }
#endif
