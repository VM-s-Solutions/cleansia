import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct OrderPhotosSection: View {
    let gallery: OrderPhotos
    let onViewPhotos: () -> Void
    var onPreviewPhoto: ((OrderPhotoPhase, String?) -> Void)?

    var body: some View {
        OrderCardSurface {
            HStack {
                Text(L10n.OrderPhotos.sectionTitle)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onBackground)
                Spacer()
                Button(action: onViewPhotos) {
                    HStack(spacing: Spacing.xs) {
                        Text(L10n.OrderPhotos.viewButton)
                            .font(CleansiaTypography.labelLarge)
                            .foregroundColor(CleansiaColors.primaryText)
                        Image(systemName: "chevron.right")
                            .font(.system(size: 12, weight: .semibold))
                            .foregroundColor(CleansiaColors.primaryText)
                    }
                    .frame(minHeight: 44)
                }
                .buttonStyle(.plain)
            }
            HStack(alignment: .top, spacing: Spacing.xs) {
                ForEach(gallery.phases) { phase in
                    let photos = gallery.photos(for: phase)
                    VStack(alignment: .leading, spacing: Spacing.xxs) {
                        PhotoCountPill(text: "\(phase.label) · \(photos.count)")
                        if photos.isEmpty {
                            Text(L10n.OrderPhotos.empty)
                                .font(CleansiaTypography.labelMedium)
                                .foregroundColor(CleansiaColors.onSurfaceVariant)
                        } else {
                            HStack(spacing: Spacing.xs) {
                                ForEach(Array(photos.prefix(1).enumerated()), id: \.offset) { _, photo in
                                    Button {
                                        if let onPreviewPhoto {
                                            onPreviewPhoto(phase, photo.id)
                                        } else {
                                            onViewPhotos()
                                        }
                                    } label: {
                                        PhotoThumb(urlString: photo.blobUrl, size: 64)
                                    }
                                    .buttonStyle(.plain)
                                    .accessibilityLabel(phase.label)
                                }
                            }
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .clipped()
                        }
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
            }
        }
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
