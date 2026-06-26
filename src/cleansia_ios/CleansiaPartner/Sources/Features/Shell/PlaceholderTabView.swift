import CleansiaCore
import SwiftUI

struct PlaceholderDestination: View {
    let systemImage: String
    let text: String

    var body: some View {
        VStack(spacing: Spacing.xs) {
            Image(systemName: systemImage)
                .font(.system(size: 48))
                .foregroundColor(CleansiaColors.primary)
            Text(verbatim: text)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onBackground)
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(CleansiaColors.background.ignoresSafeArea())
    }
}

#if DEBUG
    struct PlaceholderDestination_Previews: PreviewProvider {
        static var previews: some View {
            PlaceholderDestination(systemImage: "list.clipboard", text: "Orders — coming in T-0307")
                .background(CleansiaColors.background)
        }
    }
#endif
