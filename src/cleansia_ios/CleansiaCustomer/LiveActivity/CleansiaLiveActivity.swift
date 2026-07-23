import ActivityKit
import SwiftUI
import WidgetKit

// The CleansiaCustomerLiveActivity widget extension entry point + the branded lock-screen / Dynamic
// Island presentation of an in-progress clean, styled after Uber/Wolt delivery activities: a mascot
// avatar, a bold self-advancing ETA countdown (NOT a progress bar), and a clean two-line header. The
// countdown uses `Text(timerInterval:)`, which live-updates on a locked device with NO push and NO app
// running — so the card never looks frozen even between backend status pushes. Renders
// CleanOrderAttributes, which the app starts and the backend pushes status updates to.
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
    static let sky = Color(red: 0.008, green: 0.518, blue: 0.780) // #0284C7 (sky600)
    static let skyBright = Color(red: 0.220, green: 0.741, blue: 0.973) // #38BDF8 (sky400)
    static let tint = Color(red: 0.878, green: 0.949, blue: 0.996) // #E0F2FE (sky100)
}

// MARK: - Status presentation

private struct CleanStatus {
    let title: String
    let detail: String
    let symbol: String
    let isTerminal: Bool

    /// Active (non-terminal) states show the live ETA countdown to completion; terminal states show a
    /// final glyph + line instead.
    var showsEta: Bool {
        !isTerminal
    }

    init(_ raw: String) {
        switch raw {
        case "onTheWay":
            self = CleanStatus(
                title: "On the way",
                detail: "Your cleaner is heading over",
                symbol: "figure.walk",
                isTerminal: false
            )
        case "inProgress":
            self = CleanStatus(
                title: "Cleaning in progress",
                detail: "Your cleaner is on site",
                symbol: "sparkles",
                isTerminal: false
            )
        case "completed":
            self = CleanStatus(
                title: "Clean complete",
                detail: "All done — thank you",
                symbol: "checkmark.seal.fill",
                isTerminal: true
            )
        case "cancelled":
            self = CleanStatus(
                title: "Cancelled",
                detail: "This clean was cancelled",
                symbol: "xmark.circle.fill",
                isTerminal: true
            )
        default:
            self = CleanStatus(
                title: "Your clean",
                detail: "",
                symbol: "sparkles",
                isTerminal: false
            )
        }
    }

    private init(title: String, detail: String, symbol: String, isTerminal: Bool) {
        self.title = title
        self.detail = detail
        self.symbol = symbol
        self.isTerminal = isTerminal
    }

    /// Bundled mascot art for the active (non-terminal) states; the terminal states keep the clearer
    /// checkmark / xmark SF Symbols. Lives in the widget's own asset catalog (LiveActivity/Assets.xcassets).
    var mascotAsset: String? {
        isTerminal ? nil : "mascot_live"
    }
}

/// The status icon: the cleaning mascot for active states, an SF Symbol for terminal ones — and a robust
/// fallback to the SF Symbol whenever the mascot art can't be resolved from the widget bundle (so a
/// mis-membered asset catalog degrades to a symbol instead of rendering blank).
@ViewBuilder
private func statusIcon(_ status: CleanStatus, size: CGFloat) -> some View {
    if let art = status.mascotAsset, UIImage(named: art) != nil {
        Image(art).resizable().scaledToFit().frame(width: size, height: size)
    } else {
        Image(systemName: status.symbol)
            .font(.system(size: size * 0.62, weight: .semibold))
            .foregroundStyle(Brand.sky)
    }
}

/// A safe completion-ETA interval: the scheduled cleaning window, guarded so the range is always valid
/// (end strictly after start) — `Text(timerInterval:)` then shows the live time remaining until the clean
/// is done, advancing on its own with no push.
private func etaInterval(_ state: CleanOrderAttributes.ContentState) -> ClosedRange<Date>? {
    guard state.scheduledEnd > state.scheduledStart else { return nil }
    return state.scheduledStart ... state.scheduledEnd
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
            let eta = etaInterval(context.state)
            return DynamicIsland {
                DynamicIslandExpandedRegion(.leading) {
                    Label {
                        Text(orderLabel(context.state.orderNumber)).font(.caption2)
                    } icon: {
                        statusIcon(status, size: 22)
                    }
                }
                DynamicIslandExpandedRegion(.trailing) {
                    if status.showsEta, let eta {
                        VStack(alignment: .trailing, spacing: 0) {
                            Text(timerInterval: eta, countsDown: true)
                                .font(.title3.weight(.bold).monospacedDigit())
                                .foregroundStyle(Brand.sky)
                                .multilineTextAlignment(.trailing)
                                .frame(maxWidth: 84)
                            Text("left").font(.caption2).foregroundStyle(.secondary)
                        }
                    } else {
                        Text(status.title).font(.caption.weight(.semibold)).foregroundStyle(Brand.sky)
                    }
                }
                DynamicIslandExpandedRegion(.bottom) {
                    Text(status.detail.isEmpty ? status.title : status.detail)
                        .font(.caption).foregroundStyle(.secondary)
                }
            } compactLeading: {
                statusIcon(status, size: 18)
            } compactTrailing: {
                if status.showsEta, let eta {
                    Text(timerInterval: eta, countsDown: true)
                        .font(.caption2.weight(.semibold).monospacedDigit())
                        .foregroundStyle(Brand.sky)
                        .frame(width: 44)
                } else {
                    Image(systemName: status.symbol).foregroundStyle(Brand.sky)
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

    private var status: CleanStatus {
        CleanStatus(state.status)
    }

    var body: some View {
        HStack(spacing: 14) {
            ZStack {
                Circle().fill(Brand.tint)
                statusIcon(status, size: 42)
            }
            .frame(width: 58, height: 58)

            VStack(alignment: .leading, spacing: 3) {
                Text(status.title)
                    .font(.headline)
                    .foregroundStyle(.primary)
                Text(orderLabel(state.orderNumber))
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
                if !status.detail.isEmpty {
                    Text(status.detail)
                        .font(.caption)
                        .foregroundStyle(Brand.sky)
                        .lineLimit(1)
                }
            }

            Spacer(minLength: 8)

            // Uber/Wolt-style ETA: a bold, self-advancing countdown to completion — no progress bar.
            if status.showsEta, let eta = etaInterval(state) {
                VStack(alignment: .trailing, spacing: 0) {
                    Text(timerInterval: eta, countsDown: true)
                        .font(.system(.title2, design: .rounded).weight(.bold))
                        .monospacedDigit()
                        .foregroundStyle(Brand.sky)
                        .multilineTextAlignment(.trailing)
                        .frame(maxWidth: 96)
                    Text("remaining")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                }
            } else {
                Image(systemName: status.symbol)
                    .font(.title.weight(.semibold))
                    .foregroundStyle(Brand.sky)
            }
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 14)
    }
}

private func orderLabel(_ orderNumber: String) -> String {
    orderNumber.isEmpty ? "Your booking" : "Order #\(orderNumber)"
}
