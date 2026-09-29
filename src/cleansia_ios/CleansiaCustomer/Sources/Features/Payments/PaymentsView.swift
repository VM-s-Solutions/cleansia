import CleansiaCore
import SwiftUI

struct PaymentsView: View {
    @StateObject private var vm: PaymentsViewModel
    @State private var cardToRemove: SavedCard?
    @Environment(\.openURL) private var openURL
    @Environment(\.scenePhase) private var scenePhase

    init(savedCardClient: SavedCardClient, receivableClient: ReceivableClient, snackbar: SnackbarController) {
        _vm = StateObject(wrappedValue: PaymentsViewModel(
            savedCardClient: savedCardClient,
            receivableClient: receivableClient,
            snackbar: snackbar
        ))
    }

    var body: some View {
        PaymentsContent(
            state: vm.state,
            removeState: vm.removeState,
            paying: vm.payState.isSubmitting,
            cardToRemove: cardToRemove,
            onRetry: { Task { await vm.load() } },
            onPay: { receivable in Task { await vm.pay(receivable) } },
            onRemoveRequested: { cardToRemove = $0 },
            onRemoveConfirmed: { card in Task { await vm.remove(card) } },
            onRemoveDismissed: { cardToRemove = nil }
        )
        .navigationTitle(L10n.Payments.title)
        .navigationBarTitleDisplayMode(.inline)
        .task { await vm.load() }
        .onReceive(vm.removed) { _ in cardToRemove = nil }
        .onReceive(vm.payLinks) { openURL($0) }
        .onChange(of: scenePhase) { phase in
            if phase == .active {
                Task { await vm.onResumed() }
            }
        }
    }
}

private struct PaymentsContent: View {
    let state: UiState<PaymentsSnapshot>
    let removeState: ActionState
    let paying: Bool
    let cardToRemove: SavedCard?
    let onRetry: () -> Void
    let onPay: (Receivable) -> Void
    let onRemoveRequested: (SavedCard) -> Void
    let onRemoveConfirmed: (SavedCard) -> Void
    let onRemoveDismissed: () -> Void

    var body: some View {
        ZStack {
            CleansiaColors.background.ignoresSafeArea()
            content
            if let card = cardToRemove {
                CleansiaDialog(
                    title: L10n.Payments.cardRemoveTitle,
                    confirmLabel: L10n.Payments.cardRemoveConfirm,
                    onConfirm: { onRemoveConfirmed(card) },
                    onDismiss: onRemoveDismissed,
                    message: removeState.errorMessage ?? L10n.Payments.cardRemoveMessage,
                    dismissLabel: L10n.cancel,
                    icon: "trash",
                    destructive: true,
                    confirmEnabled: !removeState.isSubmitting
                )
            }
        }
    }

    @ViewBuilder
    private var content: some View {
        switch state {
        case .loading:
            ProgressView()
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .error:
            PaymentsErrorState(onRetry: onRetry)
        case let .loaded(snapshot):
            ScrollView {
                VStack(alignment: .leading, spacing: Spacing.s) {
                    if !snapshot.receivables.isEmpty {
                        SectionTitle(text: L10n.Payments.dueTitle)
                        SectionIntro(text: L10n.Payments.dueIntro)
                        ForEach(snapshot.receivables) { receivable in
                            ReceivableCard(receivable: receivable, paying: paying, onPay: { onPay(receivable) })
                        }
                        Spacer().frame(height: Spacing.xs)
                    }
                    SectionTitle(text: L10n.Payments.cardTitle)
                    SectionIntro(text: L10n.Payments.cardIntro)
                    if snapshot.cards.isEmpty {
                        SectionIntro(text: L10n.Payments.cardEmpty)
                    } else {
                        ForEach(snapshot.cards) { card in
                            SavedCardRow(card: card, onRemove: { onRemoveRequested(card) })
                        }
                    }
                }
                .padding(Spacing.ml)
            }
        }
    }
}

