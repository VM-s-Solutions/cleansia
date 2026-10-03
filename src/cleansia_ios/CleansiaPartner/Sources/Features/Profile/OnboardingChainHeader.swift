import CleansiaCore
import SwiftUI

/// The onboarding stepper.
///
/// **Every step is named.** Four equal columns, each a node over its short name, so a cleaner can tell
/// at a glance what each step is and where they are: the current step is a larger filled `primary` node
/// with its icon and a bold `primary` name; a finished step keeps its name and swaps its icon for a check
/// on `primaryContainer`; a step not yet done is an outlined node with a muted name. The design this
/// replaced named only the current step, in a capsule, and drew the other three as unlabelled discs —
/// three identical check circles said nothing about which steps they were.
///
/// Three channels carry state, so no single failure of colour perception loses the picture: **size and
/// fill** say where you are, **the check** says a step is finished, and **the ring** says whether you may
/// go there — `primary` on a step you can jump to, `outline` on one you cannot. A reachable step stays
/// tappable across its whole column, node and name (T-0607).
///
/// **It fits because the columns share the width.** At 320pt the card gives 256pt of content, 64pt a
/// step; the longest name in the five shipped locales is eight characters (`Особисте`, `Identity`,
/// `Личность`). Each is one word, so a larger font shrinks a name to its column rather than breaking it
/// mid-word or truncating it. The connector runs centre to centre behind the nodes, `primary` behind a
/// finished step — no green, and no shadow: `successText` measures 2.92:1 on this app's dark surface,
/// and elevation is invisible against it.
///
/// VoiceOver reads each column as one element: "Step 2 of 4, Address", then its state. Built to the same
/// numbers as the Android twin: 32 node, 36 current node, 2 connector, 6 under the node.
struct OnboardingChainHeader: View {
    let currentSection: ProfileSection
    let state: OnboardingChainState

    /// Tapping a step jumps to it. Only ever called for a reachable one.
    let onSelect: (ProfileSection) -> Void

    private var sections: [ProfileSection] {
        ProfileSection.allCases
    }

    private var currentIndex: Int {
        sections.firstIndex(of: currentSection) ?? 0
    }

    /// Reachable = already finished, or already walked past. Not "any step": jumping forward into a
    /// section the chain has not filled yet would leave a gap the chain then has to re-find.
    private func isReachable(_ index: Int) -> Bool {
        state.completionBySection[index] == true || index < currentIndex
    }

    private func isDone(_ index: Int) -> Bool {
        state.completionBySection[index] == true
    }

    var body: some View {
        VStack(spacing: Spacing.m) {
            header
            rail
        }
        .padding(Spacing.m)
        .background(CleansiaColors.surface)
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.medium)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
    }

    private var header: some View {
        HStack(spacing: Spacing.s) {
            Text(L10n.Profile.onboardingStepProgress(currentIndex + 1, state.totalSteps))
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(CleansiaColors.primary)
                .fixedSize(horizontal: true, vertical: false)
            Spacer(minLength: Spacing.xs)
            // Truncates before the counter does: losing "Complete your profile" costs nothing,
            // losing "Step 3 of 4" costs the reader their place.
            Text(L10n.Profile.onboardingHeaderSubtitle)
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .lineLimit(1)
                .truncationMode(.tail)
        }
    }

    /// The segment behind a finished step is the progress indicator. Two tones of primary, never
    /// outlineVariant — slate700 on this card measures 1.51:1.
    private func connector(after index: Int) -> Color {
        isDone(index) ? CleansiaColors.primary : CleansiaColors.primary.opacity(0.24)
    }

    private var rail: some View {
        HStack(alignment: .top, spacing: 0) {
            ForEach(sections.indices, id: \.self) { index in
                let section = sections[index]
                let isCurrent = section == currentSection
                StepNode(
                    icon: Self.icon(for: section),
                    label: Self.label(for: section),
                    position: L10n.Profile.onboardingStepProgress(index + 1, sections.count),
                    state: isCurrent ? .current : (isDone(index) ? .done : .upcoming),
                    isReachable: !isCurrent && isReachable(index),
                    leadingConnector: index == 0 ? nil : connector(after: index - 1),
                    trailingConnector: index == sections.count - 1 ? nil : connector(after: index),
                    onTap: { onSelect(section) }
                )
                .frame(maxWidth: .infinity)
            }
        }
    }

    private static func label(for section: ProfileSection) -> String {
        switch section {
        case .personal: L10n.Profile.onboardingStepPersonal
        case .address: L10n.Profile.onboardingStepAddress
        case .identification: L10n.Profile.onboardingStepIdentification
        case .bank: L10n.Profile.onboardingStepBank
        }
    }

    /// Named, not numbered. Each glyph is already used elsewhere in this app and pairs with a Material
    /// icon of the same shape on Android, so the two platforms read identically.
    private static func icon(for section: ProfileSection) -> String {
        switch section {
        case .personal: "person"
        case .address: "mappin.and.ellipse"
        case .identification: "person.text.rectangle"
        case .bank: "building.columns"
        }
    }
}

