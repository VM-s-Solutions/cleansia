import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct OrderPhotosSection: View {
    let response: GetOrderPhotosResponse
    let onViewPhotos: () -> Void

    private var previewThumbs: [GetOrderPhotosOrderPhotoDto] {
        // A few previews that fit the card width without a horizontal scroll — the whole card is a
        // button into the full gallery, so the row is just a teaser, not a scroller.
        Array((response.photos ?? []).prefix(4))
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
                // Plain, non-scrolling row (clipped) — a horizontal ScrollView here captured the
                // pan gesture and broke the edge-swipe-back on completed orders.
                HStack(spacing: Spacing.xs) {
                    ForEach(Array(previewThumbs.enumerated()), id: \.offset) { _, photo in
                        PhotoThumb(urlString: photo.blobUrl, size: 72)
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                .clipped()
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
