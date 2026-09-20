import Foundation
import SwiftUI

/// Wraps a server-rendered HTML fragment (a legal text) in a document styled in the app's ink. The
/// hexes are `onSurface` / `primary`'s light and dark pairs written out, because a `WKWebView`
/// stylesheet cannot read a SwiftUI `Color`; the media query lets the page follow the scheme the view
/// hands the web view.
enum HtmlDocument {
    static let inkLightHex = "#0F172A"
    static let inkDarkHex = "#E2E8F0"
    static let accentLightHex = "#0284C7"
    static let accentDarkHex = "#38BDF8"

    static func wrap(_ fragment: String) -> String {
        """
        <!doctype html>
        <html><head><meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <style>
          :root { color-scheme: light dark; }
          body { margin: 0; padding: 0 4px 16px; color: \(inkLightHex); background: transparent;
                 font-family: -apple-system, sans-serif; font-size: 15px; line-height: 1.55; }
          h1, h2, h3 { font-size: 16px; font-weight: 600; margin: 20px 0 6px; }
          p, li { margin: 0 0 10px; }
          blockquote { margin: 0 0 12px; padding: 8px 12px; border-left: 3px solid \(accentLightHex);
                       opacity: 0.85; }
          a { color: \(accentLightHex); }
          @media (prefers-color-scheme: dark) {
            body { color: \(inkDarkHex); }
            blockquote { border-left-color: \(accentDarkHex); }
            a { color: \(accentDarkHex); }
          }
        </style></head>
        <body>\(fragment)</body></html>
        """
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
    /// Shared by the partner contract sheet and the customer contract screen.
    public struct HtmlContentView: UIViewRepresentable {
        private let html: String

        public init(html: String) {
            self.html = html
        }

        public func makeUIView(context: Context) -> WKWebView {
            Self.makeWebView(navigationDelegate: context.coordinator)
        }

        static func makeWebView(navigationDelegate: WKNavigationDelegate) -> WKWebView {
            let configuration = WKWebViewConfiguration()
            configuration.defaultWebpagePreferences.allowsContentJavaScript = false
            let webView = WKWebView(frame: .zero, configuration: configuration)
            webView.isOpaque = false
            webView.backgroundColor = .clear
            webView.scrollView.backgroundColor = .clear
            webView.scrollView.showsHorizontalScrollIndicator = false
            webView.navigationDelegate = navigationDelegate
            return webView
        }

        public func updateUIView(_ webView: WKWebView, context: Context) {
            webView.overrideUserInterfaceStyle = context.environment.colorScheme == .dark ? .dark : .light
            let document = HtmlDocument.wrap(html)
            guard context.coordinator.loadedDocument != document else { return }
            context.coordinator.loadedDocument = document
            webView.loadHTMLString(document, baseURL: nil)
        }

        public func makeCoordinator() -> Coordinator {
            Coordinator()
        }

        public final class Coordinator: NSObject, WKNavigationDelegate {
            var loadedDocument: String?

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
