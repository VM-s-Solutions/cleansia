import CleansiaCore
import Foundation

final class RecordingSignupConsent: SignupConsentRecording, @unchecked Sendable {
    private let lock = NSLock()
    private var records: [(email: String, accepted: Bool)] = []

    var parked: [(email: String, accepted: Bool)] {
        lock.withLock { records }
    }

    func recordSignupTick(email: String, accepted: Bool) async {
        lock.withLock { records.append((email, accepted)) }
    }
}
