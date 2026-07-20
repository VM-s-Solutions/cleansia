import SwiftUI

/// The branded launch splash shared by both apps (customer + partner). The "Cleansia" wordmark
/// reveals one letter at a time on the sky-600 → sky-400 brand gradient; once the name has landed the
/// optional "PARTNER" lockup and the subtitle fade in beneath it, then the resolver spinner. Pure
/// SwiftUI text animation — no image assets, no Lottie — so it costs nothing to ship and scales to any
/// device. The gradient matches the OS launch background (SplashBackground / sky-600), so there is no
/// colour jump between the launch screen and this view.
///
/// The subtitle is passed in because each app owns its own localized `L10n.Splash.tagline`; the shared
/// view stays free of app-specific strings.
public struct WordmarkSplashView: View {
    private let subtitle: String
    private let showsPartnerLabel: Bool

    @State private var revealed = false

    private static let brand = Array("Cleansia")
    private static let perLetter = 0.06
    /// When the last letter has finished animating in — the anchor for the subtitle / label / spinner.
    private var afterWordmark: Double {
        Double(Self.brand.count) * Self.perLetter
    }

    public init(subtitle: String, showsPartnerLabel: Bool = false) {
        self.subtitle = subtitle
        self.showsPartnerLabel = showsPartnerLabel
    }

    public var body: some View {
        VStack(spacing: 0) {
            wordmark

            if showsPartnerLabel {
                Text(verbatim: "PARTNER")
                    .font(CleansiaFont.poppins(.semibold, size: 15))
                    .tracking(6)
                    .foregroundColor(.white)
                    .opacity(revealed ? 1 : 0)
                    .padding(.top, Spacing.xs)
                    .animation(.easeOut(duration: 0.4).delay(afterWordmark + 0.05), value: revealed)
            }

            Text(verbatim: subtitle)
                .font(CleansiaTypography.bodyLarge)
                .foregroundColor(.white.opacity(0.9))
                .multilineTextAlignment(.center)
                .opacity(revealed ? 1 : 0)
                .padding(.top, showsPartnerLabel ? Spacing.s : Spacing.m)
                .padding(.horizontal, Spacing.xl)
                .animation(.easeOut(duration: 0.5).delay(afterWordmark + 0.18), value: revealed)

            ProgressView()
                .progressViewStyle(.circular)
                .tint(.white)
                .opacity(revealed ? 1 : 0)
                .padding(.top, Spacing.xl)
                .animation(.easeOut(duration: 0.4).delay(afterWordmark + 0.32), value: revealed)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(
            LinearGradient(
                colors: [CleansiaColors.splashGradientStart, CleansiaColors.splashGradientEnd],
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            )
            .ignoresSafeArea()
        )
        .onAppear { revealed = true }
    }

    /// "Cleansia" as individual letters so each can rise + fade in on its own delay. Individual `Text`
    /// glyphs keep their natural advance widths (Poppins has negligible kerning), so `spacing: 0`
    /// reads as the set word.
    private var wordmark: some View {
        HStack(spacing: 0) {
            ForEach(Array(Self.brand.enumerated()), id: \.offset) { index, character in
                Text(String(character))
                    .font(CleansiaFont.poppins(.bold, size: 44))
                    .foregroundColor(.white)
                    .opacity(revealed ? 1 : 0)
                    .offset(y: revealed ? 0 : 16)
                    .animation(
                        .spring(response: 0.42, dampingFraction: 0.72).delay(Double(index) * Self.perLetter),
                        value: revealed
                    )
            }
        }
        // Read the wordmark as one word, not eight separate letters, under VoiceOver.
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Cleansia")
    }
}

#if DEBUG
    struct WordmarkSplashView_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                WordmarkSplashView(subtitle: "Book a spotless home in minutes")
                    .previewDisplayName("Customer")
                WordmarkSplashView(subtitle: "Manage your jobs on the go", showsPartnerLabel: true)
                    .previewDisplayName("Partner")
            }
        }
    }
#endif
