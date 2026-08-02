import SwiftUI

public enum SecretRevealState: Equatable {
    case concealed
    case revealed

    public static let initial: SecretRevealState = .concealed

    /// The load-bearing property of the whole component: a Telegram-style blur
    /// leaves the string in the view hierarchy, where a screenshot, a screen
    /// recording and an accessibility dump all still reach it. Only the revealed
    /// state may build the view that carries the text.
    public var composesSecret: Bool {
        self == .revealed
    }
}

public enum SecretRevealPolicy {
    /// Long enough to read a door code out to someone, short enough that a phone
    /// left on a table stops showing it.
    public static let autoConcealAfter: TimeInterval = 60

    public static func toggled(_ current: SecretRevealState) -> SecretRevealState {
        current == .revealed ? .concealed : .revealed
    }

    public static func onScenePhaseChange(to phase: ScenePhase, current: SecretRevealState) -> SecretRevealState {
        phase == .active ? current : .concealed
    }

    public static func onTick(now: Date, revealedAt: Date) -> SecretRevealState {
        now.timeIntervalSince(revealedAt) >= autoConcealAfter ? .concealed : .revealed
    }
}

/// A disclosure for a value that should not be on screen by default — a door code,
/// a key-box combination, an alarm code.
///
/// It is shoulder-surf resistant, not merely tidy: `secret` is a builder, so while
/// the panel is concealed the text is never constructed. It re-conceals when the app
/// leaves the foreground and one minute after the reveal.
public struct CleansiaRevealPanel<Secret: View>: View {
    @Environment(\.scenePhase) private var scenePhase
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @State private var state: SecretRevealState = .initial
    @State private var revealedAt = Date.distantPast
    @AccessibilityFocusState private var secretFocused: Bool

    private let title: String
    private let secret: () -> Secret

    public init(title: String, @ViewBuilder secret: @escaping () -> Secret) {
        self.title = title
        self.secret = secret
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            toggle
            if state.composesSecret {
                secret()
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .accessibilityFocused($secretFocused)
                    .transition(
                        .asymmetric(
                            insertion: .opacity.combined(with: .move(edge: .top)),
                            removal: .opacity
                        )
                    )
            } else {
                RedactedLines()
                    .accessibilityHidden(true)
            }
        }
        .padding(Spacing.s)
        .background(CleansiaColors.surfaceVariant.opacity(0.6), in: RoundedRectangle(cornerRadius: CornerRadius.medium))
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.medium)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
        .onChange(of: scenePhase) { phase in
            state = SecretRevealPolicy.onScenePhaseChange(to: phase, current: state)
        }
        .task(id: revealedAt) {
            guard state.composesSecret else { return }
            try? await Task.sleep(nanoseconds: UInt64(SecretRevealPolicy.autoConcealAfter * 1_000_000_000))
            guard !Task.isCancelled else { return }
            withAnimation(animation) { state = SecretRevealPolicy.onTick(now: Date(), revealedAt: revealedAt) }
        }
    }

    private var toggle: some View {
        Button {
            withAnimation(animation) { state = SecretRevealPolicy.toggled(state) }
            if state.composesSecret {
                revealedAt = Date()
                secretFocused = true
            }
        } label: {
            HStack(spacing: Spacing.xs) {
                Image(systemName: state.composesSecret ? "lock.open.fill" : "lock.fill")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundColor(CleansiaColors.primary)
                Text(title)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                Spacer()
                Text(actionLabel)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.primary)
                Image(systemName: "chevron.down")
                    .font(.system(size: 11, weight: .bold))
                    .foregroundColor(CleansiaColors.primary)
                    .rotationEffect(.degrees(state.composesSecret ? 180 : 0))
            }
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .accessibilityLabel(Text(title))
        .accessibilityValue(Text(CoreL10n.localized(
            state.composesSecret ? "reveal_panel.value.shown" : "reveal_panel.value.hidden"
        )))
        .accessibilityHint(Text(CoreL10n.localized("reveal_panel.hint")))
    }

    private var actionLabel: String {
        CoreL10n.localized(state.composesSecret ? "reveal_panel.action.hide" : "reveal_panel.action.show")
    }

    private var animation: Animation? {
        reduceMotion ? .easeInOut(duration: 0.15) : .spring(response: 0.38, dampingFraction: 0.82)
    }
}

/// The concealed stand-in. Bars of a fixed shape — nothing here is derived from the
/// secret, not even its length.
private struct RedactedLines: View {
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @State private var shimmer = false

    private let widths: [CGFloat] = [0.9, 0.55]

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xxs) {
            ForEach(widths, id: \.self) { fraction in
                GeometryReader { geometry in
                    Capsule()
                        .fill(CleansiaColors.outlineVariant)
                        .frame(width: geometry.size.width * fraction)
                }
                .frame(height: 10)
            }
        }
        .opacity(shimmer ? 0.45 : 0.85)
        .onAppear {
            guard !reduceMotion else { return }
            withAnimation(.easeInOut(duration: 1.1).repeatForever(autoreverses: true)) { shimmer = true }
        }
    }
}

#if DEBUG
    struct CleansiaRevealPanel_Previews: PreviewProvider {
        static var previews: some View {
            CleansiaRevealPanel(title: "Entry instructions") {
                Text("Side gate, key box code 4417. Second door on the left.")
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurface)
            }
            .padding()
            .background(CleansiaColors.surface)
        }
    }
#endif
