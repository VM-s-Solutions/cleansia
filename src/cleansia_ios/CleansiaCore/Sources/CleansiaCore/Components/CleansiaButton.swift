import SwiftUI

public enum CleansiaButtonSize {
    case small
    case medium
    case large

    var minHeight: CGFloat {
        switch self {
        case .small: 40
        case .medium: 48
        case .large: 56
        }
    }

    var horizontalPadding: CGFloat {
        switch self {
        case .small: 16
        case .medium: 24
        case .large: 32
        }
    }
}

public struct CleansiaPrimaryButton: View {
    private let text: String
    private let size: CleansiaButtonSize
    private let leadingIcon: String?
    private let trailingIcon: String?
    private let loading: Bool
    private let enabled: Bool
    private let action: () -> Void

    public init(
        _ text: String,
        size: CleansiaButtonSize = .large,
        leadingIcon: String? = nil,
        trailingIcon: String? = nil,
        loading: Bool = false,
        enabled: Bool = true,
        action: @escaping () -> Void
    ) {
        self.text = text
        self.size = size
        self.leadingIcon = leadingIcon
        self.trailingIcon = trailingIcon
        self.loading = loading
        self.enabled = enabled
        self.action = action
    }

    public var body: some View {
        Button(action: action) {
            ZStack {
                if loading {
                    ProgressView()
                        .progressViewStyle(.circular)
                        .tint(CleansiaColors.onPrimary)
                } else {
                    HStack(spacing: Spacing.xs) {
                        if let leadingIcon {
                            Image(systemName: leadingIcon).font(.system(size: 18))
                        }
                        Text(text).font(CleansiaTypography.titleMedium)
                        if let trailingIcon {
                            Image(systemName: trailingIcon).font(.system(size: 18))
                        }
                    }
                }
            }
            .frame(maxWidth: .infinity, minHeight: size.minHeight)
            .padding(.horizontal, size.horizontalPadding)
            .foregroundColor(CleansiaColors.onPrimary)
            .background(CleansiaColors.primary.opacity(enabled && !loading ? 1 : 0.5))
            .clipShape(Capsule())
        }
        .disabled(!enabled || loading)
    }
}

public struct CleansiaOutlinedButton: View {
    private let text: String
    private let size: CleansiaButtonSize
    private let leadingIcon: String?
    private let enabled: Bool
    private let action: () -> Void

    public init(
        _ text: String,
        size: CleansiaButtonSize = .large,
        leadingIcon: String? = nil,
        enabled: Bool = true,
        action: @escaping () -> Void
    ) {
        self.text = text
        self.size = size
        self.leadingIcon = leadingIcon
        self.enabled = enabled
        self.action = action
    }

    public var body: some View {
        Button(action: action) {
            HStack(spacing: Spacing.s) {
                if let leadingIcon {
                    Image(systemName: leadingIcon).font(.system(size: 20))
                }
                Text(text).font(CleansiaTypography.titleMedium)
            }
            .frame(maxWidth: .infinity, minHeight: size.minHeight)
            .padding(.horizontal, size.horizontalPadding)
            .foregroundColor(CleansiaColors.onSurface)
            .overlay(Capsule().stroke(CleansiaColors.outline, lineWidth: 1))
        }
        .disabled(!enabled)
    }
}

/// The destructive/danger affordance — error-tinted surface with an error-color
/// glyph + label and a hairline error border. Theme-adaptive by construction (it
/// never puts `onError` text on an `error` fill, which collapses to dark-red-on-red
/// in dark mode), so it stays legible in both schemes.
public struct CleansiaDangerButton: View {
    private let text: String
    private let size: CleansiaButtonSize
    private let leadingIcon: String?
    private let loading: Bool
    private let enabled: Bool
    private let action: () -> Void

    public init(
        _ text: String,
        size: CleansiaButtonSize = .large,
        leadingIcon: String? = nil,
        loading: Bool = false,
        enabled: Bool = true,
        action: @escaping () -> Void
    ) {
        self.text = text
        self.size = size
        self.leadingIcon = leadingIcon
        self.loading = loading
        self.enabled = enabled
        self.action = action
    }

    public var body: some View {
        Button(role: .destructive, action: action) {
            ZStack {
                if loading {
                    ProgressView()
                        .progressViewStyle(.circular)
                        .tint(CleansiaColors.error)
                } else {
                    HStack(spacing: Spacing.xs) {
                        if let leadingIcon {
                            Image(systemName: leadingIcon).font(.system(size: 16, weight: .semibold))
                        }
                        Text(text).font(CleansiaTypography.titleMedium)
                    }
                }
            }
            .frame(maxWidth: .infinity, minHeight: size.minHeight)
            .padding(.horizontal, size.horizontalPadding)
            .foregroundColor(CleansiaColors.error)
            .background(
                CleansiaColors.error.opacity(0.12),
                in: RoundedRectangle(cornerRadius: CornerRadius.large)
            )
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.large)
                    .stroke(CleansiaColors.error.opacity(0.4), lineWidth: 1)
            )
            .opacity(enabled && !loading ? 1 : 0.55)
        }
        .buttonStyle(.plain)
        .disabled(!enabled || loading)
    }
}

public struct CleansiaTextLink: View {
    private let text: String
    private let action: () -> Void

    public init(_ text: String, action: @escaping () -> Void) {
        self.text = text
        self.action = action
    }

    public var body: some View {
        Button(action: action) {
            Text(text)
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(CleansiaColors.primary)
                .padding(Spacing.xxs)
        }
        .buttonStyle(.plain)
    }
}

#if DEBUG
    struct CleansiaButton_Previews: PreviewProvider {
        static var previews: some View {
            VStack(spacing: Spacing.m) {
                CleansiaPrimaryButton("Continue", trailingIcon: "arrow.right") {}
                CleansiaPrimaryButton("Loading", loading: true) {}
                CleansiaPrimaryButton("Disabled", enabled: false) {}
                CleansiaOutlinedButton("Continue with Google", leadingIcon: "globe") {}
                CleansiaDangerButton("Delete account", leadingIcon: "trash") {}
                CleansiaTextLink("Forgot password?") {}
            }
            .padding()
        }
    }
#endif
