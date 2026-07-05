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

    var body: some View {
        let palette = SnackbarPalette.palette(for: message.severity)
        HStack(spacing: 12) {
            Image(systemName: palette.symbol)
                .font(.system(size: 18, weight: .semibold))
                .foregroundColor(palette.foreground)
            Text(message.text)
                .font(.subheadline.weight(.medium))
                .foregroundColor(palette.foreground)
                .frame(maxWidth: .infinity, alignment: .leading)
            Button(action: onDismiss) {
                Image(systemName: "xmark")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundColor(palette.foreground)
            }
            .accessibilityLabel(Text("snackbar.dismiss", bundle: .module))
        }
        .padding(.vertical, 12)
        .padding(.horizontal, 14)
        .background(
            RoundedRectangle(cornerRadius: 14, style: .continuous)
                .fill(palette.background)
        )
        .shadow(color: .black.opacity(0.18), radius: 12, y: 4)
    }
}

enum SnackbarPalette {
    struct Palette {
        let background: Color
        let foreground: Color
        let symbol: String
    }

    static func palette(for severity: SnackbarSeverity) -> Palette {
        switch severity {
        case .error:
            Palette(
                background: Color(red: 0.996, green: 0.886, blue: 0.886),
                foreground: Color(red: 0.725, green: 0.110, blue: 0.110),
                symbol: "exclamationmark.circle"
            )
        case .success:
            Palette(
                background: Color(red: 0.863, green: 0.988, blue: 0.906),
                foreground: Color(red: 0.082, green: 0.502, blue: 0.239),
                symbol: "checkmark.circle"
            )
        case .info:
            Palette(
                background: Color(red: 0.878, green: 0.949, blue: 0.996),
                foreground: Color(red: 0.012, green: 0.412, blue: 0.631),
                symbol: "info.circle"
            )
        case .warning:
            Palette(
                background: Color(red: 0.996, green: 0.953, blue: 0.780),
                foreground: Color(red: 0.706, green: 0.325, blue: 0.035),
                symbol: "exclamationmark.triangle"
            )
        }
    }
}

#if DEBUG
    struct SnackbarPill_Previews: PreviewProvider {
        static var previews: some View {
            VStack(spacing: 12) {
                SnackbarPill(message: SnackbarMessage(text: "Order could not be cancelled", severity: .error)) {}
                SnackbarPill(message: SnackbarMessage(text: "Saved", severity: .success)) {}
                SnackbarPill(message: SnackbarMessage(text: "Heads up", severity: .info)) {}
                SnackbarPill(message: SnackbarMessage(text: "Careful now", severity: .warning)) {}
            }
            .padding()
            .previewLayout(.sizeThatFits)
        }
    }
#endif
