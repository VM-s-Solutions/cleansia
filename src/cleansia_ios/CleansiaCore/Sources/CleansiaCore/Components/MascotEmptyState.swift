import SwiftUI

public struct MascotEmptyState: View {
    private let image: Image
    private let text: String
    private let topSpacer: CGFloat
    private let verticallyCentered: Bool

    public init(
        image: Image,
        text: String,
        topSpacer: CGFloat = 220,
        verticallyCentered: Bool = false
    ) {
        self.image = image
        self.text = text
        self.topSpacer = topSpacer
        self.verticallyCentered = verticallyCentered
    }

    public var body: some View {
        VStack(spacing: 0) {
            if verticallyCentered {
                Spacer()
            } else {
                Spacer().frame(height: topSpacer)
            }
            image
                .resizable()
                .scaledToFit()
                .frame(width: 180, height: 180)
            Text(text)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .multilineTextAlignment(.center)
                .padding(.top, Spacing.m)
            if verticallyCentered {
                Spacer()
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(.horizontal, Spacing.xl)
    }
}

#if DEBUG
    struct MascotEmptyState_Previews: PreviewProvider {
        static var previews: some View {
            MascotEmptyState(
                image: Image(systemName: "tray"),
                text: "No orders yet",
                verticallyCentered: true
            )
        }
    }
#endif
