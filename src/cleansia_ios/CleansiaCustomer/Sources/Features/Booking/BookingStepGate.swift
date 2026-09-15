import Foundation

enum BookingStepGate {
    static let totalSteps = 3

    /// `alreadyConsented` is the account's record of both documents the review step's tick names;
    /// with it the box is not shown, so the tick is not asked for either.
    static func canContinue(step: Int, state: BookingState, alreadyConsented: Bool) -> Bool {
        switch step {
        case 1:
            (!state.selectedServiceIds.isEmpty || !state.selectedPackageIds.isEmpty) && state.rooms >= 1
        case 2:
            !state.street.isBlank && !state.selectedDate.isBlank && !state.selectedTime.isBlank
        case 3:
            state.paymentMethod != nil && (alreadyConsented || state.termsAccepted)
        default:
            false
        }
    }
}
