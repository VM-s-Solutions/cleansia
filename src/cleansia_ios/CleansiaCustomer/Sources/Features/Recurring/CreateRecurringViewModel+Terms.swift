import Foundation

/// A new schedule is a new booking, so it asks for the booking's terms tick on the booking's rule (Android's
/// `CreateRecurringViewModel`): shown until the account's consents are read and hold both documents in force,
/// and after a failed read. An edit asks nothing — the server gates only a create.
extension CreateRecurringViewModel {
    func setTermsAccepted(_ accepted: Bool) {
        formState.termsAccepted = accepted
    }

    func readTermsConsent() async {
        guard !isEditing else { return }
        let held = await consentClient.holdsTermsTickConsents()
        termsAsked = !held
    }

    var termsSatisfied: Bool {
        !termsAsked || formState.termsAccepted
    }

    /// Asserted only when the box was shown and ticked; an account that saw no box asserts nothing new.
    var termsAssertion: Bool? {
        termsAsked && formState.termsAccepted ? true : nil
    }
}
