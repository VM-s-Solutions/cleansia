import CleansiaCore
import SwiftUI

struct AppliesNotice: View {
    let text: String

    var body: some View {
        HStack(alignment: .top, spacing: Spacing.s) {
            Image(systemName: "info.circle")
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(text)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .fixedSize(horizontal: false, vertical: true)
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.surfaceVariant, in: RoundedRectangle(cornerRadius: CornerRadius.medium))
    }
}
