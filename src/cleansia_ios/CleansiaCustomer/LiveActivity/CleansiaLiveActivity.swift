import ActivityKit
import CleansiaCore
import SwiftUI
import WidgetKit

// The CleansiaCustomerLiveActivity widget extension entry point + the branded lock-screen / Dynamic
// Island presentation of an in-progress clean, styled after Uber/Wolt delivery activities: a per-status
// mascot avatar, a bold self-advancing ETA (NOT a progress bar), and a clean two-line header. The ETA is
// system-drawn, so it live-updates on a locked device with NO push and NO app running; once the counted-to
// end passes it switches to counting UP from a past anchor, which cannot clamp — so the card keeps moving
// even when no push arrives. Renders CleanOrderAttributes, which the app starts and the backend pushes
// status updates to.
//
// The whole extension deploys at iOS 16.1, so no @available guards are needed inside it.
//
// Copy comes from CleansiaCore's catalog via LiveActivityL10n: this target links Core but not the app
// module, so Core's is the only catalog both processes can read.

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
    /// Bundled mascot art, one pose per active state so the card reads at a glance; the terminal states
    /// keep the clearer checkmark / xmark SF Symbols. Lives in the widget's own asset catalog
    /// (LiveActivity/Assets.xcassets); `mascot_live` stays the generic fallback for an unknown status.
    let mascotAsset: String?
    let isTerminal: Bool

    init(_ raw: String) {
        switch raw {
        case "onTheWay":
            self = CleanStatus(
                title: LiveActivityL10n.Status.onTheWayTitle,
                detail: LiveActivityL10n.Status.onTheWayDetail,
                symbol: "figure.walk",
                mascotAsset: "mascot_on_the_way",
                isTerminal: false
            )
        case "inProgress":
            self = CleanStatus(
                title: LiveActivityL10n.Status.inProgressTitle,
                detail: LiveActivityL10n.Status.inProgressDetail,
                symbol: "sparkles",
                mascotAsset: "mascot_cleaning",
                isTerminal: false
            )
        case "completed":
            self = CleanStatus(
                title: LiveActivityL10n.Status.completedTitle,
                detail: LiveActivityL10n.Status.completedDetail,
                symbol: "checkmark.seal.fill",
                mascotAsset: nil,
                isTerminal: true
            )
        case "cancelled":
            self = CleanStatus(
                title: LiveActivityL10n.Status.cancelledTitle,
                detail: LiveActivityL10n.Status.cancelledDetail,
                symbol: "xmark.circle.fill",
                mascotAsset: nil,
                isTerminal: true
            )
        default:
            self = CleanStatus(
                title: LiveActivityL10n.Status.genericTitle,
                detail: "",
                symbol: "sparkles",
                mascotAsset: "mascot_live",
                isTerminal: false
            )
        }
    }

    private init(title: String, detail: String, symbol: String, mascotAsset: String?, isTerminal: Bool) {
        self.title = title
        self.detail = detail
        self.symbol = symbol
        self.mascotAsset = mascotAsset
        self.isTerminal = isTerminal
    }

    func eta(for state: CleanOrderAttributes.ContentState, now: Date = Date()) -> EtaPresentation {
        LiveActivityEta.presentation(window: state.etaWindow, terminalLabel: isTerminal ? title : nil, now: now)
    }
}

/// The status icon: the status's own mascot for active states, an SF Symbol for terminal ones — and a robust
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

/// The ETA readout: the ticking digits plus the word that says what they mean. A terminal status has
/// nothing to time, so it falls back to the status line (expanded island) or its glyph.
private struct EtaReadout: View {
    enum Style {
        case lockScreen
        case island
        case compact
    }

    let eta: EtaPresentation
    let symbol: String
    let style: Style

    var body: some View {
        switch eta {
        case let .countdown(range):
            ticking(Text(timerInterval: range, countsDown: true))
        case let .elapsed(since):
            ticking(Text(since, style: .timer))
        case let .label(text):
            terminal(text)
        }
    }

    private func ticking(_ digits: Text) -> some View {
        VStack(alignment: .trailing, spacing: 0) {
            digits
                .font(digitFont)
                .foregroundStyle(Brand.sky)
                .multilineTextAlignment(.trailing)
                .frame(maxWidth: digitWidth)
            if let caption {
                Text(caption)
                    .font(.caption2)
                    .foregroundStyle(.secondary)
            }
        }
    }

    @ViewBuilder
    private func terminal(_ text: String) -> some View {
        switch style {
        case .island:
            Text(text).font(.caption.weight(.semibold)).foregroundStyle(Brand.sky)
        case .lockScreen:
            Image(systemName: symbol).font(.title.weight(.semibold)).foregroundStyle(Brand.sky)
        case .compact:
            Image(systemName: symbol).foregroundStyle(Brand.sky)
        }
    }

    /// Never "remaining" over a count-UP — that number is time spent, not time left. The compact slot has
    /// room for the digits only.
    private var caption: String? {
        guard style != .compact else { return nil }
        return switch eta {
        case .countdown: style == .lockScreen ? LiveActivityL10n.Eta.remaining : LiveActivityL10n.Eta.left
        case .elapsed: LiveActivityL10n.Eta.elapsed
        case .label: nil
        }
    }

    private var digitFont: Font {
        switch style {
        case .lockScreen: .system(.title2, design: .rounded).weight(.bold).monospacedDigit()
        case .island: .title3.weight(.bold).monospacedDigit()
        case .compact: .caption2.weight(.semibold).monospacedDigit()
        }
    }

    private var digitWidth: CGFloat {
        switch style {
        case .lockScreen: 96
        case .island: 84
        case .compact: 44
        }
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
            let eta = status.eta(for: context.state)
            return DynamicIsland {
                DynamicIslandExpandedRegion(.leading) {
                    Label {
                        Text(orderLabel(context.state.orderNumber)).font(.caption2)
                    } icon: {
                        statusIcon(status, size: 22)
                    }
                }
                DynamicIslandExpandedRegion(.trailing) {
                    EtaReadout(eta: eta, symbol: status.symbol, style: .island)
                }
                DynamicIslandExpandedRegion(.bottom) {
                    Text(status.detail.isEmpty ? status.title : status.detail)
                        .font(.caption).foregroundStyle(.secondary)
                }
            } compactLeading: {
                statusIcon(status, size: 18)
            } compactTrailing: {
                EtaReadout(eta: eta, symbol: status.symbol, style: .compact)
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

            // Uber/Wolt-style ETA: a bold, self-advancing number — no progress bar.
            EtaReadout(eta: status.eta(for: state), symbol: status.symbol, style: .lockScreen)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 14)
    }
}

private func orderLabel(_ orderNumber: String) -> String {
    orderNumber.isEmpty ? LiveActivityL10n.bookingFallback : LiveActivityL10n.orderNumber(orderNumber)
}
