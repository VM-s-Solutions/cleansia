import CleansiaCore
import SwiftUI

struct BookingSheetView: View {
    @StateObject private var vm = BookingViewModel()
    let geocoding: GeocodingService
    let mapProvider: MapProvider
    let onDismiss: () -> Void

    var body: some View {
        BookingSheetContent(
            viewModel: vm,
            geocoding: geocoding,
            mapProvider: mapProvider,
            onLeading: {
                if !vm.back() { onDismiss() }
            },
            onContinue: { vm.advance() },
            onConfirm: {}
        )
        .presentationDetents([.large])
        .presentationDragIndicator(.visible)
    }
}

private struct BookingSheetContent: View {
    @ObservedObject var viewModel: BookingViewModel
    let geocoding: GeocodingService
    let mapProvider: MapProvider
    let onLeading: () -> Void
    let onContinue: () -> Void
    let onConfirm: () -> Void

    private var step: Int {
        viewModel.currentStep
    }

    private var isLastStep: Bool {
        step >= BookingStepGate.totalSteps
    }

    private var canContinue: Bool {
        BookingStepGate.canContinue(step: step, state: viewModel.state)
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

    private var footer: some View {
        VStack(spacing: 0) {
            if isLastStep {
                SlideToConfirmTrack(
                    text: totalDisplay.map(L10n.Booking.slideToConfirmPrice) ?? L10n.Booking.slideToConfirm,
                    enabled: canContinue
                )
                .onTapGesture { if canContinue { onConfirm() } }
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

private struct SlideToConfirmTrack: View {
    let text: String
    let enabled: Bool

    var body: some View {
        ZStack(alignment: .leading) {
            RoundedRectangle(cornerRadius: CornerRadius.pill)
                .fill(enabled ? CleansiaColors.primary : CleansiaColors.surfaceVariant)

            Text(text)
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(enabled ? CleansiaColors.onPrimary : CleansiaColors.onSurfaceVariant)
                .frame(maxWidth: .infinity)

            Circle()
                .fill(CleansiaColors.surface)
                .overlay {
                    Image(systemName: "chevron.right")
                        .font(.system(size: 16, weight: .bold))
                        .foregroundColor(enabled ? CleansiaColors.primary : CleansiaColors.onSurfaceVariant)
                }
                .padding(4)
        }
        .frame(height: 56)
        .opacity(enabled ? 1 : 0.6)
    }
}

#if DEBUG
    struct BookingSheetView_Previews: PreviewProvider {
        static var previews: some View {
            BookingSheetView(
                geocoding: CLGeocoderGeocodingService(),
                mapProvider: PreviewMapProvider(),
                onDismiss: {}
            )
        }
    }
#endif
