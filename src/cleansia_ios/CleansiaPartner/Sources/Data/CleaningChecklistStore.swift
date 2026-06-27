import Foundation

/// Local-only persistence for the cleaner's checklist ticks, keyed by orderId.
/// PURELY LOCAL — there is no backend checklist API; the ticks are the cleaner's
/// own working memory and survive process death. Mechanism divergence from
/// Android (DataStore → UserDefaults); behavior identical.
protocol CleaningChecklistStore: AnyObject {
    func checkedIds(orderId: String) -> Set<String>
    func setChecked(orderId: String, itemId: String, checked: Bool)
}

final class UserDefaultsCleaningChecklistStore: CleaningChecklistStore {
    private let defaults: UserDefaults
    private let keyPrefix = "order_checklist."

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
    }

    func checkedIds(orderId: String) -> Set<String> {
        let stored = defaults.stringArray(forKey: key(orderId)) ?? []
        return Set(stored)
    }

    func setChecked(orderId: String, itemId: String, checked: Bool) {
        var ids = checkedIds(orderId: orderId)
        if checked {
            ids.insert(itemId)
        } else {
            ids.remove(itemId)
        }
        defaults.set(Array(ids), forKey: key(orderId))
    }

    private func key(_ orderId: String) -> String {
        keyPrefix + orderId
    }
}
