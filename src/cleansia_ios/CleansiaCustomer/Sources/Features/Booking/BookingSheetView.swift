import CleansiaCore
import SwiftUI

struct BookingSheetView: View {
    @ObservedObject var vm: BookingViewModel
    @Environment(\.snackbarController) private var snackbar
    @State private var success: BookingSuccess?
    @State private var slideResetCount = 0
    let geocoding: GeocodingService
    let mapProvider: MapProvider
    let serviceArea: ServiceAreaProvider?
    let paymentSheet: PaymentSheetPresenting
    let orderClient: OrderClient
    let warmOrders: @Sendable () async -> Void
    let onDismiss: () -> Void
    let onViewOrder: (String) -> Void
    let onCompleteProfile: () -> Void

    private static let footerSnackbarInset: CGFloat = 88

    init(
        vm: BookingViewModel,
        geocoding: GeocodingService,
        mapProvider: MapProvider,
        serviceArea: ServiceAreaProvider? = nil,
        paymentSheet: PaymentSheetPresenting,
        orderClient: OrderClient,
        warmOrders: @escaping @Sendable () async -> Void = {},
        onDismiss: @escaping () -> Void,
        onViewOrder: @escaping (String) -> Void = { _ in },
        onCompleteProfile: @escaping () -> Void = {}
    ) {
        self.vm = vm
        self.geocoding = geocoding
        self.mapProvider = mapProvider
        self.serviceArea = serviceArea
        self.paymentSheet = paymentSheet
        self.orderClient = orderClient
        self.warmOrders = warmOrders
        self.onDismiss = onDismiss
        self.onViewOrder = onViewOrder
        self.onCompleteProfile = onCompleteProfile
    }

    var body: some View {
        Group {
            if let success {
                BookingSuccessView(
                    confirmationCode: success.confirmationCode,
                    orderId: success.orderId,
                    loadOrder: { [orderClient] id in
                        if case let .success(order) = await orderClient.getById(orderId: id) {
                            return order
                        }
                        return nil
                    },
                    warmOrders: warmOrders,
                    onViewOrder: success.orderId.isBlank ? nil : {
                        vm.reset()
                        self.success = nil
                        onViewOrder(success.orderId)
                    },
                    onDone: {
                        vm.reset()
                        self.success = nil
                        onDismiss()
                    }
                )
            } else {
                BookingSheetContent(
                    viewModel: vm,
                    geocoding: geocoding,
                    mapProvider: mapProvider,
                    serviceArea: serviceArea,
                    slideResetTrigger: slideResetCount,
                    onLeading: {
                        if !vm.back() { onDismiss() }
                    },
                    onContinue: { vm.advance() },
                    onConfirm: submit
                )
            }
        }
        .overlay {
            BusyMascotOverlay(
                visible: vm.submitState.isSubmitting,
                message: L10n.Booking.busyBooking
            )
        }
        .onReceive(vm.events) { event in
            switch event {
            case .selectionPrunedForMarket:
                snackbar.showInfo(L10n.Booking.marketSelectionPruned)
            case .cashCleared:
                snackbar.showInfo(L10n.Booking.cashCleared)
            }
        }
        .snackbarHost(snackbar, bottomInset: Self.footerSnackbarInset)
        .presentationDetents([.large])
        .presentationDragIndicator(.visible)
        // Swiping down closes the sheet on any step: the draft is the session's view model, so the Book
        // button reopens it where it was left. Only a booking being placed holds it, so its outcome has
        // somewhere to land.
        .interactiveDismissDisabled(vm.submitState.isSubmitting)
    }

    private func submit() async {
        await handle(vm.submit())
    }

    private func handle(_ outcome: BookingSubmitOutcome) async {
        switch outcome {
        case let .success(orderId, confirmationCode):
            success = BookingSuccess(orderId: orderId, confirmationCode: confirmationCode)
        case let .cardPending(orderId, confirmationCode, presentation):
            await presentPaymentSheet(presentation, orderId: orderId, confirmationCode: confirmationCode)
        case .profileIncomplete:
            slideResetCount += 1
            onCompleteProfile()
        case .paymentMethodCleared:
            slideResetCount += 1
        case let .cardGuaranteeNeeded(presentation):
            await saveCardGuarantee(presentation)
        case .cardGuaranteeConsentRequired:
            slideResetCount += 1
            snackbar.showError(L10n.Booking.cardGuaranteeConsentRequired)
        case .cardGuaranteePending:
            slideResetCount += 1
            snackbar.showInfo(L10n.Booking.cardGuaranteePending)
        case .timeNoLongerHolds:
            slideResetCount += 1
            snackbar.showInfo(L10n.Booking.draftTimeChanged)
        case let .failed(error):
            slideResetCount += 1
            // Prefer the server's own business error — "no cleaner is available
            // for that slot", "the price changed", "we don't serve that city" are
            // all actionable and already translated in all five locales, where the
            // generic network line tells the customer nothing. `showApiError`
            // drops cancellations by itself, so no extra guard is needed here.
            if let error {
                snackbar.showApiError(error)
            } else {
                snackbar.showError(L10n.Booking.errorGenericNetwork)
            }
        }
    }

