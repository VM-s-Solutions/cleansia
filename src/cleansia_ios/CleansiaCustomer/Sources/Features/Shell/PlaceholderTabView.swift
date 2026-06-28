import CleansiaCore
import SwiftUI

struct PlaceholderTabView: View {
    let systemImage: String
    let title: String

    var body: some View {
        VStack(spacing: Spacing.xs) {
            Image(systemName: systemImage)
                .font(.system(size: 48))
                .foregroundColor(CleansiaColors.primary)
            Text(verbatim: title)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onBackground)
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(CleansiaColors.background.ignoresSafeArea())
    }
}

#if DEBUG
    struct PlaceholderTabView_Previews: PreviewProvider {
        static var previews: some View {
            PlaceholderTabView(systemImage: "house", title: "Home")
                .background(CleansiaColors.background)
        }
    }
#endif