private struct SectionTitle: View {
    let text: String

    var body: some View {
        Text(text)
            .font(CleansiaTypography.titleMedium)
            .foregroundColor(CleansiaColors.onSurface)
    }
}

private struct SectionIntro: View {
    let text: String

    var body: some View {
        Text(text)
            .font(CleansiaTypography.bodyMedium)
            .foregroundColor(CleansiaColors.onSurfaceVariant)
            .fixedSize(horizontal: false, vertical: true)
    }
}

private struct ReceivableCard: View {
    let receivable: Receivable
    let paying: Bool
    let onPay: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            HStack(spacing: Spacing.s) {
                LeadingIcon(systemName: "doc.text")
                VStack(alignment: .leading, spacing: 2) {
                    Text(L10n.Payments.kind(receivable.kind))
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    Text(L10n.Payments.dueOrder(receivable.displayOrderNumber))
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                Spacer()
                Text(OrdersFormat.price(receivable.amount, currencyCode: receivable.currencyCode))
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
            }
            CleansiaPrimaryButton(
                L10n.Payments.payAction,
                size: .medium,
                loading: paying,
                enabled: !paying,
                action: onPay
            )
        }
        .padding(Spacing.m)
        .background(CleansiaColors.surface)
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
    }
}

private struct SavedCardRow: View {
    let card: SavedCard
    let onRemove: () -> Void

    private var expiry: String {
        L10n.Payments.cardExpires(month: card.expMonth, year: card.expYear, currencyCode: card.currencyCode)
    }

    var body: some View {
        HStack(spacing: Spacing.s) {
            LeadingIcon(systemName: "creditcard")
            VStack(alignment: .leading, spacing: 2) {
                Text(L10n.Payments.cardLabel(brand: card.brand.capitalized, last4: card.last4))
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                Text(expiry)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            Spacer()
            Button(action: onRemove) {
                Image(systemName: "trash")
                    .foregroundColor(CleansiaColors.error)
                    .frame(width: 44, height: 44)
            }
            .buttonStyle(.plain)
            .accessibilityLabel(L10n.Payments.cardRemoveAction)
        }
        .padding(Spacing.m)
        .background(CleansiaColors.surface)
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
    }
}

private struct LeadingIcon: View {
    let systemName: String

    var body: some View {
        ZStack {
            Circle()
                .fill(CleansiaColors.primaryContainer)
                .frame(width: 44, height: 44)
            Image(systemName: systemName)
                .foregroundColor(CleansiaColors.primary)
        }
    }
}

private struct PaymentsErrorState: View {
    let onRetry: () -> Void

    var body: some View {
        VStack(spacing: Spacing.m) {
            Image(systemName: "icloud.slash")
                .font(.system(size: 44))
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(L10n.Payments.errorMessage)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
            CleansiaOutlinedButton(L10n.retry, size: .medium, action: onRetry)
                .fixedSize()
        }
        .padding(Spacing.xl)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

#if DEBUG
    struct PaymentsContent_Previews: PreviewProvider {
        static var previews: some View {
            PaymentsContent(
                state: .loaded(PaymentsSnapshot(
                    receivables: [Receivable(
                        id: "rcv-1",
                        orderId: "ord-1",
                        displayOrderNumber: "CL-1042",
                        kind: .cancellationFee,
                        amount: 450,
                        currencyCode: "CZK",
                        createdOn: Date()
                    )],
                    cards: [SavedCard(
                        id: "card-1",
                        brand: "visa",
                        last4: "4242",
                        expMonth: 4,
                        expYear: 2029,
                        currencyCode: "CZK"
                    )]
                )),
                removeState: .idle,
                paying: false,
                cardToRemove: nil,
                onRetry: {},
                onPay: { _ in },
                onRemoveRequested: { _ in },
                onRemoveConfirmed: { _ in },
                onRemoveDismissed: {}
            )
        }
    }
#endif