    /// Stripe's sheet over the booking. The app leaves the foreground under it and comes back — a 3-D Secure
    /// approval in the bank app, a look-up of the card number, a Face ID prompt — and the order or the card is
    /// past the time by then, so the open booking is not re-checked while it is up.
    private func showPaymentSheet(_ presentation: PaymentSheetPresentation) async -> PaymentSheetOutcome {
        vm.paymentSheetShowing = true
        defer { vm.paymentSheetShowing = false }
        return await paymentSheet.present(presentation)
    }

    private func saveCardGuarantee(_ presentation: PaymentSheetPresentation) async {
        switch await showPaymentSheet(presentation) {
        case .completed:
            await handle(vm.submitAfterCardGuarantee())
        case .canceled, .failed:
            vm.abandonCardGuarantee()
            slideResetCount += 1
            snackbar.showError(L10n.Booking.cardGuaranteeCancelled)
        }
    }

    private func presentPaymentSheet(
        _ presentation: PaymentSheetPresentation,
        orderId: String,
        confirmationCode: String
    ) async {
        let outcome = await showPaymentSheet(presentation)
        switch BookingCardResultResolver.resolve(outcome, confirmationCode: confirmationCode) {
        case let .navigateToSuccess(code):
            // Same duplicate-order guard as the VM's cash path: clear the
            // session-lived draft the moment the payment lands, not on the
            // success screen's exit (a swiped-away sheet skips those closures).
            vm.reset()
            success = BookingSuccess(orderId: orderId, confirmationCode: code)
        case let .snackbar(messageKey):
            snackbar.showError(L10n.localized(messageKey))
        }
    }
}

private struct BookingSuccess {
    let orderId: String
    let confirmationCode: String
}

private struct BookingSheetContent: View {
    @ObservedObject var viewModel: BookingViewModel
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    let geocoding: GeocodingService
    let mapProvider: MapProvider
    let serviceArea: ServiceAreaProvider?
    let slideResetTrigger: Int
    let onLeading: () -> Void
    let onContinue: () -> Void
    let onConfirm: () async -> Void

    /// The direction of the last step change; the page coming in enters by it.
    @State private var movingForward = true
    /// The step on screen. It follows the wizard's one run-loop turn behind (the `onChange` in `body`).
    @State private var shownStep: Int

    init(
        viewModel: BookingViewModel,
        geocoding: GeocodingService,
        mapProvider: MapProvider,
        serviceArea: ServiceAreaProvider?,
        slideResetTrigger: Int,
        onLeading: @escaping () -> Void,
        onContinue: @escaping () -> Void,
        onConfirm: @escaping () async -> Void
    ) {
        self.viewModel = viewModel
        self.geocoding = geocoding
        self.mapProvider = mapProvider
        self.serviceArea = serviceArea
        self.slideResetTrigger = slideResetTrigger
        self.onLeading = onLeading
        self.onContinue = onContinue
        self.onConfirm = onConfirm
        _shownStep = State(initialValue: viewModel.currentStep)
    }

    private var step: Int {
        shownStep
    }

    /// Which way the step change being drawn goes: on, the next step comes in from the trailing edge; back, the
    /// previous one comes in from the leading edge (Android's `AnimatedContent` twin). While the wizard has
    /// moved and the page has not yet, the page on screen is drawn with the way it is about to leave.
    private var forward: Bool {
        viewModel.currentStep == shownStep ? movingForward : viewModel.currentStep > shownStep
    }

    private var isLastStep: Bool {
        step >= BookingStepGate.totalSteps
    }

    private var isSubmitting: Bool {
        viewModel.submitState.isSubmitting
    }

    private var canContinue: Bool {
        BookingStepGate.canContinue(
            step: step,
            state: viewModel.state,
            alreadyConsented: viewModel.alreadyConsented,
            needsCardGuarantee: viewModel.needsCardGuarantee
        )
    }

    private var canConfirm: Bool {
        canContinue && !isSubmitting
    }

    /// On a card booking this is what the card is asked for — the figure the Stripe sheet then shows —
    /// so credit that applies is already off it.
    private var totalDisplay: String? {
        guard let quote = viewModel.quoteState.quote else { return nil }
        let summary = BookingPriceSummary.resolve(
            quote: quote,
            discount: viewModel.effectiveDiscount,
            payByCard: viewModel.state.paymentMethod == .card
        )
        return BookingPricing.formatTotal(summary.dueOnCard, currencyCode: quote.currencyCode)
    }

