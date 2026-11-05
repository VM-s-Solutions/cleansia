import SwiftUI

public struct FloatingCircleButton: View {
    let systemIcon: String
    let accessibilityLabel: String
    let action: () -> Void

    public init(systemIcon: String, accessibilityLabel: String, action: @escaping () -> Void) {
        self.systemIcon = systemIcon
        self.accessibilityLabel = accessibilityLabel
        self.action = action
    }

    public var body: some View {
        Button(action: action) {
            Image(systemName: systemIcon)
                .font(.system(size: 18, weight: .semibold))
                .foregroundColor(CleansiaColors.onSurface)
                .frame(width: 44, height: 44)
                .background(CleansiaColors.surface)
                .clipShape(Circle())
                .shadow(color: .black.opacity(0.15), radius: 6, y: 2)
        }
        .accessibilityLabel(accessibilityLabel)
    }
}
