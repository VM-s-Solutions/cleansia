import CleansiaCore
import SwiftUI

/// The one-time ask, sited on the dashboard because that is the only screen every cleaner opens and
/// the app has no other in-place prompt pattern. Both buttons are real answers — "keep every job" IS
/// the country-wide preference, not a "later" — so the card never returns either way.
struct JobRadiusPromptCard: View {
    let onChooseRadius: () -> Void
    let onKeepEveryJob: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(L10n.JobRadius.promptTitle)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
            Text(L10n.JobRadius.promptBody)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .fixedSize(horizontal: false, vertical: true)
            HStack(spacing: Spacing.m) {
                Spacer()
                CleansiaTextLink(L10n.JobRadius.promptKeepAll, action: onKeepEveryJob)
                CleansiaTextLink(L10n.JobRadius.promptChoose, action: onChooseRadius)
            }
            .padding(.top, Spacing.xxs)
        }
        .padding(Spacing.m)
        .background(CleansiaColors.primary.opacity(0.08))
        .clipShape(RoundedRectangle(cornerRadius: 18))
        .padding(.horizontal, Spacing.m)
    }
}

#if DEBUG
    struct JobRadiusPromptCard_Previews: PreviewProvider {
        static var previews: some View {
            JobRadiusPromptCard(onChooseRadius: {}, onKeepEveryJob: {})
                .padding(.vertical, Spacing.m)
                .background(CleansiaColors.background)
                .previewLayout(.sizeThatFits)
        }
    }
#endif
