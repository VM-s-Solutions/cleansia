import CleansiaCore
import SwiftUI

struct SocialSignInSection: View {
    let isLoading: Bool
    let onApple: () -> Void
    let onGoogle: () -> Void

    var body: some View {
        VStack(spacing: Spacing.s) {
            LabelledDivider(L10n.Auth.dividerOr)

            AppleIDButton(action: onApple)
                .frame(height: 56)
                .disabled(isLoading)

            GoogleSignInButton(action: onGoogle)
                .disabled(isLoading)
                .opacity(isLoading ? 0.5 : 1)
        }
    }
}

private struct GoogleSignInButton: View {
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            HStack(spacing: Spacing.s) {
                Image("google_g")
                    .resizable()
                    .renderingMode(.original)
                    .scaledToFit()
                    .frame(width: 20, height: 20)
                Text(L10n.Auth.continueWithGoogle)
                    .font(CleansiaTypography.titleMedium)
            }
            .frame(maxWidth: .infinity, minHeight: 56)
            .padding(.horizontal, Spacing.l)
            .foregroundColor(CleansiaColors.onSurface)
            .overlay(Capsule().stroke(CleansiaColors.outline, lineWidth: 1))
        }
    }
}

#if DEBUG
    struct SocialSignInSection_Previews: PreviewProvider {
        static var previews: some View {
            SocialSignInSection(isLoading: false, onApple: {}, onGoogle: {})
                .padding()
        }
    }
#endif
