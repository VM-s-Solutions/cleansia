import SwiftUI

public struct LabelledDivider: View {
    private let label: String

    public init(_ label: String) {
        self.label = label
    }

    public var body: some View {
        HStack(spacing: Spacing.m) {
            line
            Text(label)
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            line
        }
        .padding(.vertical, Spacing.xs)
    }

    private var line: some View {
        Rectangle()
            .fill(CleansiaColors.outlineVariant)
            .frame(height: 1)
            .frame(maxWidth: .infinity)
    }
}

#if DEBUG
    struct Components_Previews: PreviewProvider {
        static var previews: some View {
            LabelledDivider("OR")
                .padding()
        }
    }
#endif
