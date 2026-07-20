import ActivityKit
import Foundation

// The ActivityKit contract shared by the CleansiaCustomer APP (which starts/ends the activity) and the
// CleansiaCustomerLiveActivity WIDGET (which renders it). This file is a member of BOTH targets — the app
// target picks it up under `Sources`, the widget target includes it explicitly (see project.yml).
//
// It must match the backend wire contract EXACTLY (ADR-0029 D4):
//   • the type name `CleanOrderAttributes` is the `attributes-type` the server sends on push-to-start
//     (LiveActivityPayloadFactory.AttributesType) — renaming it breaks server-started activities;
//   • `ContentState`'s keys {v, status, orderNumber, scheduledStart, scheduledEnd} are the content-state
//     the server pushes on every order-status change (LiveActivityContentState). Pinned both-sides by
//     src/Cleansia.Tests/Functions/Fixtures/live-activity-content-state.json.
//
// Guarded @available(iOS 16.1, *): ActivityKit is 16.1+, while the customer app floor stays 16.0.
@available(iOS 16.1, *)
struct CleanOrderAttributes: ActivityAttributes {
    // Dynamic — replaced on every server push.
    public struct ContentState: Codable, Hashable {
        public var v: Int
        public var status: String            // onTheWay | inProgress | completed | cancelled (unknown → generic)
        public var orderNumber: String
        public var scheduledStart: Date
        public var scheduledEnd: Date

        public init(v: Int, status: String, orderNumber: String, scheduledStart: Date, scheduledEnd: Date) {
            self.v = v
            self.status = status
            self.orderNumber = orderNumber
            self.scheduledStart = scheduledStart
            self.scheduledEnd = scheduledEnd
        }
    }

    // Static — set once at activity start (mirrors the backend LiveActivityStartAttributes).
    public var orderNumber: String

    public init(orderNumber: String) {
        self.orderNumber = orderNumber
    }
}
