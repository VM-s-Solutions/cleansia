import CleansiaCore
import SwiftUI

struct AuthHeaderImage: View {
    var size: CGFloat = 140

    var body: some View {
        Image(systemName: "sparkles")
            .font(.system(size: size * 0.42))
            .foregroundColor(CleansiaColors.primary)
            .frame(width: size, height: size)
    }
}
