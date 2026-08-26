import CleansiaCore
import SwiftUI

/// The onboarding stepper.
///
/// **Rebuilt from the plain numbered dots it used to be.** Three things carry state now, one bit each,
/// so no single failure of colour perception loses the whole picture:
///
/// - the **disc fill** says where you are — solid `primary` for the current step, a washed
///   `primaryContainer` for one that is finished, nothing at all for one you have not reached;
/// - the **glyph** says whether it is finished — a checkmark once it is, otherwise an icon that names
///   the step, because a bare ordinal tells a cleaner nothing about what is being asked of them;
/// - the **ring** says whether you may go there — `primary` on a step you can jump to, `outline` on
///   one you cannot.
///
/// **Only the current step is named.** Four Cyrillic labels do not fit across four medallions on a
/// 320dp screen: the cell budget is 64dp and "Идентификация" needs roughly twice that, so the row
/// would either truncate every label to noise or wrap into a ragged third band. The connector rail
/// carries the *shape* of the journey and one prominent title says where you are in it, which is the
/// question a label under every dot was answering badly.
///
/// **No green, and no shadow.** Reference designs for this pattern are drawn on white: `successText`
/// measures 2.92:1 on this app's dark surface, and elevation is invisible against it. Progress is
/// carried by the connector tinting `primary` behind you, which is also why the separate progress bar
/// this used to sit above is gone — it said the same thing twice.
struct OnboardingChainHeader: View {
    let currentSection: ProfileSection
    let state: OnboardingChainState

    /// Tapping a medallion jumps to that step. Only ever called for a reachable one.
    let onSelect: (ProfileSection) -> Void

    private var sections: [ProfileSection] { ProfileSection.allCases }

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
            Text(Self.label(for: currentSection))
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .lineLimit(1)
                .minimumScaleFactor(0.8)
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

    /// Fixed-width medallions with flexible connectors between them, so the rail spans the card at any
    /// width without the segments drawing at unequal lengths.
    private var rail: some View {
        HStack(spacing: 0) {
            ForEach(sections.indices, id: \.self) { index in
                StepMedallion(
                    icon: Self.icon(for: sections[index]),
                    isDone: isDone(index),
                    isCurrent: sections[index] == currentSection,
                    isReachable: isReachable(index),
                    label: Self.label(for: sections[index]),
                    onTap: { onSelect(sections[index]) }
                )
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

private struct StepMedallion: View {
    let icon: String
    let isDone: Bool
    let isCurrent: Bool
    let isReachable: Bool
    let label: String
    let onTap: () -> Void

    @State private var isPressed = false

    private static let disc: CGFloat = 40
    private static let target: CGFloat = 48

    var body: some View {
        ZStack {
            // The halo is the only thing that makes the current step read as "lifted" — this theme has
            // no usable elevation, so depth has to come from tint.
            if isCurrent {
                Circle()
                    .fill(CleansiaColors.primary.opacity(0.22))
                    .frame(width: Self.target, height: Self.target)
            }

            Circle()
                .fill(fill)
                .frame(width: Self.disc, height: Self.disc)

            Circle()
                .strokeBorder(ring, lineWidth: isCurrent ? 0 : 1.5)
                .frame(width: Self.disc, height: Self.disc)

            Image(systemName: isDone ? "checkmark" : icon)
                .font(.system(size: 20, weight: .medium))
                .foregroundColor(glyph)
        }
        .frame(width: Self.target, height: Self.target)
        .contentShape(Rectangle())
        .scaleEffect(isPressed ? 0.94 : 1)
        .animation(.easeOut(duration: 0.12), value: isPressed)
        .onLongPressGesture(
            minimumDuration: 0,
            pressing: { pressing in if isReachable { isPressed = pressing } },
            perform: { if isReachable, !isCurrent { onTap() } }
        )
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(label)
        .accessibilityValue(accessibilityState)
        .accessibilityAddTraits(isReachable && !isCurrent ? .isButton : [])
        .accessibilityHint(isReachable && !isCurrent ? L10n.Profile.onboardingStepJumpHint : "")
    }

    private var fill: Color {
        if isCurrent { return CleansiaColors.primary }
        if isDone { return CleansiaColors.primaryContainer }
        return .clear
    }

    private var ring: Color {
        if isReachable { return CleansiaColors.primary }
        return CleansiaColors.outline
    }

    private var glyph: Color {
        if isCurrent { return CleansiaColors.onPrimary }
        if isDone { return CleansiaColors.onPrimaryContainer }
        return CleansiaColors.onSurfaceVariant
    }

    /// Spoken after the section name. Without it VoiceOver announced four identical-sounding controls
    /// and said nothing about which was finished or which you were on.
    private var accessibilityState: String {
        if isCurrent { return L10n.Profile.onboardingStepStateCurrent }
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
            }
            .padding()
            .background(CleansiaColors.background)
        }
    }
#endif
