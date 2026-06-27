import CleansiaCore
import Foundation

/// Tracks which checklist items the cleaner has ticked for a single order,
/// backed by the local `CleaningChecklistStore` (keyed by orderId) so the set
/// survives process death. No network — the ticks are the cleaner's own working
/// memory (the `CleaningChecklistViewModel.kt` parity).
@MainActor
final class CleaningChecklistViewModel: ViewModel {
    @Published private(set) var checkedIds: Set<String> = []

    private let orderId: String
    private let store: CleaningChecklistStore

    init(orderId: String, store: CleaningChecklistStore) {
        self.orderId = orderId
        self.store = store
        checkedIds = store.checkedIds(orderId: orderId)
    }

    func setChecked(_ itemId: String, _ checked: Bool) {
        store.setChecked(orderId: orderId, itemId: itemId, checked: checked)
        checkedIds = store.checkedIds(orderId: orderId)
    }
}

#if DEBUG
    extension CleaningChecklistViewModel {
        static var preview: CleaningChecklistViewModel {
            CleaningChecklistViewModel(orderId: "preview", store: UserDefaultsCleaningChecklistStore())
        }
    }
#endif
