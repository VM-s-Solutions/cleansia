import CleansiaCore
import SwiftUI

/// Interim notifications inbox reached from the Home bell. No feed endpoint
/// exists yet (tracked by T-0393), so it always shows the empty state — enough
/// to make the bell a live tap instead of a dead one. Android should get the
/// same interim to keep the platforms in step.
struct NotificationsInboxSheet: View {
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            ZStack {
                CleansiaColors.background.ignoresSafeArea()
                MascotEmptyState(
                    image: Mascot.leaning.image,
                    text: L10n.NotificationsInbox.emptyTitle,
                    subtitle: L10n.NotificationsInbox.emptySubtitle,
                    verticallyCentered: true,
                    imageSize: 160,
                    titleFont: CleansiaTypography.headlineSmall
                )
            }
            .navigationTitle(L10n.NotificationsInbox.title)
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .confirmationAction) {
                    Button(L10n.NotificationsInbox.close) { dismiss() }
                        .tint(CleansiaColors.primary)
                }
            }
        }
    }
}

#if DEBUG
    struct NotificationsInboxSheet_Previews: PreviewProvider {
        static var previews: some View {
            NotificationsInboxSheet()
        }
    }
#endif
