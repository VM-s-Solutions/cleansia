import XCTest
@testable import CleansiaCore

final class HtmlContentViewTests: XCTestCase {
    func testTheFragmentIsEmbeddedAsTheBodyOfAFullDocument() {
        let document = HtmlDocument.wrap("<p>Contract for work</p>")

        XCTAssertTrue(document.hasPrefix("<!doctype html>"))
        XCTAssertTrue(document.contains("<body><p>Contract for work</p></body>"))
        XCTAssertTrue(document.contains("<meta charset=\"utf-8\">"))
    }

    func testTheDocumentCarriesBothSchemesInk() {
        let document = HtmlDocument.wrap("<p>x</p>")

        XCTAssertTrue(document.contains("color: \(HtmlDocument.inkLightHex)"))
        XCTAssertTrue(document.contains("prefers-color-scheme: dark"))
        XCTAssertTrue(document.contains("color: \(HtmlDocument.inkDarkHex)"))
        XCTAssertTrue(document.contains(HtmlDocument.accentLightHex))
        XCTAssertTrue(document.contains(HtmlDocument.accentDarkHex))
    }

    func testTheWrapperAddsNoScript() {
        XCTAssertFalse(HtmlDocument.wrap("<p>x</p>").lowercased().contains("<script"))
    }

    func testOnlyTheDocumentsOwnLoadMayNavigate() {
        XCTAssertTrue(HtmlDocument.allowsNavigation(to: URL(string: "about:blank")))
        XCTAssertFalse(HtmlDocument.allowsNavigation(to: URL(string: "https://cleansia.cz/terms")))
        XCTAssertFalse(HtmlDocument.allowsNavigation(to: URL(string: "mailto:info@cleansia.cz")))
        XCTAssertFalse(HtmlDocument.allowsNavigation(to: nil))
    }

    #if canImport(UIKit)
        @MainActor
        func testTheWebViewIsBuiltWithJavaScriptOffAndRefusesNavigationThroughItsCoordinator() {
            let coordinator = HtmlContentView(html: "<p>x</p>").makeCoordinator()

            let webView = HtmlContentView.makeWebView(navigationDelegate: coordinator)

            XCTAssertFalse(webView.configuration.defaultWebpagePreferences.allowsContentJavaScript)
            XCTAssertFalse(webView.isOpaque)
            XCTAssertTrue(webView.navigationDelegate === coordinator)
        }
    #endif
}