    var body: some View {
        VStack(spacing: 0) {
            header
            ProgressView(value: Double(step), total: Double(BookingStepGate.totalSteps))
                .tint(CleansiaColors.primary)
                .padding(.horizontal, Spacing.l)

            stepBody
                .frame(maxWidth: .infinity, maxHeight: .infinity)

            footer
        }
        .background(CleansiaColors.background.ignoresSafeArea())
        .task { await viewModel.loadConsentStatus() }
        // The direction is read from the step change itself, so the wizard's own step back to When — a time
        // that stopped holding, at the slide or on a return to the foreground — slides back as the back button
        // does. A page leaving animates with the transition it last rendered with, so the page drawn follows
        // the wizard one turn later, once the page on screen has been drawn with the way it leaves (`forward`).
        .onChange(of: viewModel.currentStep) { next in
            DispatchQueue.main.async {
                movingForward = next > shownStep
                shownStep = next
            }
        }
    }

    private var header: some View {
        HStack(spacing: Spacing.s) {
            Button {
                onLeading()
            } label: {
                Image(systemName: step > 1 ? "chevron.left" : "xmark")
                    .font(.system(size: 17, weight: .semibold))
                    .foregroundColor(CleansiaColors.onSurface)
                    .frame(width: 44, height: 44)
            }
            .accessibilityLabel(Text(step > 1 ? L10n.Booking.back : L10n.Booking.close))

            Text(L10n.Booking.stepTitle(step))
                .cleansiaFont(CleansiaTypography.headlineSmall)
                .foregroundColor(CleansiaColors.onBackground)
                .lineLimit(1)
                .minimumScaleFactor(0.8)
                .frame(maxWidth: .infinity, alignment: .leading)

            Text(L10n.Booking.stepIndicator(step, BookingStepGate.totalSteps))
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .lineLimit(1)
                .fixedSize()
                .contentTransition(.numericText())
                .animation(.default, value: step)
        }
        .padding(.horizontal, Spacing.m)
        .padding(.top, Spacing.l)
    }

    private var stepBody: some View {
        ZStack {
            switch step {
            case 1: ServicesStep(viewModel: viewModel)
            case 2: DirtinessStep(viewModel: viewModel)
            case 3: WhenWhereStep(
                    viewModel: viewModel,
                    geocoding: geocoding,
                    mapProvider: mapProvider,
                    serviceArea: serviceArea
                )
            default: ConfirmStep(viewModel: viewModel)
            }
        }
        .transition(stepTransition)
        .id(step)
        .animation(.easeInOut(duration: 0.28), value: step)
    }

    /// Leading and trailing follow the layout direction, so back slides the right way in either. Reduce
    /// Motion crossfades instead.
    private var stepTransition: AnyTransition {
        if reduceMotion { return .opacity }
        return .asymmetric(
            insertion: .move(edge: forward ? .trailing : .leading).combined(with: .opacity),
            removal: .move(edge: forward ? .leading : .trailing).combined(with: .opacity)
        )
    }

    private var confirmLabel: String {
        totalDisplay.map(L10n.Booking.slideToConfirmPrice) ?? L10n.Booking.slideToConfirm
    }

    private var footer: some View {
        VStack(spacing: 0) {
            if isLastStep {
                SlideToConfirm(
                    idleLabel: confirmLabel,
                    busyLabel: confirmLabel,
                    isBusy: isSubmitting,
                    enabled: canConfirm,
                    resetTrigger: slideResetTrigger,
                    style: .prominent,
                    onConfirm: { Task { await onConfirm() } }
                )
                .lineLimit(1)
                .minimumScaleFactor(0.85)
            } else {
                CleansiaPrimaryButton(
                    totalDisplay.map(L10n.Booking.continuePrice) ?? L10n.Booking.continueAction,
                    trailingIcon: "arrow.right",
                    loading: viewModel.isQuoting,
                    enabled: canContinue,
                    action: onContinue
                )
                .lineLimit(1)
                .minimumScaleFactor(0.85)
            }
        }
        .padding(.horizontal, Spacing.ml)
        .padding(.vertical, Spacing.s)
        .background(CleansiaColors.surface)
    }
}

#if DEBUG
    struct BookingSheetView_Previews: PreviewProvider {
        static var previews: some View {
            BookingSheetView(
                vm: BookingViewModel(),
                geocoding: CLGeocoderGeocodingService(),
                mapProvider: PreviewMapProvider(),
                paymentSheet: StripePaymentController(),
                orderClient: LiveOrderClient(),
                onDismiss: {}
            )
        }
    }
#endif