/// One step: its node over its name, and the half of each neighbouring connector that falls in its
/// column, so adjacent halves meet between two nodes. The node's own fill hides the line behind it.
private struct StepNode: View {
    enum State {
        case current
        case done
        case upcoming
    }

    let icon: String
    let label: String
    /// "Step 2 of 4", read before the name.
    let position: String
    let state: State
    let isReachable: Bool
    let leadingConnector: Color?
    let trailingConnector: Color?
    let onTap: () -> Void

    private static let node: CGFloat = 32
    private static let currentNode: CGFloat = 36
    private static let nodeRow: CGFloat = 36

    var body: some View {
        VStack(spacing: 6) {
            ZStack {
                HStack(spacing: 0) {
                    (leadingConnector ?? .clear).frame(height: 2)
                    (trailingConnector ?? .clear).frame(height: 2)
                }
                nodeDisc
            }
            .frame(height: Self.nodeRow)
            // Never cut: every name is one word, so at a large font it shrinks to fit its column rather
            // than breaking mid-word onto a second line.
            Text(label)
                .font(CleansiaTypography.labelMedium)
                .fontWeight(state == .current ? .heavy : .semibold)
                .foregroundColor(labelColor)
                .multilineTextAlignment(.center)
                .lineLimit(1)
                .minimumScaleFactor(0.6)
                .padding(.horizontal, 2)
        }
        .padding(.bottom, Spacing.xxs)
        .contentShape(Rectangle())
        .onTapGesture { if isReachable { onTap() } }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(position + ", " + label)
        .accessibilityValue(stateDescription)
        .accessibilityAddTraits(isReachable ? .isButton : [])
        .accessibilityHint(isReachable ? L10n.Profile.onboardingStepJumpHint : "")
        .accessibilityAction { if isReachable { onTap() } }
    }

    private var nodeDisc: some View {
        let size = state == .current ? Self.currentNode : Self.node
        return ZStack {
            Circle()
                .fill(fill)
            if state == .upcoming {
                Circle()
                    .strokeBorder(isReachable ? CleansiaColors.primary : CleansiaColors.outline, lineWidth: 1.5)
            }
            Image(systemName: state == .done ? "checkmark" : icon)
                .font(.system(size: state == .current ? 17 : 15, weight: state == .current ? .semibold : .medium))
                .foregroundColor(glyph)
        }
        .frame(width: size, height: size)
    }

    private var fill: Color {
        switch state {
        case .current: CleansiaColors.primary
        case .done: CleansiaColors.primaryContainer
        case .upcoming: CleansiaColors.surface
        }
    }

    private var glyph: Color {
        switch state {
        case .current: CleansiaColors.onPrimary
        case .done: CleansiaColors.onPrimaryContainer
        case .upcoming: CleansiaColors.onSurfaceVariant
        }
    }

    private var labelColor: Color {
        switch state {
        case .current: CleansiaColors.primary
        case .done: CleansiaColors.onSurface
        case .upcoming: CleansiaColors.onSurfaceVariant
        }
    }

    /// Spoken after the position and the name.
    private var stateDescription: String {
        switch state {
        case .current: L10n.Profile.onboardingStepStateCurrent
        case .done: L10n.Profile.onboardingStepStateDone
        case .upcoming: L10n.Profile.onboardingStepStateUpcoming
        }
    }
}

#if DEBUG
    struct OnboardingChainHeader_Previews: PreviewProvider {
        static var previews: some View {
            VStack(spacing: Spacing.l) {
                OnboardingChainHeader(
                    currentSection: .identification,
                    state: OnboardingChainState(
                        isLoading: false,
                        completionBySection: [0: true, 1: true, 2: false, 3: false]
                    ),
                    onSelect: { _ in }
                )
                OnboardingChainHeader(
                    currentSection: .personal,
                    state: OnboardingChainState(
                        isLoading: false,
                        completionBySection: [0: false, 1: false, 2: false, 3: false]
                    ),
                    onSelect: { _ in }
                )
                OnboardingChainHeader(
                    currentSection: .bank,
                    state: OnboardingChainState(
                        isLoading: false,
                        completionBySection: [0: true, 1: true, 2: true, 3: false]
                    ),
                    onSelect: { _ in }
                )
            }
            .padding()
            .background(CleansiaColors.background)
        }
    }
#endif
