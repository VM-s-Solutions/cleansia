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
                .foregroundColor(CleansiaColors.onInverseSurface)
                .frame(maxWidth: .infinity, alignment: .leading)
            Button(action: onDismiss) {
                Image(systemName: "xmark")
                    .font(.system(size: 12, weight: .bold))
                    .foregroundColor(CleansiaColors.onInverseSurfaceVariant)
                    .frame(width: 28, height: 28)
            }
            .accessibilityLabel(Text(CoreL10n.localized("snackbar.dismiss")))
        }
        .padding(.vertical, 10)
        .padding(.leading, Spacing.s)
        .padding(.trailing, Spacing.xs)
        .background(pillBackground)
        .clipShape(RoundedRectangle(cornerRadius: cornerRadius, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
                .strokeBorder(CleansiaColors.onInverseSurface.opacity(0.12), lineWidth: 0.5)
        )
        .shadow(color: .black.opacity(0.22), radius: 18, y: 8)
    }

    private func severityBadge(_ palette: SnackbarPalette.Palette) -> some View {
        // Solid accent disc + a plain white glyph. Plain glyphs (not the
        // `.circle.fill` variants, whose inner mark is optically offset inside
        // the symbol) get centered on their tight bounds, so they sit dead in
        // the middle of the disc.
        Image(systemName: palette.symbol)
            .font(.system(size: 14, weight: .bold))
            .foregroundColor(.white)
            .frame(width: 30, height: 30)
            .background(Circle().fill(palette.accent))
            .accessibilityHidden(true)
    }

    private var pillBackground: some View {
        RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
            .fill(CleansiaColors.inverseSurface)
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
            Palette(accent: error, symbol: "exclamationmark")
        case .success:
            Palette(accent: success, symbol: "checkmark")
        case .info:
            Palette(accent: info, symbol: "info")
        case .warning:
            Palette(accent: warning, symbol: "exclamationmark")
        }
    }

    // Solid accents (the deeper 600 tone in light, 500 in dark) — enough
    // contrast to carry a white glyph.
    private static let success = Color.dynamic(light: Color(hex: 0x16A34A), dark: Color(hex: 0x22C55E))
    private static let error = Color.dynamic(light: Color(hex: 0xDC2626), dark: Color(hex: 0xEF4444))
    // Info rides the sky brand ramp (light = sky-600, i.e. CleansiaColors.primary).
    private static let info = Color.dynamic(light: Color(hex: 0x0284C7), dark: Color(hex: 0x0EA5E9))
    private static let warning = Color.dynamic(light: Color(hex: 0xD97706), dark: Color(hex: 0xF59E0B))
}

#if DEBUG
    struct SnackbarPill_Previews: PreviewProvider {
        static var previews: some View {
            ForEach([ColorScheme.light, .dark], id: \.self) { scheme in
                VStack(spacing: 12) {
                    SnackbarPill(message: SnackbarMessage(text: "Order could not be cancelled", severity: .error)) {}
                    SnackbarPill(message: SnackbarMessage(text: "Booking saved", severity: .success)) {}
                    SnackbarPill(message: SnackbarMessage(text: "Heads up — check your details", severity: .info)) {}
                    SnackbarPill(message: SnackbarMessage(text: "Careful now", severity: .warning)) {}
                }
                .frame(maxWidth: 380)
                .padding()
                .background(CleansiaColors.background)
                .environment(\.colorScheme, scheme)
                .previewDisplayName(scheme == .light ? "Light" : "Dark")
            }
            .previewLayout(.sizeThatFits)
        }
    }
#endif
