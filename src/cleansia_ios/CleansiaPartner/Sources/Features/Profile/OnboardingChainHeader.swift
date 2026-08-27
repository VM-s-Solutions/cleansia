import CleansiaCore
import SwiftUI

/// The onboarding stepper.
///
/// **The current step is a capsule, not a dot.** It grows out of the rail carrying its own icon and
/// its own name, and every other step shrinks to a compact disc. That is the whole idea: the name
/// belongs to the step it describes instead of floating on a line of its own underneath the rail,
/// where it named nothing in particular. The separate title line this used to end with is gone —
/// the pill holds it now, and the card is about 50pt shorter for it.
///
/// Three channels carry state, one bit each, so no single failure of colour perception loses the
/// whole picture:
///
/// - **shape** says where you are — a capsule is the current step, a disc is any other;
/// - **fill** says whether a step is finished — `primaryContainer` behind a checkmark once it is,
///   nothing behind an icon while it is not;
/// - **ring** says whether you may go there — `primary` on a step you can jump to, `outline` on one
///   you cannot.
///
/// **The row fits because the pill is content-sized and the connectors absorb the slack.** At the
/// narrowest supported width the card gives 256pt of content: three 36pt dots and a pill of at most
/// 120pt leave about 9pt for each connector. 120 is the real ceiling and not an estimate — the
/// longest step name in any of the five shipped locales is eight characters (`Особисте`, `Identity`,
/// `Identita`, `Личность`), which is 62pt of `labelLarge` plus 58pt of disc, gaps and insets.
///
/// **No green, and no shadow.** Reference designs for this pattern are drawn on white: `successText`
/// measures 2.92:1 on this app's dark surface, and elevation is invisible against it. Progress is
/// carried by the connector tinting `primary` behind you, which is also why the separate progress bar
/// this used to sit above is gone — it said the same thing twice.
struct OnboardingChainHeader: View {
    let currentSection: ProfileSection
    let state: OnboardingChainState

    /// Tapping a dot jumps to that step. Only ever called for a reachable one.
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

    /// One content-sized pill, three fixed dots, and connectors that take whatever is left, so the
    /// rail spans the card at any width without the segments drawing at unequal lengths.
    private var rail: some View {
        HStack(spacing: 0) {
            ForEach(sections.indices, id: \.self) { index in
                step(at: index)
                if index < sections.count - 1 {
                    // The segment behind a finished step is the progress indicator. Two tones of
                    // primary, never outlineVariant — slate700 on this card measures 1.51:1.
                    Rectangle()
                        .fill(isDone(index) ? CleansiaColors.primary : CleansiaColors.primary.opacity(0.24))
                        .frame(height: 2)
                        .frame(maxWidth: .infinity)
                }
            }
        }
    }

    @ViewBuilder
    private func step(at index: Int) -> some View {
        let section = sections[index]
        if section == currentSection {
            StepPill(icon: Self.icon(for: section), label: Self.label(for: section))
        } else {
            StepDot(
                icon: Self.icon(for: section),
                isDone: isDone(index),
                isReachable: isReachable(index),
                label: Self.label(for: section),
                onTap: { onSelect(section) }
            )
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

/// The current step. Content-sized on purpose: it is the one element allowed to claim whatever width
/// its label needs, because the connectors either side give that width up.
private struct StepPill: View {
    let icon: String
    let label: String

    private static let height: CGFloat = 40
    private static let disc: CGFloat = 26

    var body: some View {
        HStack(spacing: Spacing.xs) {
            ZStack {
                // A wash of the pill's own ink, not a second palette colour — it has to read as an
                // inset in the capsule rather than a separate badge sitting on top of it.
                Circle()
                    .fill(CleansiaColors.onPrimary.opacity(0.22))
                    .frame(width: Self.disc, height: Self.disc)
                Image(systemName: icon)
                    .font(.system(size: 15, weight: .semibold))
                    .foregroundColor(CleansiaColors.onPrimary)
            }
            // No lineLimit and no minimumScaleFactor anywhere in this view: the pill is sized by its
            // text, so there is nothing for the text to be squeezed into.
            Text(label)
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(CleansiaColors.onPrimary)
                .fixedSize(horizontal: true, vertical: false)
        }
        .padding(.leading, Spacing.xs)
        .padding(.trailing, Spacing.m)
        .frame(height: Self.height)
        .background(CleansiaColors.primary)
        .clipShape(Capsule())
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(label)
        .accessibilityValue(L10n.Profile.onboardingStepStateCurrent)
    }
}

/// Any step that is not the current one. 30pt of disc inside a 36pt target — smaller than the 48 this
/// used to draw, because the pill has to fit on the same row at 320pt and something had to give.
private struct StepDot: View {
    let icon: String
    let isDone: Bool
    let isReachable: Bool
    let label: String
    let onTap: () -> Void

    @State private var isPressed = false

    private static let disc: CGFloat = 30
    private static let target: CGFloat = 36

    var body: some View {
        ZStack {
            Circle()
                .fill(fill)
                .frame(width: Self.disc, height: Self.disc)

            Circle()
                .strokeBorder(ring, lineWidth: isDone ? 0 : 1.5)
                .frame(width: Self.disc, height: Self.disc)

            Image(systemName: isDone ? "checkmark" : icon)
                .font(.system(size: 16, weight: .medium))
                .foregroundColor(glyph)
        }
        .frame(width: Self.target, height: Self.target)
        .contentShape(Rectangle())
        .scaleEffect(isPressed ? 0.94 : 1)
        .animation(.easeOut(duration: 0.12), value: isPressed)
        .onLongPressGesture(
            minimumDuration: 0,
            pressing: { pressing in if isReachable { isPressed = pressing } },
            perform: { if isReachable { onTap() } }
        )
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(label)
        .accessibilityValue(accessibilityState)
        .accessibilityAddTraits(isReachable ? .isButton : [])
        .accessibilityHint(isReachable ? L10n.Profile.onboardingStepJumpHint : "")
    }

    private var fill: Color {
        if isDone { return CleansiaColors.primaryContainer }
        return .clear
    }

    private var ring: Color {
        if isReachable { return CleansiaColors.primary }
        return CleansiaColors.outline
    }

    private var glyph: Color {
        if isDone { return CleansiaColors.onPrimaryContainer }
        return CleansiaColors.onSurfaceVariant
    }

    /// Spoken after the section name. Without it VoiceOver announced three identical-sounding controls
    /// and said nothing about which was finished.
    private var accessibilityState: String {
        if isDone { return L10n.Profile.onboardingStepStateDone }
        return L10n.Profile.onboardingStepStateUpcoming
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
