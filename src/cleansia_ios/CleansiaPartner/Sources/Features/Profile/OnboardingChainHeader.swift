import CleansiaCore
import SwiftUI

struct OnboardingChainHeader: View {
    let currentSection: ProfileSection
    let state: OnboardingChainState

    private var sections: [ProfileSection] {
        ProfileSection.allCases
    }

    private var currentIndex: Int {
        sections.firstIndex(of: currentSection) ?? 0
    }

    var body: some View {
        VStack(spacing: Spacing.s) {
            HStack {
                Text(L10n.Profile.onboardingStepProgress(currentIndex + 1, state.totalSteps))
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(CleansiaColors.primary)
                Spacer()
                Text(L10n.Profile.onboardingHeaderSubtitle)
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }

            ProgressView(
                value: state.totalSteps > 0 ? Double(state.completedSteps) / Double(state.totalSteps) : 0
            )
            .tint(CleansiaColors.primary)

            HStack {
                ForEach(sections.indices, id: \.self) { index in
                    SectionDot(
                        index: index + 1,
                        label: Self.label(for: sections[index]),
                        isDone: state.completionBySection[index] == true,
                        isCurrent: sections[index] == currentSection
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

    var body: some View {
        VStack(spacing: Spacing.xxs) {
            ZStack {
                Circle()
                    .fill(dotColor)
                    .frame(width: 32, height: 32)
                if isDone {
                    Image(systemName: "checkmark")
                        .font(.system(size: 14, weight: .semibold))
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
                )
            )
            .padding()
            .background(CleansiaColors.background)
        }
    }
#endif
