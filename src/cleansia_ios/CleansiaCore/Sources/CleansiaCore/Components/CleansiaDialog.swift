import SwiftUI

public struct CleansiaDialog<Content: View>: View {
    private let title: String
    private let message: String?
    private let confirmLabel: String
    private let dismissLabel: String?
    private let icon: String?
    private let destructive: Bool
    private let confirmEnabled: Bool
    private let onConfirm: () -> Void
    private let onDismiss: () -> Void
    private let content: Content?

    @State private var presented = false

    public init(
        title: String,
        confirmLabel: String,
        onConfirm: @escaping () -> Void,
        onDismiss: @escaping () -> Void,
        message: String? = nil,
        dismissLabel: String? = nil,
        icon: String? = nil,
        destructive: Bool = false,
        confirmEnabled: Bool = true,
        @ViewBuilder content: () -> Content
    ) {
        self.title = title
        self.confirmLabel = confirmLabel
        self.onConfirm = onConfirm
        self.onDismiss = onDismiss
        self.message = message
        self.dismissLabel = dismissLabel
        self.icon = icon
        self.destructive = destructive
        self.confirmEnabled = confirmEnabled
        self.content = content()
    }

    public var body: some View {
        ZStack {
            Color.black.opacity(0.4)
                .ignoresSafeArea()
                .opacity(presented ? 1 : 0)
                .onTapGesture(perform: onDismiss)

            VStack(spacing: 0) {
                if let icon {
                    ZStack {
                        Circle()
                            .fill(destructive ? CleansiaColors.errorContainer : CleansiaColors.primaryContainer)
                            .frame(width: 56, height: 56)
                        Image(systemName: icon)
                            .font(.system(size: 28))
                            .foregroundColor(destructive ? CleansiaColors.onErrorContainer : CleansiaColors
                                .onPrimaryContainer)
                    }
                    .padding(.bottom, Spacing.m)
                }

                Text(title)
                    .font(CleansiaTypography.titleLarge)
                    .foregroundColor(CleansiaColors.onSurface)
                    .multilineTextAlignment(.center)

                if let message {
                    Text(message)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .multilineTextAlignment(.center)
                        .padding(.top, Spacing.xs)
                }

                if let content {
                    content.padding(.top, Spacing.m)
                }

                HStack(spacing: Spacing.s) {
                    if let dismissLabel {
                        DialogButton(
                            label: dismissLabel,
                            background: CleansiaColors.surfaceVariant,
                            foreground: CleansiaColors.onSurface,
                            action: onDismiss
                        )
                    }
                    DialogButton(
                        label: confirmLabel,
                        background: destructive ? CleansiaColors.error : CleansiaColors.primary,
                        foreground: destructive ? CleansiaColors.onError : CleansiaColors.onPrimary,
                        enabled: confirmEnabled,
                        action: onConfirm
                    )
                }
                .padding(.top, Spacing.l)
            }
            .padding(Spacing.l)
            .frame(maxWidth: 420)
            .background(CleansiaColors.surface)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
            .shadow(radius: 24)
            .padding(.horizontal, Spacing.l)
            .scaleEffect(presented ? 1 : 0.85)
            .opacity(presented ? 1 : 0)
        }
        .onAppear {
            withAnimation(.spring(response: 0.4, dampingFraction: 0.62)) { presented = true }
        }
    }
}

public extension CleansiaDialog where Content == EmptyView {
    init(
        title: String,
        confirmLabel: String,
        onConfirm: @escaping () -> Void,
        onDismiss: @escaping () -> Void,
        message: String? = nil,
        dismissLabel: String? = nil,
        icon: String? = nil,
        destructive: Bool = false,
        confirmEnabled: Bool = true
    ) {
        self.init(
            title: title,
            confirmLabel: confirmLabel,
            onConfirm: onConfirm,
            onDismiss: onDismiss,
            message: message,
            dismissLabel: dismissLabel,
            icon: icon,
            destructive: destructive,
            confirmEnabled: confirmEnabled,
            content: { EmptyView() }
        )
    }
}

private struct DialogButton: View {
    let label: String
    let background: Color
    let foreground: Color
    var enabled = true
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Text(label)
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(foreground)
                .frame(maxWidth: .infinity, minHeight: 48)
                .background(background.opacity(enabled ? 1 : 0.5))
                .clipShape(RoundedRectangle(cornerRadius: 14))
        }
        .buttonStyle(.plain)
        .disabled(!enabled)
    }
}

#if DEBUG
    struct CleansiaDialog_Previews: PreviewProvider {
        static var previews: some View {
            CleansiaDialog(
                title: "Delete account?",
                confirmLabel: "Delete",
                onConfirm: {},
                onDismiss: {},
                message: "This permanently removes your account and data.",
                dismissLabel: "Cancel",
                icon: "trash",
                destructive: true
            )
        }
    }
#endif
