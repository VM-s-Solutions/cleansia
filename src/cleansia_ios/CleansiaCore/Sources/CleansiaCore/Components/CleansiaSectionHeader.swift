import SwiftUI

public struct CleansiaSectionHeader: View {
    private let title: String
    private let badge: String?
    private let subtitle: String?
    private let centered: Bool

    public init(
        title: String,
        badge: String? = nil,
        subtitle: String? = nil,
        centered: Bool = false
    ) {
        self.title = title
        self.badge = badge
        self.subtitle = subtitle
        self.centered = centered
    }

    public var body: some View {
        VStack(alignment: centered ? .center : .leading, spacing: Spacing.xxs) {
            if let badge {
                Text(badge.uppercased())
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.primary)
                    .padding(.horizontal, Spacing.s)
                    .padding(.vertical, Spacing.xxs)
                    .background(CleansiaColors.primaryContainer)
                    .clipShape(Capsule())
                    .padding(.bottom, Spacing.xxs)
            }
            Text(title)
                .font(CleansiaTypography.headlineMedium)
                .foregroundColor(CleansiaColors.onBackground)
            if let subtitle {
                Text(subtitle)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
        }
        .frame(maxWidth: .infinity, alignment: centered ? .center : .leading)
    }
}

#if DEBUG
struct CleansiaSectionHeader_Previews: PreviewProvider {
    static var previews: some View {
        CleansiaSectionHeader(
            title: "Your orders",
            badge: "Active",
            subtitle: "Jobs in progress right now"
        )
        .padding()
    }
}
#endif
