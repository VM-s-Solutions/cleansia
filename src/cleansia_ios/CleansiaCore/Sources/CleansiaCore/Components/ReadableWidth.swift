import SwiftUI

enum ReadableWidth {
    /// Every iPhone today is 440pt wide at most and the iPhone Duo's outer display about 466pt, so nothing there is
    /// held to it.
    static let cap: CGFloat = 640
}

public extension View {
    /// Holds the content to `ReadableWidth.cap`, centred in a container it still fills, so a background behind it
    /// stays full-bleed. It reads the width it is given, never the device.
    /// → /mobile-app/patterns#wide-canvases-iphone-duo
    func readableWidth() -> some View {
        frame(maxWidth: ReadableWidth.cap).frame(maxWidth: .infinity)
    }
}

#if DEBUG
    struct ReadableWidth_Previews: PreviewProvider {
        static var previews: some View {
            ForEach([CGSize(width: 393, height: 852), CGSize(width: 951, height: 669)], id: \.width) { size in
                VStack(spacing: Spacing.m) {
                    CleansiaPrimaryButton("Continue") {}
                    CleansiaOutlinedButton("Cancel") {}
                }
                .padding(Spacing.m)
                .readableWidth()
                .frame(maxHeight: .infinity)
                .background(CleansiaColors.background)
                .previewLayout(.fixed(width: size.width, height: size.height))
                .previewDisplayName("\(Int(size.width)) × \(Int(size.height))")
            }
        }
    }
#endif
