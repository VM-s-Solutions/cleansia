import CleansiaCore
import SwiftUI

/// Full-screen busy overlay with the cleaning mascot, mirroring Android's
/// `BusyMascotOverlay`. The scrim swallows touches so the user can't interact
/// with the underlying screen mid-flight; the card springs in from 85% scale.
struct BusyMascotOverlay: View {
    let visible: Bool
    let message: String

    var body: some View {
        ZStack {
            if visible {
                Color.black.opacity(0.45)
                    .ignoresSafeArea()
                    .transition(.opacity)
                VStack(spacing: Spacing.xs) {
                    AnimatedMascotView(.cleaningInProgress, fallback: .cleaning)
                        .frame(width: 140, height: 140)
                    Text(message)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                        .multilineTextAlignment(.center)
                }
                .padding(.horizontal, Spacing.l)
                .padding(.vertical, 28)
                .frame(maxWidth: 360)
                .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.large))
                .shadow(radius: 16)
                .padding(.horizontal, Spacing.xl)
                .transition(.scale(scale: 0.85).combined(with: .opacity))
            }
        }
        .animation(.spring(response: 0.4, dampingFraction: 0.7), value: visible)
    }
}

#if DEBUG
    struct BusyMascotOverlay_Previews: PreviewProvider {
        static var previews: some View {
            ZStack {
                CleansiaColors.background.ignoresSafeArea()
                BusyMascotOverlay(visible: true, message: "Activating Cleansia Plus…")
            }
        }
    }
#endif
