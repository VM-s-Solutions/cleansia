import SwiftUI

public struct OrderSectionHeaderRow: View {
    let title: String
    var systemImage: String?
    var subtitle: String?

    public init(title: String, systemImage: String? = nil, subtitle: String? = nil) {
        self.title = title
        self.systemImage = systemImage
        self.subtitle = subtitle
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            HStack(spacing: Spacing.xs) {
                if let systemImage {
                    Image(systemName: systemImage)
                        .font(.system(size: 18))
                        .foregroundColor(CleansiaColors.primary)
                }
                if let subtitle {
                    Text(title)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onBackground)
                        + Text(verbatim: Self.subtitleRun(subtitle))
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                } else {
                    Text(title)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onBackground)
                }
            }
            // The rule that separates a section from its content now that the cards carry no border —
            // the partner sheet's `OrderSectionCard` arrangement.
            Divider().overlay(CleansiaColors.outlineVariant)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    /// The count after the title, tied to it, so a wrapped line never starts with its dot.
    static func subtitleRun(_ subtitle: String) -> String {
        "\u{00A0}· \(subtitle)"
    }
}

public struct OrderInfoRow: View {
    let label: String
    let value: String

    public init(label: String, value: String) {
        self.label = label
        self.value = value
    }

    public var body: some View {
        HStack {
            Text(label)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Spacer()
            Text(value)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
        }
    }
}
