import CleansiaCore
import SwiftUI

/// The onboarding stepper. Drawn identically on both platforms — where the two drifted, the
/// reconciliation is noted inline so the next reader knows which side moved and why.
struct OnboardingChainHeader: View {
    let currentSection: ProfileSection
    let state: OnboardingChainState

    /// Tapping a dot jumps to that section. Nil-safe by construction: only dots the cleaner may
    /// actually reach are given the gesture, so this never fires for a step they have not seen.
    let onSelect: (ProfileSection) -> Void

    private var sections: [ProfileSection] {
        ProfileSection.allCases
    }

    private var currentIndex: Int {
        sections.firstIndex(of: currentSection) ?? 0
    }

    /// Reachable = already finished, or already walked past. Not "any step": jumping forward into a
    /// section the chain has not filled in yet would leave a gap the chain then has to re-find, and
    /// the whole point of the chain is that it hands you the next missing thing.
    private func isReachable(_ index: Int) -> Bool {
        state.completionBySection[index] == true || index < currentIndex
    }

    var body: some View {
        // 8 then 16 rather than a uniform 12: the bar belongs to the "Step N of 4" line above it, and
        // the dots are a separate block. Android already drew it this way.
        VStack(spacing: 0) {
            HStack {
                Text(L10n.Profile.onboardingStepProgress(currentIndex + 1, state.totalSteps))
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(CleansiaColors.primary)
                Spacer()
                Text(L10n.Profile.onboardingHeaderSubtitle)
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }

            Spacer().frame(height: Spacing.xs)

            // A 6pt pill with an explicit track, not the system default 4pt square-ended bar. The
            // default reads as a system control rather than part of this card.
            ProgressView(
                value: state.totalSteps > 0 ? Double(state.completedSteps) / Double(state.totalSteps) : 0
            )
            .tint(CleansiaColors.primary)
            .background(CleansiaColors.surfaceVariant)
            .frame(height: 6)
            .clipShape(Capsule())

            Spacer().frame(height: Spacing.m)

            HStack {
                ForEach(sections.indices, id: \.self) { index in
                    SectionDot(
                        index: index + 1,
                        label: Self.label(for: sections[index]),
                        isDone: state.completionBySection[index] == true,
                        isCurrent: sections[index] == currentSection,
                        isReachable: isReachable(index),
                        onTap: { onSelect(sections[index]) }
                    )
                    if index < sections.count - 1 {
                        Spacer()
                    }
                }
            }
        }
        .padding(Spacing.m)
        .background(CleansiaColors.primary.opacity(0.08))
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
    }

    private static func label(for section: ProfileSection) -> String {
        switch section {
        case .personal: L10n.Profile.onboardingStepPersonal
        case .address: L10n.Profile.onboardingStepAddress
        case .identification: L10n.Profile.onboardingStepIdentification
        case .bank: L10n.Profile.onboardingStepBank
        }
    }
}

private struct SectionDot: View {
    let index: Int
    let label: String
    let isDone: Bool
    let isCurrent: Bool
    let isReachable: Bool
    let onTap: () -> Void

    var body: some View {
        content
            // The gesture goes on the whole dot+label column, not the 32pt circle: a 32pt target is
            // under the 44pt minimum, and the label is the part people aim at.
            .contentShape(Rectangle())
            .onTapGesture { if isReachable { onTap() } }
            .accessibilityElement(children: .combine)
            .accessibilityAddTraits(isReachable ? .isButton : [])
    }

    private var content: some View {
        VStack(spacing: Spacing.xxs) {
            ZStack {
                Circle()
                    .fill(dotColor)
                    .frame(width: 32, height: 32)
                if isDone {
                    // 18, matching Android. At 14 the tick read smaller than the numeral it replaces.
                    Image(systemName: "checkmark")
                        .font(.system(size: 18, weight: .semibold))
                        .foregroundColor(CleansiaColors.onPrimary)
                } else {
                    Text(verbatim: "\(index)")
                        .font(CleansiaTypography.labelLarge)
                        .foregroundColor(isCurrent ? CleansiaColors.onPrimary : CleansiaColors.onSurface)
                }
            }
            Text(label)
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(isCurrent ? CleansiaColors.primary : CleansiaColors.onSurfaceVariant)
        }
    }

    private var dotColor: Color {
        if isCurrent { return CleansiaColors.primary }
        if isDone { return CleansiaColors.primary.opacity(0.6) }
        return CleansiaColors.surfaceVariant
    }
}

#if DEBUG
    struct OnboardingChainHeader_Previews: PreviewProvider {
        static var previews: some View {
            OnboardingChainHeader(
                currentSection: .address,
                state: OnboardingChainState(
                    isLoading: false,
                    completionBySection: [0: true, 1: false, 2: false, 3: false]
                ),
                onSelect: { _ in }
            )
            .padding()
            .background(CleansiaColors.background)
        }
    }
#endif
