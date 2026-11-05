import Foundation
import SwiftUI

/// Wraps a server-rendered HTML fragment (a legal text) in a document styled in the app's ink. The
/// hexes are `onSurface` / `primary` / `primaryText`'s light and dark pairs written out, because a
/// `WKWebView` stylesheet cannot read a SwiftUI `Color`; the media query lets the page follow the scheme
/// the view hands the web view. The quote rule takes the primary, a link the text ink (sky-700 in light
/// mode, where the primary's sky-600 is 4.10:1 on white); dark mode is the primary's sky-400 for both.
enum HtmlDocument {
    static let inkLightHex = "#0F172A"
    static let inkDarkHex = "#E2E8F0"
    static let accentLightHex = "#0284C7"
    static let accentDarkHex = "#38BDF8"
    static let linkLightHex = "#0369A1"

    static func wrap(_ fragment: String, fontFaces: String = bundledFontFaces) -> String {
        """
        <!doctype html>
        <html><head><meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <style>
          \(fontFaces)
          :root { color-scheme: light dark; }
          body { margin: 0; padding: 0 0 16px; color: \(inkLightHex); background: transparent;
                 font-family: Nunito, -apple-system, sans-serif; font-size: 15px; line-height: 1.55; }
          h1, h2, h3 { font-size: 16px; font-weight: 600; margin: 20px 0 6px; }
          p, li { margin: 0 0 10px; }
          blockquote { margin: 0 0 12px; padding: 8px 12px; border-left: 3px solid \(accentLightHex);
                       opacity: 0.85; }
          a { color: \(linkLightHex); }
          @media (prefers-color-scheme: dark) {
            body { color: \(inkDarkHex); }
            blockquote { border-left-color: \(accentDarkHex); }
            a { color: \(accentDarkHex); }
          }
        </style></head>
        <body>\(fragment)</body></html>
        """
    }

    /// A web view cannot see the fonts the app registers, so the body face rides inside the document: one face
    /// per weight the app bundles, as a data URL. With none bundled the text falls back to the system face.
    static let bundledFontFaces = fontFaces(in: .main)

    static func fontFaces(in bundle: Bundle) -> String {
        let weights: [(weight: Int, face: CleansiaFont.BundledFace)] = [
            (400, .nunitoRegular), (600, .nunitoSemiBold), (700, .nunitoBold)
        ]
        return weights.compactMap { weight, face -> String? in
            guard let url = bundle.url(forResource: face.rawValue, withExtension: "ttf"),
                  let data = try? Data(contentsOf: url) else { return nil }
            return "@font-face { font-family: Nunito; font-weight: \(weight); "
                + "src: url(data:font/ttf;base64,\(data.base64EncodedString())) format('truetype'); }"
        }
        .joined(separator: "\n")
    }

    /// Only the document itself may load: `loadHTMLString` with no base URL navigates to `about:blank`,
    /// and every link inside the fragment is refused so the sheet never becomes a browser.
    static func allowsNavigation(to url: URL?) -> Bool {
        url?.scheme == "about"
    }
}

#if canImport(UIKit)
    import WebKit

    /// Renders a server HTML fragment in-app: JavaScript off, no file access, every navigation refused.
    public struct HtmlContentView: UIViewRepresentable {
        private let html: String
        private let onContentHeight: ((CGFloat) -> Void)?

        /// The text scrolls itself. Given `onContentHeight` it does not: it reports its document's height, so the
        /// caller sizes it to that and scrolls it with the rest of its page.
        public init(html: String, onContentHeight: ((CGFloat) -> Void)? = nil) {
            self.html = html
            self.onContentHeight = onContentHeight
        }

        public func makeUIView(context: Context) -> WKWebView {
            Self.makeWebView(coordinator: context.coordinator)
        }

        static func makeWebView(coordinator: Coordinator) -> WKWebView {
            let configuration = WKWebViewConfiguration()
            configuration.defaultWebpagePreferences.allowsContentJavaScript = false
            let webView = WKWebView(frame: .zero, configuration: configuration)
            webView.isOpaque = false
            webView.backgroundColor = .clear
            webView.scrollView.backgroundColor = .clear
            webView.scrollView.showsHorizontalScrollIndicator = false
            webView.navigationDelegate = coordinator
            coordinator.fitToDocument(webView)
            return webView
        }

        public func updateUIView(_ webView: WKWebView, context: Context) {
            webView.overrideUserInterfaceStyle = context.environment.colorScheme == .dark ? .dark : .light
            guard context.coordinator.loadedFragment != html else { return }
            context.coordinator.loadedFragment = html
            webView.loadHTMLString(HtmlDocument.wrap(html), baseURL: nil)
        }

        public func makeCoordinator() -> Coordinator {
            Coordinator(onContentHeight: onContentHeight)
        }

        public final class Coordinator: NSObject, WKNavigationDelegate {
            var loadedFragment: String?
            private let onContentHeight: ((CGFloat) -> Void)?
            private var contentSize: NSKeyValueObservation?
            private var reportedHeight: CGFloat?

            init(onContentHeight: ((CGFloat) -> Void)?) {
                self.onContentHeight = onContentHeight
            }

            /// The report is deferred a turn: the height changes during a layout pass, which may not write the
            /// caller's state.
            func fitToDocument(_ webView: WKWebView) {
                guard onContentHeight != nil else { return }
                let scrollView = webView.scrollView
                scrollView.isScrollEnabled = false
                contentSize = scrollView.observe(\.contentSize, options: [.initial, .new]) { [weak self] view, _ in
                    let height = view.contentSize.height.rounded(.up)
                    DispatchQueue.main.async { self?.report(height) }
                }
            }

            private func report(_ height: CGFloat) {
                guard height > 0, height != reportedHeight else { return }
                reportedHeight = height
                onContentHeight?(height)
            }

            public func webView(
                _: WKWebView,
                decidePolicyFor navigationAction: WKNavigationAction,
                decisionHandler: @escaping (WKNavigationActionPolicy) -> Void
            ) {
                decisionHandler(HtmlDocument.allowsNavigation(to: navigationAction.request.url) ? .allow : .cancel)
            }
        }
    }
#endif
