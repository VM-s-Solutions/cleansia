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
        case .subscribePlus:
            Plan(tab: .profile, routes: [.subscribePlus])
        case .rewardsActivity:
            Plan(tab: .rewards, routes: [.rewardsActivity])
        }
    }
}
