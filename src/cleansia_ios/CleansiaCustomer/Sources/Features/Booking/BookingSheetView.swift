import CleansiaCore
import SwiftUI

struct BookingSheetView: View {
    @ObservedObject var vm: BookingViewModel
    @Environment(\.snackbarController) private var snackbar
    @State private var success: BookingSuccess?
    @State private var slideResetCount = 0
    let geocoding: GeocodingService
    let mapProvider: MapProvider
    let paymentSheet: PaymentSheetPresenting
    let onDismiss: () -> Void
    let onViewOrder: (String) -> Void
    let onCompleteProfile: () -> Void

    private static let footerSnackbarInset: CGFloat = 88

    init(
        vm: BookingViewModel,
        geocoding: GeocodingService,
        mapProvider: MapProvider,
        paymentSheet: PaymentSheetPresenting,
        onDismiss: @escaping () -> Void,
        onViewOrder: @escaping (String) -> Void = { _ in },
        onCompleteProfile: @escaping () -> Void = {}
    ) {
        self.vm = vm
        self.geocoding = geocoding
        self.mapProvider = mapProvider
        self.paymentSheet = paymentSheet
        self.onDismiss = onDismiss
        self.onViewOrder = onViewOrder
        self.onCompleteProfile = onCompleteProfile
    }

    var body: some View {
        Group {
            if let success {
                BookingSuccessView(
                    confirmationCode: success.confirmationCode,
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
        .snackbarHost(snackbar, bottomInset: Self.footerSnackbarInset)
        .presentationDetents([.large])
        .presentationDragIndicator(.visible)
    }

    private func submit() async {
        switch await vm.submit() {
        case let .success(orderId, confirmationCode):
            success = BookingSuccess(orderId: orderId, confirmationCode: confirmationCode)
        case let .cardPending(orderId, confirmationCode, presentation):
            await presentPaymentSheet(presentation, orderId: orderId, confirmationCode: confirmationCode)
        case .profileIncomplete:
            slideResetCount += 1
            onCompleteProfile()
        case .failed:
            slideResetCount += 1
            snackbar.showError(L10n.Booking.errorGenericNetwork)
        }
    }

    private func presentPaymentSheet(
        _ presentation: PaymentSheetPresentation,
        orderId: String,
        confirmationCode: String
    ) async {
        let outcome = await paymentSheet.present(presentation)
        switch BookingCardResultResolver.resolve(outcome, confirmationCode: confirmationCode) {
        case let .navigateToSuccess(code):
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
    let geocoding: GeocodingService
    let mapProvider: MapProvider
    let slideResetTrigger: Int
    let onLeading: () -> Void
    let onContinue: () -> Void
    let onConfirm: () async -> Void

    private var step: Int {
        viewModel.currentStep
    }

    private var isLastStep: Bool {
        step >= BookingStepGate.totalSteps
    }

    private var isSubmitting: Bool {
        viewModel.submitState.isSubmitting
    }

    private var canContinue: Bool {
        BookingStepGate.canContinue(step: step, state: viewModel.state)
    }

    private var canConfirm: Bool {
        canContinue && !isSubmitting
    }

    private var totalDisplay: String? {
        guard let quote = viewModel.quoteState.quote else { return nil }
        let promoDiscount: Double = if case let .valid(amount) = viewModel.promoState { amount } else { 0 }
        let finalTotal = BookingPricing.finalTotal(
            basePrice: quote.totalPrice,
            cleaningAt: viewModel.state.selectedInstant,
            tierDiscount: 0,
            promoDiscount: promoDiscount
        )
        return BookingPricing.formatTotal(finalTotal, currencyCode: quote.currencyCode)
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
    }

    private var header: some View {
        HStack(spacing: Spacing.s) {
            Button(action: onLeading) {
                Image(systemName: step > 1 ? "chevron.left" : "xmark")
                    .font(.system(size: 17, weight: .semibold))
                    .foregroundColor(CleansiaColors.onSurface)
                    .frame(width: 44, height: 44)
            }
            .accessibilityLabel(Text(step > 1 ? L10n.Booking.back : L10n.Booking.close))

            Text(L10n.Booking.stepTitle(step))
                .font(CleansiaTypography.headlineSmall)
                .foregroundColor(CleansiaColors.onBackground)
                .frame(maxWidth: .infinity, alignment: .leading)

            Text(L10n.Booking.stepIndicator(step, BookingStepGate.totalSteps))
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
        .padding(.horizontal, Spacing.m)
        .padding(.top, Spacing.xs)
    }

    private var stepBody: some View {
        ZStack {
            switch step {
            case 1: ServicesStep(viewModel: viewModel)
            case 2: WhenWhereStep(viewModel: viewModel, geocoding: geocoding, mapProvider: mapProvider)
            default: ConfirmStep(viewModel: viewModel)
            }
        }
        .transition(stepTransition)
        .id(step)
        .animation(.easeInOut(duration: 0.28), value: step)
    }

    private var stepTransition: AnyTransition {
        .asymmetric(
            insertion: .move(edge: .trailing).combined(with: .opacity),
            removal: .move(edge: .leading).combined(with: .opacity)
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
            } else {
                CleansiaPrimaryButton(
                    totalDisplay.map(L10n.Booking.continuePrice) ?? L10n.Booking.continueAction,
                    trailingIcon: "arrow.right",
                    enabled: canContinue,
                    action: onContinue
                )
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
                onDismiss: {}
            )
        }
    }
#endif
