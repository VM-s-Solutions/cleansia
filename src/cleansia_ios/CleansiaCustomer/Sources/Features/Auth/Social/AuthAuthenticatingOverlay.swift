import CleansiaCore
import SwiftUI

/// A full-cover blocking overlay shown while a social sign-in awaits the backend
/// token exchange — it disables the form (the scrim intercepts every touch) and
/// gives the otherwise-silent pause a spinner + label.
struct AuthAuthenticatingOverlay: View {
    var body: some View {
        ZStack {
            Color.black.opacity(0.25)
                .ignoresSafeArea()
            VStack(spacing: Spacing.m) {
                ProgressView()
                    .progressViewStyle(.circular)
                    .tint(CleansiaColors.primary)
                    .scaleEffect(1.2)
                Text(L10n.Auth.signingIn)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurface)
            }
            .padding(Spacing.l)
            .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.large))
            .shadow(color: .black.opacity(0.2), radius: 20, y: 8)
        }
        .contentShape(Rectangle())
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(L10n.Auth.signingIn)
    }
}

#if DEBUG
    struct AuthAuthenticatingOverlay_Previews: PreviewProvider {
        static var previews: some View {
            ZStack {
                CleansiaColors.background
                AuthAuthenticatingOverlay()
            }
        }
    }
#endif
