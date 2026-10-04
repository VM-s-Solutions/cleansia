import CleansiaCore
import Foundation

/// A new schedule is a new booking, so it asks for the booking's terms tick on the booking's rule (Android's
/// `CreateRecurringViewModel`): shown until the account's consents are read and hold both documents in force,
/// and after a failed read. An edit asks nothing — the server gates only a create.
extension CreateRecurringViewModel {
    private static let termsRefusalCode = "consent.terms_not_accepted"

    func setTermsAccepted(_ accepted: Bool) {
        formState.termsAccepted = accepted
    }

    /// A read the server has already overruled is not taken: it would still say "held" and hide the box again.
    func readTermsConsent() async {
        guard !isEditing, !termsRefused else { return }
        let held = await consentClient.holdsTermsTickConsents()
        termsAsked = !held || termsRefused
    }

    /// The read judges the texts in force for the default market; CreateRecurringBooking judges the saved
    /// address's. When they disagree the server refuses the create, and that refusal shows the box, unticked,
    /// for the rest of the form's life, so the next submit can assert the tick.
    func showTermsIfRefused(_ error: ApiError) {
        guard !isEditing, error.code == Self.termsRefusalCode else { return }
        termsRefused = true
        termsAsked = true
        formState.termsAccepted = false
    }

    var termsSatisfied: Bool {
        !termsAsked || formState.termsAccepted
    }

    /// Asserted only when the box was shown and ticked; an account that saw no box asserts nothing new.
    var termsAssertion: Bool? {
        termsAsked && formState.termsAccepted ? true : nil
    }
}
