import CleansiaCore
import Foundation

/// Freshness watermark for the my-invoices list (the `InvoicesRepository.kt:58,
/// 74,83-85` parity). The list is "fresh" within `window` of its last
/// `markFresh`; `ensureFreshOrCached` callers skip the network while warm, so a
/// resume from the invoice detail is a no-op (no spinner flash). A user pull
/// bypasses this entirely. `MainActor`-isolated — only the VM touches it.
@MainActor
final class InvoicesStaleness: SessionScopedCache {
    private let window: TimeInterval
    private let now: () -> Date
    private var mark: Date?

    init(window: TimeInterval = 30, now: @escaping () -> Date = Date.init) {
        self.window = window
        self.now = now
    }

    var isStale: Bool {
        guard let mark else { return true }
        return now().timeIntervalSince(mark) >= window
    }

    func markFresh() {
        mark = now()
    }

    func invalidate() {
        mark = nil
    }

    func clear() async {
        mark = nil
    }
}
