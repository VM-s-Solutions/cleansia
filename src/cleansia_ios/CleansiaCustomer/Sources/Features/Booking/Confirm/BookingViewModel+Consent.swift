import CleansiaCore
import Foundation

extension BookingViewModel {
    /// Re-read at every sheet opening rather than cached: the view model outlives a sign-out, and
    /// the answer belongs to the account, not the draft. A guest has no record to read.
    func loadConsentStatus() async {
        guard tokenStore.current() != nil else {
            alreadyConsented = false
            return
        }
        let granted = await consentClient.grantedTypes()
        alreadyConsented = granted?.isSuperset(of: SignupConsentType.signupTick) == true
    }
}
