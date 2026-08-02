import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCustomer

/// The shell's tab labels are drawn by UIKit's `UITabBarItem`, not by SwiftUI, so no text modifier
/// reaches them: `.lineLimit`, `.truncationMode` and `.minimumScaleFactor` inside `.tabItem` are
/// silently discarded, and the bar has no truncation fallback of its own — a label that outgrows its
/// slot is drawn at full width over its neighbours. The only place the fit can be defended is the
/// strings, so this measures the real bar on the narrowest supported width and fails when a
/// translation stops fitting.
@MainActor
final class ShellTabBarLabelTests: XCTestCase {
    private static let languages = ["en", "cs", "sk", "uk", "ru"]

    /// iPhone SE (3rd gen) / iPhone 8 / 13 mini — no iOS-16-capable device is narrower.
    private static let narrowestSupportedWidth: CGFloat = 375

    private var restoreBundle: Bundle?

    override func setUp() {
        super.setUp()
        restoreBundle = L10n.bundle
    }

    override func tearDown() {
        L10n.bundle = restoreBundle ?? .main
        super.tearDown()
    }

    func testEveryTabLabelStaysOnOneLine() throws {
        for language in Self.languages {
            L10n.bundle = try localeBundle(language)
            for label in try renderedTabLabels() {
                let lineHeight = (label.font ?? .systemFont(ofSize: 10)).lineHeight
                XCTAssertLessThan(
                    inkHeight(of: label),
                    lineHeight * 1.5,
                    "\"\(label.text ?? "")\" wrapped in \(language)"
                )
            }
        }
    }

    func testEveryTabLabelFitsItsSlot() throws {
        for language in Self.languages {
            L10n.bundle = try localeBundle(language)
            for label in try renderedTabLabels() {
                let slot = try XCTUnwrap(label.superview?.bounds.width)
                let natural = label.sizeThatFits(
                    CGSize(width: CGFloat.greatestFiniteMagnitude, height: .greatestFiniteMagnitude)
                ).width
                XCTAssertLessThanOrEqual(
                    natural,
                    slot,
                    "\"\(label.text ?? "")\" needs \(natural)pt in \(language) but the slot is \(slot)pt. The tab "
                        + "bar will not truncate it — it draws over the neighbouring tabs. Shorten the string."
                )
            }
        }
    }

    // MARK: - Rendering

    private func renderedTabLabels() throws -> [UILabel] {
        let frame = CGRect(x: 0, y: 0, width: Self.narrowestSupportedWidth, height: 667)
        let host = UIHostingController(rootView: TabLabelHarness())
        let window = UIWindow(frame: frame)
        window.rootViewController = host
        window.makeKeyAndVisible()
        host.view.frame = frame
        host.view.layoutIfNeeded()

        let bar = try XCTUnwrap(Self.tabBar(under: host.view), "the harness rendered no UITabBar")
        // iOS 26 keeps a selected (semibold) and an unselected (medium) label per item; measure both,
        // because the semibold one is the wider of the two.
        let labels = Self.labels(under: bar).filter { !($0.text ?? "").isEmpty }
        XCTAssertEqual(
            Set(labels.compactMap(\.text)).count,
            CustomerShellTab.navigationTabs.count,
            "not every tab rendered a label"
        )
        return labels
    }

    /// The vertical extent of the drawn glyphs rather than the label's frame: a second line shows up
    /// here even where the label was given room for only one.
    private func inkHeight(of label: UILabel) -> CGFloat {
        let size = label.bounds.size
        guard size.width > 1, size.height > 1 else { return 0 }
        let format = UIGraphicsImageRendererFormat.default()
        format.scale = 1
        format.opaque = false
        let image = UIGraphicsImageRenderer(size: size, format: format).image { context in
            label.layer.render(in: context.cgContext)
        }
        guard let cgImage = image.cgImage else { return 0 }
        let width = cgImage.width
        let height = cgImage.height
        var pixels = [UInt8](repeating: 0, count: width * height * 4)
        guard let context = CGContext(
            data: &pixels,
            width: width,
            height: height,
            bitsPerComponent: 8,
            bytesPerRow: width * 4,
            space: CGColorSpaceCreateDeviceRGB(),
            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
        ) else { return 0 }
        context.draw(cgImage, in: CGRect(x: 0, y: 0, width: width, height: height))

        var first: Int?
        var last: Int?
        for row in 0 ..< height {
            for column in 0 ..< width where pixels[(row * width + column) * 4 + 3] > 40 {
                if first == nil { first = row }
                last = row
                break
            }
        }
        guard let first, let last else { return 0 }
        return CGFloat(last - first + 1)
    }

    private static func tabBar(under view: UIView) -> UITabBar? {
        if let bar = view as? UITabBar { return bar }
        return view.subviews.lazy.compactMap { tabBar(under: $0) }.first
    }

    private static func labels(under view: UIView) -> [UILabel] {
        let own = (view as? UILabel).map { [$0] } ?? []
        return own + view.subviews.flatMap { labels(under: $0) }
    }

    private func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
    }
}

/// All five slots, so the bar divides its width exactly as `CustomerShellView` makes it — the empty
/// `.book` placeholder the Book FAB docks onto included.
private struct TabLabelHarness: View {
    var body: some View {
        TabView {
            ForEach(Array(CustomerShellTab.allCases.enumerated()), id: \.offset) { _, tab in
                Color.clear
                    .tabItem { Label(tab.label, systemImage: tab.systemImage) }
                    .tag(tab)
            }
        }
    }
}
