import CleansiaCore
import SwiftUI

struct BookingSheetView: View {
    @StateObject private var vm = BookingViewModel()
    let onDismiss: () -> Void

    var body: some View {
        BookingSheetContent(
            step: vm.currentStep,
            state: vm.state,
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
    let step: Int
    let state: BookingState
    let onLeading: () -> Void
    let onContinue: () -> Void
    let onConfirm: () -> Void

    private var isLastStep: Bool {
        step >= BookingStepGate.totalSteps
    }

    private var canContinue: Bool {
        BookingStepGate.canContinue(step: step, state: state)
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
            case 1: ServicesStep()
            case 2: WhenWhereStep()
            default: ConfirmStep()
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
                SlideToConfirmTrack(text: L10n.Booking.slideToConfirm, enabled: canContinue)
                    .onTapGesture { if canContinue { onConfirm() } }
            } else {
                CleansiaPrimaryButton(
                    L10n.Booking.continueAction,
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
            Group {
                preview(step: 1, state: BookingState())
                    .previewDisplayName("Step 1 — gate locked")
                preview(step: 1, state: filledStepOne)
                    .previewDisplayName("Step 1 — can continue")
                preview(step: 2, state: filledStepTwo)
                    .previewDisplayName("Step 2")
                preview(step: 3, state: filledStepThree)
                    .previewDisplayName("Step 3 — slide to confirm")
            }
        }

        private static var filledStepOne: BookingState {
            var state = BookingState()
            state.selectedServiceIds = ["s-1"]
            return state
        }

        private static var filledStepTwo: BookingState {
            var state = filledStepOne
            state.street = "Wenceslas Square"
            state.selectedDate = "2026-07-01"
            state.selectedTime = "10:00"
            return state
        }

        private static var filledStepThree: BookingState {
            var state = filledStepTwo
            state.paymentMethod = "cash"
            return state
        }

        private static func preview(step: Int, state: BookingState) -> some View {
            BookingSheetContent(
                step: step,
                state: state,
                onLeading: {},
                onContinue: {},
                onConfirm: {}
            )
        }
    }
#endif
