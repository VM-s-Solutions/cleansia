import CleansiaCore
import SwiftUI

struct PaymentsView: View {
    @StateObject private var vm: PaymentsViewModel
    @State private var cardToRemove: SavedCard?
    @State private var restartingCardSetup = false
    /// The card a confirmed removal is running for, so its row can show the spinner the dialog used to.
    @State private var removingCardId: String?
    @Environment(\.openURL) private var openURL
    @Environment(\.scenePhase) private var scenePhase
    private let paymentSheet: PaymentSheetPresenting

    init(
        savedCardClient: SavedCardClient,
        receivableClient: ReceivableClient,
        snackbar: SnackbarController,
        paymentSheet: PaymentSheetPresenting,
        market: Market?,
        pendingCards: SavedCardCaptureStore
    ) {
        _vm = StateObject(wrappedValue: PaymentsViewModel(
            savedCardClient: savedCardClient,
            receivableClient: receivableClient,
            snackbar: snackbar,
            countryId: market?.countryId,
            currencyCode: market?.currencyCode,
            pendingCards: pendingCards
        ))
        self.paymentSheet = paymentSheet
    }

    var body: some View {
        PaymentsContent(
            state: vm.state,
            removingCardId: vm.removeState.isSubmitting ? removingCardId : nil,
            paying: vm.payState.isSubmitting,
            offersCardCapture: vm.offersCardCapture,
            cardConsentAccepted: vm.cardConsentAccepted,
            addingCard: vm.addCardState.isSubmitting,
            cardCapture: vm.cardCapture,
            onRetry: { Task { await vm.load() } },
            onPay: { receivable in Task { await vm.pay(receivable) } },
            onRemoveRequested: { cardToRemove = $0 },
            onCardConsentChanged: vm.setCardConsentAccepted,
            onAddCard: { Task { await vm.addCard() } },
            onCheckCard: { Task { await vm.checkSavedCard() } },
            onContinueCard: vm.continueCardSetup,
            onRestartCard: { restartingCardSetup = true }
        )
        .navigationTitle(L10n.Payments.title)
        .navigationBarTitleDisplayMode(.inline)
        // The system confirm closes on the tap; the row shows the removal running and a refusal is the
        // snackbar's, so nothing holds a dialog open over a request.
        .alert(
            L10n.Payments.cardRemoveTitle,
            isPresented: Binding(get: { cardToRemove != nil }, set: { if !$0 { cardToRemove = nil } }),
            presenting: cardToRemove
        ) { card in
            Button(L10n.Payments.cardRemoveConfirm, role: .destructive) {
                removingCardId = card.id
                Task { await vm.remove(card) }
            }
            Button(L10n.cancel, role: .cancel) {}
        } message: { _ in
            Text(L10n.Payments.cardRemoveMessage)
        }
        .alert(L10n.Payments.cardRestartTitle, isPresented: $restartingCardSetup) {
            Button(L10n.Payments.cardRestartAction, role: .destructive) { vm.restartCardSetup() }
            Button(L10n.cancel, role: .cancel) {}
        } message: {
            Text(L10n.Payments.cardRestartMessage)
        }
        .task { await vm.load() }
        .onReceive(vm.payLinks) { openURL($0) }
        .onReceive(vm.cardSetups) { request in
            Task { @MainActor in
                guard vm.claimCardSheet(request.attempt) else { return }
                let outcome = await paymentSheet.present(request.presentation)
                await vm.cardSheetFinished(outcome, attempt: request.attempt)
            }
        }
        .onChange(of: scenePhase) { phase in
            if phase == .active {
                Task { await vm.onResumed() }
            }
        }
    }
}

struct PaymentsContent: View {
    let state: UiState<PaymentsSnapshot>
    let removingCardId: String?
    let paying: Bool
    let offersCardCapture: Bool
    let cardConsentAccepted: Bool
    let addingCard: Bool
    let cardCapture: SavedCardCapture?
    let onRetry: () -> Void
    let onPay: (Receivable) -> Void
    let onRemoveRequested: (SavedCard) -> Void
    let onCardConsentChanged: (Bool) -> Void
    let onAddCard: () -> Void
    let onCheckCard: () -> Void
    let onContinueCard: () -> Void
    let onRestartCard: () -> Void

    var body: some View {
        ZStack {
            CleansiaColors.background.ignoresSafeArea()
            content
        }
    }

    @ViewBuilder
    private var content: some View {
        switch state {
        case .loading:
            VStack(spacing: Spacing.m) {
                ProgressView().frame(maxWidth: .infinity, maxHeight: .infinity)
                captureBlock.padding(Spacing.ml)
            }
        case .error:
            VStack(spacing: Spacing.m) {
                PaymentsErrorState(onRetry: onRetry)
                captureBlock.padding(Spacing.ml)
            }
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
                            SavedCardRow(
                                card: card,
                                removing: removingCardId == card.id,
                                canRemove: removingCardId == nil,
                                onRemove: { onRemoveRequested(card) }
                            )
                        }
                    }
                    captureBlock
                    if offersCardCapture {
                        AddCardBlock(
                            consentAccepted: cardConsentAccepted,
                            adding: addingCard,
                            onConsentChanged: onCardConsentChanged,
                            onAdd: onAddCard
                        )
                    }
                }
                .padding(Spacing.ml)
            }
        }
    }

    @ViewBuilder
    private var captureBlock: some View {
        if let cardCapture {
            PendingCardBlock(
                capture: cardCapture, onCheck: onCheckCard,
                onContinue: onContinueCard, onRestart: onRestartCard
            )
        }
    }
}

