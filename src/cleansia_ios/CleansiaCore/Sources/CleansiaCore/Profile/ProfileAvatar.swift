#if canImport(UIKit)
    import SwiftUI

    /// The one avatar disc on this platform — every hero and every edit screen draws the same surface at
    /// a different size. The disc is a fixed white cut-out in both themes, so the initials ink is the
    /// pinned light-mode pair and never an adaptive token (`AvatarDiscBindingTests` reads this file).
    ///
    /// The photo is cached under its blob NAME: the URL beside it is a per-request SAS that changes every
    /// time the profile is fetched, so a URL-keyed cache re-downloads the same face on every read.
    public struct ProfileAvatar: View {
        private let display: AvatarDisplay
        private let initials: String
        private let cache: RemoteImageCache
        private let diameter: CGFloat
        private let strokeWidth: CGFloat
        private let strokeColor: Color
        private let onLoadFailure: (ProfilePhoto) -> Void
        private let onLoadSuccess: () -> Void

        /// The hero's ring reads against the brand gradient; on a surface-coloured card it would vanish,
        /// so the card call site passes an outline token instead.
        public init(
            display: AvatarDisplay,
            initials: String,
            cache: RemoteImageCache,
            diameter: CGFloat = 72,
            strokeWidth: CGFloat = 3,
            strokeColor: Color = .white.opacity(0.35),
            onLoadFailure: @escaping (ProfilePhoto) -> Void = { _ in },
            onLoadSuccess: @escaping () -> Void = {}
        ) {
            self.display = display
            self.initials = initials
            self.cache = cache
            self.diameter = diameter
            self.strokeWidth = strokeWidth
            self.strokeColor = strokeColor
            self.onLoadFailure = onLoadFailure
            self.onLoadSuccess = onLoadSuccess
        }

        public var body: some View {
            ZStack {
                initialsDisc
                photo
            }
            .frame(width: diameter, height: diameter)
            .clipShape(Circle())
            .overlay(Circle().stroke(strokeColor, lineWidth: strokeWidth))
        }

        private var initialsDisc: some View {
            ZStack {
                Circle()
                    .fill(Color.white)
                Text(initials)
                    .cleansiaFont(CleansiaTypography.headlineSmall)
                    .foregroundColor(CleansiaColors.onFixedWhite)
            }
        }

        @ViewBuilder
        private var photo: some View {
            switch display {
            case .initials:
                EmptyView()
            case let .remote(stored):
                if let url = stored.blobURL {
                    CachedRemoteImage(
                        cacheKey: stored.fileName,
                        url: url,
                        cache: cache,
                        onLoadFailure: { onLoadFailure(stored) },
                        onLoadSuccess: onLoadSuccess,
                        placeholder: { Color.clear }
                    )
                }
            case let .picked(image):
                Image(uiImage: image)
                    .resizable()
                    .scaledToFill()
            }
        }
    }

    #if DEBUG
        struct ProfileAvatar_Previews: PreviewProvider {
            static var previews: some View {
                HStack(spacing: Spacing.m) {
                    ProfileAvatar(display: .initials, initials: "JD", cache: RemoteImageCache())
                    ProfileAvatar(display: .initials, initials: "AL", cache: RemoteImageCache(), diameter: 96)
                }
                .padding()
                .background(LinearGradient(colors: BrandGradient.blue.colors, startPoint: .top, endPoint: .bottom))
            }
        }
    #endif
#endif
