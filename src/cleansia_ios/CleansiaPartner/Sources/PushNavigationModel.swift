import Combine
import Foundation

@MainActor
final class PushNavigationModel: ObservableObject {
    @Published var pendingDestination: PartnerNotificationDestination?

    /// The event key of every push that lands while the app is in the foreground — the case a
    /// scenePhase observer cannot see.
    let foregroundPushes = PassthroughSubject<String, Never>()

    func consume() -> PartnerNotificationDestination? {
        defer { pendingDestination = nil }
        return pendingDestination
    }
}