private struct PendingCardBlock: View {
    let capture: SavedCardCapture
    let onCheck: () -> Void
    let onContinue: () -> Void
    let onRestart: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            Label(L10n.Payments.cardPendingTitle, systemImage: "creditcard")
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
            Text(L10n.Payments.cardAddPending)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .fixedSize(horizontal: false, vertical: true)
            switch capture.stage {
            case .starting, .presentingSheet:
                ProgressView().tint(CleansiaColors.primary)
            case .awaitingSheet:
                PendingCardButton(title: L10n.Booking.continueAction, primary: true, action: onContinue)
            case .pending, .verifying:
                PendingCardButton(
                    title: L10n.Payments.cardCheckAction, primary: true,
                    loading: capture.stage.isBusy,
                    enabled: !capture.stage.isBusy,
                    action: onCheck
                )
            }
            if capture.stage != .starting {
                PendingCardButton(title: L10n.Payments.cardRestartAction, primary: false, action: onRestart)
            }
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.large))
        .overlay(RoundedRectangle(cornerRadius: CornerRadius.large).stroke(CleansiaColors.outlineVariant, lineWidth: 1))
    }
}

private struct PendingCardButton: View {
    @Environment(\.dynamicTypeSize) private var textSize
    let title: String
    let primary: Bool
    var loading = false
    var enabled = true
    let action: () -> Void

    var body: some View {
        Group {
            if textSize.isAccessibilitySize {
                Button(action: action) {
                    Group {
                        if loading {
                            ProgressView().tint(primary ? CleansiaColors.onPrimary : CleansiaColors.onSurface)
                        } else {
                            Text(title)
                                .font(CleansiaTypography.titleMedium)
                                .multilineTextAlignment(.center)
                                .fixedSize(horizontal: false, vertical: true)
                        }
                    }
                    .frame(maxWidth: .infinity, minHeight: 56)
                    .padding(.horizontal, Spacing.m)
                    .padding(.vertical, Spacing.xs)
                    .foregroundColor(primary ? CleansiaColors.onPrimary : CleansiaColors.onSurface)
                    .background(primary ? CleansiaColors.primary : CleansiaColors.surface)
                    .clipShape(Capsule())
                    .overlay(Capsule().stroke(primary ? Color.clear : CleansiaColors.outline, lineWidth: 1))
                }
                .disabled(!enabled || loading)
            } else if primary {
                CleansiaPrimaryButton(title, loading: loading, enabled: enabled, action: action)
            } else {
                CleansiaOutlinedButton(title, enabled: enabled, action: action)
            }
        }
        .accessibilityLabel(title)
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
    let removing: Bool
    /// One removal at a time: the view model drops a second while the first is in flight.
    let canRemove: Bool
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
            if removing {
                ProgressView()
                    .frame(width: 44, height: 44)
            } else {
                Button(action: onRemove) {
                    Image(systemName: "trash")
                        .foregroundColor(CleansiaColors.error.opacity(canRemove ? 1 : 0.4))
                        .frame(width: 44, height: 44)
                }
                .buttonStyle(.plain)
                .disabled(!canRemove)
                .accessibilityLabel(L10n.Payments.cardRemoveAction)
            }
        }
        .padding(Spacing.m)
        .background(CleansiaColors.surface)
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
    }
}

private struct AddCardBlock: View {
    let consentAccepted: Bool
    let adding: Bool
    let onConsentChanged: (Bool) -> Void
    let onAdd: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            SectionIntro(text: L10n.Payments.cardAddNote)
            CleansiaConsentCheckbox(
                checked: Binding(get: { consentAccepted }, set: onConsentChanged),
                markdown: L10n.Booking.savedCardConsent,
                toggleAccessibilityLabel: L10n.Booking.cardGuaranteeTitle
            )
            CleansiaPrimaryButton(
                L10n.Payments.cardAddAction,
                size: .medium,
                loading: adding,
                enabled: consentAccepted && !adding,
                action: onAdd
            )
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
                .foregroundColor(CleansiaColors.primaryIconOnContainer)
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
                removingCardId: nil,
                paying: false,
                offersCardCapture: true,
                cardConsentAccepted: false,
                addingCard: false,
                cardCapture: nil,
                onRetry: {},
                onPay: { _ in },
                onRemoveRequested: { _ in },
                onCardConsentChanged: { _ in },
                onAddCard: {},
                onCheckCard: {},
                onContinueCard: {},
                onRestartCard: {}
            )
        }
    }
#endif
