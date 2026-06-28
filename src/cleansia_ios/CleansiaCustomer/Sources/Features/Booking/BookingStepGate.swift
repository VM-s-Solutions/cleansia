import Foundation

enum BookingStepGate {
    static let totalSteps = 3

    static func canContinue(step: Int, state: BookingState) -> Bool {
        switch step {
        case 1:
            (!state.selectedServiceIds.isEmpty || !state.selectedPackageIds.isEmpty) && state.rooms >= 1
        case 2:
            !state.street.isBlank && !state.selectedDate.isBlank && !state.selectedTime.isBlank
        case 3:
            !state.paymentMethod.isBlank
        default:
            false
        }
    }
}
