import Foundation

@MainActor
final class PushNavigationModel: ObservableObject {
    @Published var pendingDestination: CustomerNotificationDestination?

    func consume() -> CustomerNotificationDestination? {
        defer { pendingDestination = nil }
        return pendingDestination
    }
}
