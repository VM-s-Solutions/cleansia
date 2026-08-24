import CleansiaCore
import Foundation

/// Whether — and about which booking — to raise the completion review prompt on app open.
///
/// A pure rule hoisted out of the shell, matching `JobRadiusPrompt` on the partner side: a decision to
/// interrupt someone deserves to be tested directly rather than through a view.
enum ReviewPrompt {
    /// The `AppSettingsStore` prompt id. Keyed per ORDER below, not per user, so a second completed
    /// clean still asks.
    static func settingsKey(orderId: String) -> String {
        "order_review_\(orderId)"
    }

    /// The newest completed, unreviewed booking the customer has not already been asked about.
    ///
    /// Newest rather than oldest deliberately: the prompt lands right after a clean finishes, and being
    /// asked about the freshest one is what the customer expects. An older unreviewed booking is not
    /// chased — a backlog of prompts is how this pattern becomes noise.
    ///
    /// `hasReview` is server truth and outranks the local flag, so a review left on another device
    /// silences the prompt here too.
    static func candidate(
        orders: [CustomerOrderSummary],
        alreadyPrompted: Set<String>
    ) -> CustomerOrderSummary? {
        orders
            .filter { OrderStatusGroup.isCompleted($0.status) }
            .filter { !$0.hasReview }
            .filter { !alreadyPrompted.contains($0.id) }
            .max { lhs, rhs in
                (lhs.cleaningDateTime ?? .distantPast) < (rhs.cleaningDateTime ?? .distantPast)
            }
    }
}
