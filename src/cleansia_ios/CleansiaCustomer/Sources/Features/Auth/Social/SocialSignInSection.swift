import CleansiaCore
import SwiftUI

struct SocialSignInSection: View {
    let isLoading: Bool
    /// Non-nil dims the pair and says why they will refuse. They stay tappable on purpose: the
    /// refusal is the explanation, and a dead control with no reason is a dead end.
    let lockNote: String?
    let onApple: () -> Void
    let onGoogle: () -> Void

    private var dimmed: Bool {
        isLoading || lockNote != nil
    }

    var body: some View {
        VStack(spacing: Spacing.s) {
            LabelledDivider(L10n.Auth.dividerOr)

            AppleIDButton(action: onApple)
                .frame(height: 56)
                .disabled(isLoading)
                .opacity(dimmed ? 0.5 : 1)

            GoogleSignInButton(action: onGoogle)
                .disabled(isLoading)
                .opacity(dimmed ? 0.5 : 1)

            if let lockNote {
                Text(lockNote)
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .multilineTextAlignment(.center)
                    .frame(maxWidth: .infinity)
            }
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
            Group {
                SocialSignInSection(isLoading: false, lockNote: nil, onApple: {}, onGoogle: {})
                    .previewDisplayName("Unlocked")
                SocialSignInSection(
                    isLoading: false,
                    lockNote: "Accept the Terms of Service and Privacy Policy to continue with Google or Apple.",
                    onApple: {},
                    onGoogle: {}
                )
                .previewDisplayName("Locked")
            }
            .padding()
        }
    }
#endif
