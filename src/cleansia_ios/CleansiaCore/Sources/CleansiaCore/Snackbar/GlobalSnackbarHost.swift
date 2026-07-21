import SwiftUI

public extension View {
    /// A `nil` inset follows `controller.bottomInset` (the shell-scoped lift);
    /// hosts at modal-sheet roots pin an explicit value instead.
    func snackbarHost(_ controller: SnackbarController, bottomInset: CGFloat? = nil) -> some View {
        modifier(SnackbarHostModifier(controller: controller, pinnedInset: bottomInset))
    }
}

struct SnackbarHostModifier: ViewModifier {
    @ObservedObject var controller: SnackbarController
    let pinnedInset: CGFloat?

    private var bottomInset: CGFloat {
        pinnedInset ?? controller.bottomInset
    }

    func body(content: Content) -> some View {
        content.overlay(alignment: .bottom) {
            if let message = controller.current {
                SnackbarPill(message: message) { controller.dismiss(id: message.id) }
                    .padding(.horizontal, 16)
                    .padding(.bottom, bottomInset)
                    .transition(.move(edge: .bottom).combined(with: .opacity))
                    .task(id: message.id) {
                        try? await Task.sleep(nanoseconds: UInt64(message.autoDismissDuration * 1_000_000_000))
                        controller.dismiss(id: message.id)
                    }
            }
        }
        .animation(.easeInOut(duration: 0.25), value: controller.current?.id)
    }
}

struct SnackbarPill: View {
    let message: SnackbarMessage
    let onDismiss: () -> Void

    private let cornerRadius: CGFloat = 20

    var body: some View {
        let palette = SnackbarPalette.palette(for: message.severity)
        HStack(spacing: Spacing.s) {
            severityBadge(palette)
            Text(message.text)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .frame(maxWidth: .infinity, alignment: .leading)
            Button(action: onDismiss) {
                Image(systemName: "xmark")
                    .font(.system(size: 12, weight: .bold))
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .frame(width: 28, height: 28)
            }
            .accessibilityLabel(Text(CoreL10n.localized("snackbar.dismiss")))
        }
        .padding(.vertical, 10)
        .padding(.leading, Spacing.s)
        .padding(.trailing, Spacing.xs)
        .background(pillBackground(accent: palette.accent))
        .clipShape(RoundedRectangle(cornerRadius: cornerRadius, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
                .strokeBorder(CleansiaColors.outlineVariant.opacity(0.6), lineWidth: 0.5)
        )
        .shadow(color: .black.opacity(0.22), radius: 18, y: 8)
    }

    private func severityBadge(_ palette: SnackbarPalette.Palette) -> some View {
        ZStack {
            Circle().fill(palette.accent)
            Image(systemName: palette.symbol)
                .font(.system(size: 15, weight: .bold))
                .foregroundColor(.white)
        }
        .frame(width: 30, height: 30)
        .accessibilityHidden(true)
    }

    private func pillBackground(accent: Color) -> some View {
        RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
            .fill(CleansiaColors.surface)
            .overlay(alignment: .leading) {
                Capsule()
                    .fill(accent)
                    .frame(width: 4)
                    .padding(.vertical, 12)
                    .padding(.leading, 5)
            }
    }
}

enum SnackbarPalette {
    struct Palette {
        let accent: Color
        let symbol: String
    }

    static func palette(for severity: SnackbarSeverity) -> Palette {
        switch severity {
        case .error:
            Palette(accent: CleansiaColors.error, symbol: "exclamationmark.circle.fill")
        case .success:
            Palette(accent: successAccent, symbol: "checkmark.circle.fill")
        case .info:
            Palette(accent: CleansiaColors.primary, symbol: "info.circle.fill")
        case .warning:
            Palette(accent: warningAccent, symbol: "exclamationmark.triangle.fill")
        }
    }

    private static let successAccent = Color.dynamic(light: Color(hex: 0x16A34A), dark: Color(hex: 0x22C55E))
    private static let warningAccent = Color.dynamic(light: Color(hex: 0xF59E0B), dark: Color(hex: 0xFBBF24))
}

#if DEBUG
    struct SnackbarPill_Previews: PreviewProvider {
        static var previews: some View {
            ForEach([ColorScheme.light, .dark], id: \.self) { scheme in
                VStack(spacing: 12) {
                    SnackbarPill(message: SnackbarMessage(text: "Order could not be cancelled", severity: .error)) {}
                    SnackbarPill(message: SnackbarMessage(text: "Saved", severity: .success)) {}
                    SnackbarPill(message: SnackbarMessage(text: "Heads up", severity: .info)) {}
                    SnackbarPill(message: SnackbarMessage(text: "Careful now", severity: .warning)) {}
                }
                .padding()
                .background(CleansiaColors.background)
                .environment(\.colorScheme, scheme)
                .previewDisplayName(scheme == .light ? "Light" : "Dark")
            }
            .previewLayout(.sizeThatFits)
        }
    }
#endif
