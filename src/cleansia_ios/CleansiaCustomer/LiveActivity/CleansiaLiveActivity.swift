import ActivityKit
import SwiftUI
import WidgetKit

// The CleansiaCustomerLiveActivity widget extension entry point + the branded lock-screen / Dynamic
// Island presentation of an in-progress clean. Self-contained (hardcoded brand palette, SF Symbols,
// system timer views that animate without a push) so it builds cleanly on its own; it renders
// CleanOrderAttributes, which the app starts and the backend pushes updates to.
//
// The whole extension deploys at iOS 16.1, so no @available guards are needed inside it.

@main
struct CleansiaLiveActivityBundle: WidgetBundle {
    var body: some Widget {
        CleanOrderLiveActivity()
    }
}

// MARK: - Brand

private enum Brand {
    static let sky = Color(red: 0.008, green: 0.518, blue: 0.780)   // #0284C7 (sky600)
    static let skyBright = Color(red: 0.220, green: 0.741, blue: 0.973) // #38BDF8 (sky400)
    static let tint = Color(red: 0.878, green: 0.949, blue: 0.996)  // #E0F2FE (sky100)
}

// MARK: - Status presentation

private struct CleanStatus {
    let title: String
    let detail: String
    let symbol: String
    let showsProgress: Bool
    let isTerminal: Bool

    init(_ raw: String) {
        switch raw {
        case "onTheWay":
            self = CleanStatus(title: "On the way", detail: "Your cleaner is heading over",
                               symbol: "figure.walk", showsProgress: false, isTerminal: false)
        case "inProgress":
            self = CleanStatus(title: "Cleaning in progress", detail: "Your cleaner is on site",
                               symbol: "sparkles", showsProgress: true, isTerminal: false)
        case "completed":
            self = CleanStatus(title: "Clean complete", detail: "All done — thank you",
                               symbol: "checkmark.seal.fill", showsProgress: false, isTerminal: true)
        case "cancelled":
            self = CleanStatus(title: "Cancelled", detail: "This clean was cancelled",
                               symbol: "xmark.circle.fill", showsProgress: false, isTerminal: true)
        default:
            self = CleanStatus(title: "Your clean", detail: "", symbol: "sparkles",
                               showsProgress: false, isTerminal: false)
        }
    }

    private init(title: String, detail: String, symbol: String, showsProgress: Bool, isTerminal: Bool) {
        self.title = title; self.detail = detail; self.symbol = symbol
        self.showsProgress = showsProgress; self.isTerminal = isTerminal
    }
}

// MARK: - Widget

struct CleanOrderLiveActivity: Widget {
    var body: some WidgetConfiguration {
        ActivityConfiguration(for: CleanOrderAttributes.self) { context in
            LockScreenLiveActivityView(state: context.state)
                .activityBackgroundTint(Brand.sky.opacity(0.10))
                .activitySystemActionForegroundColor(Brand.sky)
        } dynamicIsland: { context in
            let status = CleanStatus(context.state.status)
            return DynamicIsland {
                DynamicIslandExpandedRegion(.leading) {
                    Label {
                        Text(orderLabel(context.state.orderNumber)).font(.caption2)
                    } icon: {
                        Image(systemName: status.symbol).foregroundStyle(Brand.sky)
                    }
                }
                DynamicIslandExpandedRegion(.trailing) {
                    Text(status.title).font(.caption.weight(.semibold)).foregroundStyle(Brand.sky)
                }
                DynamicIslandExpandedRegion(.bottom) {
                    if status.showsProgress {
                        ProgressView(timerInterval: context.state.scheduledStart...context.state.scheduledEnd,
                                     countsDown: false)
                            .tint(Brand.sky)
                            .font(.caption2)
                    } else {
                        Text(status.detail).font(.caption).foregroundStyle(.secondary)
                    }
                }
            } compactLeading: {
                Image(systemName: status.symbol).foregroundStyle(Brand.sky)
            } compactTrailing: {
                if status.showsProgress {
                    ProgressView(timerInterval: context.state.scheduledStart...context.state.scheduledEnd,
                                 countsDown: false)
                        .tint(Brand.sky)
                        .labelsHidden()
                        .frame(width: 32)
                } else {
                    Text(status.title).font(.caption2.weight(.semibold)).foregroundStyle(Brand.sky)
                }
            } minimal: {
                Image(systemName: status.symbol).foregroundStyle(Brand.sky)
            }
            .keylineTint(Brand.sky)
        }
    }
}

// MARK: - Lock screen

private struct LockScreenLiveActivityView: View {
    let state: CleanOrderAttributes.ContentState

    private var status: CleanStatus { CleanStatus(state.status) }

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 12) {
                ZStack {
                    Circle().fill(Brand.tint).frame(width: 40, height: 40)
                    Image(systemName: status.symbol)
                        .font(.system(size: 18, weight: .semibold))
                        .foregroundStyle(Brand.sky)
                }
                VStack(alignment: .leading, spacing: 1) {
                    Text(status.title).font(.headline)
                    Text(orderLabel(state.orderNumber)).font(.subheadline).foregroundStyle(.secondary)
                }
                Spacer()
                Text("Cleansia")
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(Brand.sky)
            }

            if status.showsProgress {
                VStack(alignment: .leading, spacing: 4) {
                    ProgressView(timerInterval: state.scheduledStart...state.scheduledEnd, countsDown: false)
                        .tint(Brand.sky)
                    HStack {
                        Text("Started").font(.caption2).foregroundStyle(.secondary)
                        Spacer()
                        Text(state.scheduledEnd, style: .time)
                            .font(.caption2).foregroundStyle(.secondary)
                    }
                }
            } else if !status.detail.isEmpty {
                Text(status.detail).font(.footnote).foregroundStyle(.secondary)
            }
        }
        .padding(16)
    }
}

private func orderLabel(_ orderNumber: String) -> String {
    orderNumber.isEmpty ? "Your booking" : "Order #\(orderNumber)"
}
