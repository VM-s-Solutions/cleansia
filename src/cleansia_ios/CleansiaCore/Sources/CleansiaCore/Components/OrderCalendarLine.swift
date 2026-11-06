import SwiftUI

/// The day of a booking, and its window when it has one, beside a calendar glyph. It is never shrunk to fit: a
/// window that does not fit beside its day starts the next line whole, without the "·", and a day too long for
/// its line wraps.
public struct OrderCalendarLine: View {
    private let day: String
    private let window: String?

    public init(_ day: String, window: String? = nil) {
        self.day = day
        self.window = window
    }

    public var body: some View {
        Label {
            if let window {
                ViewThatFits(in: .horizontal) {
                    Text(verbatim: "\(day) · \(window)").fixedSize()
                    VStack(alignment: .leading, spacing: 0) {
                        Text(verbatim: day)
                        Text(verbatim: window)
                    }
                }
                .accessibilityElement(children: .combine)
            } else {
                Text(verbatim: day)
            }
        } icon: {
            Image(systemName: "calendar")
        }
        .font(CleansiaTypography.bodyMedium)
        .foregroundColor(CleansiaColors.onSurfaceVariant)
        .fixedSize(horizontal: false, vertical: true)
    }
}
