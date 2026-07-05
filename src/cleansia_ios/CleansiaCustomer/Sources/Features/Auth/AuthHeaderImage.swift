import CleansiaCore
import SwiftUI

struct AuthHeaderImage: View {
    var size: CGFloat = 140

    var body: some View {
        Mascot.waving.image
            .resizable()
            .scaledToFit()
            .frame(width: size, height: size)
    }
}
