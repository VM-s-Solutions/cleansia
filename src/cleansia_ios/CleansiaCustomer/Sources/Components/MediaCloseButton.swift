import SwiftUI

/// The close control floating over a full-screen photo: on iOS 26 a clear Liquid Glass circle, the
/// variant for controls over media; below it a white glyph on a translucent black circle, so it reads
/// over any image. The order-photo pager and the dispute-evidence viewer share it.
struct MediaCloseButton: View {
    let action: () -> Void

    var body: some View {
        if #available(iOS 26, *) {
            Button(action: action) {
                Image(systemName: "xmark")
                    .font(.system(size: 16, weight: .bold))
                    .foregroundColor(.white)
                    .frame(width: 30, height: 30)
            }
            .buttonStyle(.glass(.clear))
            .buttonBorderShape(.circle)
        } else {
            Button(action: action) {
                Image(systemName: "xmark")
                    .font(.system(size: 16, weight: .bold))
                    .foregroundColor(.white)
                    .frame(width: 44, height: 44)
                    .background(Color.black.opacity(0.4), in: Circle())
            }
        }
    }
}
