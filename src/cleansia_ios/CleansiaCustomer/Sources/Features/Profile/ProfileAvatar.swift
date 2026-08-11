import CleansiaCore
import SwiftUI

/// The one avatar disc in the customer app — the profile hero and the edit screen draw the same
/// surface at different sizes. The disc is a fixed white cut-out in both themes, so the initials ink is
/// the pinned light-mode pair and never an adaptive token (`AvatarDiscBindingTests` reads this file).
///
/// The photo is cached under its blob NAME: the URL beside it is a per-request SAS that changes every
/// time the profile is fetched, so a URL-keyed cache re-downloads the same face on every read.
struct ProfileAvatar: View {
    let display: AvatarDisplay
    let initials: String
    let cache: RemoteImageCache
    var diameter: CGFloat = 72
    var strokeWidth: CGFloat = 3
    /// The hero's ring reads against the brand gradient; on a surface-coloured card it would vanish,
    /// so the card call site passes an outline token instead.
    var strokeColor: Color = .white.opacity(0.35)
    var onLoadFailure: (ProfilePhoto) -> Void = { _ in }

    var body: some View {
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
                .font(CleansiaTypography.headlineSmall)
                .foregroundColor(CleansiaColors.onFixedWhite)
        }
    }

    @ViewBuilder
    private var photo: some View {
        switch display {
        case .initials:
            EmptyView()
        case let .remote(stored):
            CachedRemoteImage(
                cacheKey: stored.fileName,
                url: stored.blobURL,
                cache: cache,
                onLoadFailure: { onLoadFailure(stored) },
                placeholder: { Color.clear }
            )
        case let .picked(image):
            Image(uiImage: image)
                .resizable()
                .scaledToFill()
        }
    }
}

extension CurrentUserProfile {
    var initials: String {
        let first = firstName.first.map(String.init) ?? ""
        let last = lastName.first.map(String.init) ?? ""
        return (first + last).uppercased()
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
