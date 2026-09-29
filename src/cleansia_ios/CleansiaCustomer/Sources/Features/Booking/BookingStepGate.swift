import Foundation

enum BookingStepGate {
    static let totalSteps = 4

    /// `alreadyConsented` is the account's record of both documents the review step's tick names;
    /// with it the box is not shown, so the tick is not asked for either. The card-guarantee tick is
    /// asked only while `needsCardGuarantee` shows its box.
    static func canContinue(
        step: Int,
        state: BookingState,
        alreadyConsented: Bool,
        needsCardGuarantee: Bool = false
    ) -> Bool {
        switch step {
        case 1:
            (!state.selectedServiceIds.isEmpty || !state.selectedPackageIds.isEmpty) && state.rooms >= 1
        case 2:
            state.dirtiness != nil
        case 3:
            !state.street.isBlank && !state.selectedDate.isBlank && !state.selectedTime.isBlank
        case 4:
            state.paymentMethod != nil && (alreadyConsented || state.termsAccepted)
                && (!needsCardGuarantee || state.cardGuaranteeAccepted)
        default:
            false
        }
    }
}
