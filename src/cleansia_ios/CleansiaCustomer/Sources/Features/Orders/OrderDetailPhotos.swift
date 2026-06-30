import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct OrderPhotosSection: View {
    let response: GetOrderPhotosResponse
    let onViewPhotos: () -> Void

    private var previewThumbs: [GetOrderPhotosOrderPhotoDto] {
        Array((response.photos ?? []).prefix(6))
    }

    var body: some View {
        Button(action: onViewPhotos) {
            OrderCardSurface {
                HStack {
                    Text(L10n.OrderPhotos.sectionTitle)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onBackground)
                    Spacer()
                    Text(L10n.OrderPhotos.viewButton)
                        .font(CleansiaTypography.labelLarge)
                        .foregroundColor(CleansiaColors.primary)
                    Image(systemName: "chevron.right")
                        .font(.system(size: 12, weight: .semibold))
                        .foregroundColor(CleansiaColors.primary)
                }
                HStack(spacing: Spacing.xs) {
                    PhotoCountPill(text: L10n.OrderPhotos.summaryBefore(response.beforePhotoCount ?? 0))
                    PhotoCountPill(text: L10n.OrderPhotos.summaryAfter(response.afterPhotoCount ?? 0))
                }
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: Spacing.xs) {
                        ForEach(Array(previewThumbs.enumerated()), id: \.offset) { _, photo in
                            PhotoThumb(urlString: photo.blobUrl, size: 72)
                        }
                    }
                }
            }
        }
        .buttonStyle(.plain)
    }
}

private struct PhotoCountPill: View {
    let text: String

    var body: some View {
        Text(text)
            .font(CleansiaTypography.labelSmall)
            .foregroundColor(CleansiaColors.onSurfaceVariant)
            .padding(.horizontal, Spacing.xs)
            .padding(.vertical, Spacing.xxs)
            .background(CleansiaColors.surfaceVariant, in: Capsule())
    }
}

struct PhotoThumb: View {
    let urlString: String?
    let size: CGFloat

    var body: some View {
        AsyncImage(url: urlString.flatMap(URL.init(string:))) { image in
            image.resizable().scaledToFill()
        } placeholder: {
            CleansiaColors.surfaceVariant
        }
        .frame(width: size, height: size)
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.small))
    }
}
