import SwiftUI

/// The bell carrying the unread badge ("99+" capped, hidden at zero); opens the
/// notifications inbox.
public struct NotificationBell: View {
    let unreadCount: Int
    let copy: NotificationsInboxCopy.Type
    let onTap: () -> Void

    public init(unreadCount: Int, copy: NotificationsInboxCopy.Type, onTap: @escaping () -> Void) {
        self.unreadCount = unreadCount
        self.copy = copy
        self.onTap = onTap
    }

    private var unreadBadge: String? {
        NotificationBadgeModel.label(for: unreadCount)
    }

    public var body: some View {
        Button(action: onTap) {
            Image(systemName: "bell")
                .cleansiaBounce(onIncreaseOf: unreadCount)
                .font(.system(size: 18))
                .foregroundColor(CleansiaColors.onSurface)
                .frame(width: 40, height: 40)
                .background(Circle().fill(CleansiaColors.surface))
                .overlay(Circle().stroke(CleansiaColors.outlineVariant, lineWidth: 1))
                .overlay(alignment: .topTrailing) {
                    if let unreadBadge {
                        Text(verbatim: unreadBadge)
                            .font(CleansiaTypography.labelSmall)
                            .foregroundColor(.white)
                            .padding(.horizontal, 5)
                            .padding(.vertical, 1)
                            .background(CleansiaColors.error, in: Capsule())
                            .offset(x: 4, y: -2)
                    }
                }
                .padding(Spacing.xxs)
                .contentShape(Circle())
        }
        .buttonStyle(.plain)
        .accessibilityLabel(Text(verbatim: copy.title))
        .accessibilityValue(
            Text(verbatim: unreadBadge.map { copy.unreadA11y($0) } ?? "")
        )
    }
}
