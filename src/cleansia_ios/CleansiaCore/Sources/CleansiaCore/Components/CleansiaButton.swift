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
                        // A label that outgrows its button truncates; it does not wrap and
                        // it is not scaled. Owner ruling 2026-08-27, ratifying what Android
                        // has always shipped — without this the capsule grows a second line
                        // in cs/sk/uk/ru and the row it sits in reflows around it.
                        // The full string still reaches VoiceOver, which is what makes
                        // truncating the VISUAL label acceptable; a caller-side substring
                        // would not be.
                        Text(text)
                            .font(CleansiaTypography.titleMedium)
                            .lineLimit(1)
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

/// Colour resolution for `CleansiaOutlinedButton`, hoisted out of the view so the
/// neutral default and the border-follows-content rule are assertable without a
/// snapshot harness.
///
/// The colour has to arrive through `init`, not `.tint()`: an explicit
/// `foregroundColor` outranks the environment tint, and `stroke` takes a literal
/// `ShapeStyle` that never consults the environment at all. A `.tint(...)` on this
/// button is silently dead, which is how the order-detail Cancel/Report actions
/// shipped uniformly grey.
enum CleansiaOutlinedButtonColors {
    static func content(_ contentColor: Color?) -> Color {
        contentColor ?? CleansiaColors.onSurface
    }

    /// The border follows the content colour unless overridden, because Android
    /// colours both slots identically on every tinted `OutlinedButton` and a red
    /// label inside a grey capsule reads as a rendering fault rather than as a
    /// destructive action.
    static func border(content: Color?, border: Color?) -> Color {
        border ?? content ?? CleansiaColors.outline
    }
}

public struct CleansiaOutlinedButton: View {
    private let text: String
    private let size: CleansiaButtonSize
    private let leadingIcon: String?
    private let contentColor: Color?
    private let borderColor: Color?
    private let enabled: Bool
    private let action: () -> Void

    public init(
        _ text: String,
        size: CleansiaButtonSize = .large,
        leadingIcon: String? = nil,
        contentColor: Color? = nil,
        borderColor: Color? = nil,
        enabled: Bool = true,
        action: @escaping () -> Void
    ) {
        self.text = text
        self.size = size
        self.leadingIcon = leadingIcon
        self.contentColor = contentColor
        self.borderColor = borderColor
        self.enabled = enabled
        self.action = action
    }

    public var body: some View {
        Button(action: action) {
            HStack(spacing: Spacing.s) {
                if let leadingIcon {
                    Image(systemName: leadingIcon).font(.system(size: 20))
                }
                Text(text)
                    .font(CleansiaTypography.titleMedium)
                    .lineLimit(1)
            }
            .frame(maxWidth: .infinity, minHeight: size.minHeight)
            .padding(.horizontal, size.horizontalPadding)
            .foregroundColor(CleansiaOutlinedButtonColors.content(contentColor))
            .overlay(
                Capsule().stroke(
                    CleansiaOutlinedButtonColors.border(content: contentColor, border: borderColor),
                    lineWidth: 1
                )
            )
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

    /// Actionable right now — neither disabled by the caller nor mid-request.
    private var isActive: Bool {
        enabled && !loading
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
                        Text(text)
                            .font(CleansiaTypography.titleMedium)
                            .lineLimit(1)
                    }
                }
            }
            .frame(maxWidth: .infinity, minHeight: size.minHeight)
            .padding(.horizontal, size.horizontalPadding)
            // Disabled changes the COLOURS, it does not fade the stack.
            //
            // A blanket `.opacity(0.55)` faded the red label and its red-tinted background by the same
            // amount, so the two moved together and the contrast between them collapsed — the button
            // read as an unlabelled mauve slab. On the cancellation sheet, where the destructive button
            // is disabled until the customer picks a reason, that is the first thing they see.
            //
            // Muting to a neutral instead keeps the text legible while removing the red's urgency,
            // which is what "not available yet" should look like.
            .foregroundColor(isActive ? CleansiaColors.error : CleansiaColors.onSurfaceVariant)
            .background(
                isActive
                    ? CleansiaColors.error.opacity(0.12)
                    : CleansiaColors.onSurfaceVariant.opacity(0.10),
                in: RoundedRectangle(cornerRadius: CornerRadius.large)
            )
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.large)
                    .stroke(
                        isActive
                            ? CleansiaColors.error.opacity(0.4)
                            : CleansiaColors.outlineVariant,
                        lineWidth: 1
                    )
            )
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
                .lineLimit(1)
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
                CleansiaOutlinedButton(
                    "Cancel order",
                    leadingIcon: "xmark.circle",
                    contentColor: CleansiaColors.error
                ) {}
                CleansiaDangerButton("Delete account", leadingIcon: "trash") {}
                CleansiaTextLink("Forgot password?") {}
            }
            .padding()
        }
    }
#endif
