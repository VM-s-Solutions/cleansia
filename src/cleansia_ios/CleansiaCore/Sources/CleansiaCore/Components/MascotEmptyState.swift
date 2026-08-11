import SwiftUI

public struct MascotEmptyState<Actions: View>: View {
    private let image: Image
    private let text: String
    private let subtitle: String?
    private let topSpacer: CGFloat
    private let verticallyCentered: Bool
    private let imageSize: CGFloat
    private let titleStyle: CleansiaTextStyle
    private let actions: Actions

    public init(
        image: Image,
        text: String,
        subtitle: String? = nil,
        topSpacer: CGFloat = 220,
        verticallyCentered: Bool = false,
        imageSize: CGFloat = 180,
        titleStyle: CleansiaTextStyle = CleansiaTypography.scale.titleMedium,
        @ViewBuilder actions: () -> Actions
    ) {
        self.image = image
        self.text = text
        self.subtitle = subtitle
        self.topSpacer = topSpacer
        self.verticallyCentered = verticallyCentered
        self.imageSize = imageSize
        self.titleStyle = titleStyle
        self.actions = actions()
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
                .frame(width: imageSize, height: imageSize)
            Text(text)
                .cleansiaFont(titleStyle)
                .foregroundColor(CleansiaColors.onSurface)
                .multilineTextAlignment(.center)
                .padding(.top, Spacing.m)
            if let subtitle {
                Text(subtitle)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .multilineTextAlignment(.center)
                    .padding(.top, Spacing.xs)
            }
            actions
                .padding(.top, Spacing.l)
            if verticallyCentered {
                Spacer()
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(.horizontal, Spacing.xl)
    }
}

public extension MascotEmptyState where Actions == EmptyView {
    init(
        image: Image,
        text: String,
        subtitle: String? = nil,
        topSpacer: CGFloat = 220,
        verticallyCentered: Bool = false,
        imageSize: CGFloat = 180,
        titleStyle: CleansiaTextStyle = CleansiaTypography.scale.titleMedium
    ) {
        self.init(
            image: image,
            text: text,
            subtitle: subtitle,
            topSpacer: topSpacer,
            verticallyCentered: verticallyCentered,
            imageSize: imageSize,
            titleStyle: titleStyle,
            actions: { EmptyView() }
        )
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
