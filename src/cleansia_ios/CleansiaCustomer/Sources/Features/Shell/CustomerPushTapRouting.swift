import Foundation

enum CustomerPushTapRouting {
    struct Plan: Equatable {
        let tab: CustomerShellTab
        let routes: [ShellRoute]
    }

    static func plan(for destination: CustomerNotificationDestination) -> Plan {
        switch destination {
        case let .order(orderId):
            Plan(tab: .orders, routes: [.orderDetail(orderId)])
        case let .dispute(disputeId):
            // Pre-seeded so back lands on the disputes list, mirroring the
            // CreateDisputeView onCreated wiring.
            Plan(tab: .profile, routes: [.disputes, .disputeDetail(disputeId)])
        case .membershipManagement:
            // The profile tab with NOTHING pushed on top: MembershipManagementCard lives there
            // (ProfileTab.swift), and it already renders the right thing for both states —
            // manage-and-cancel for a live subscription, subscribe for a lapsed one. Pushing
            // .subscribePlus over it, as this used to, buried the management card under the
            // sales page for the one audience that is already a customer.
            Plan(tab: .profile, routes: [])
        case .rewardsActivity:
            Plan(tab: .rewards, routes: [.rewardsActivity])
        }
    }
}
