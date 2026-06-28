import Foundation

@MainActor
final class PushNavigationModel: ObservableObject {
    @Published var pendingDestination: PartnerNotificationDestination?

    func consume() -> PartnerNotificationDestination? {
        defer { pendingDestination = nil }
        return pendingDestination
    }
}
