import XCTest
@testable import CleansiaCore

final class LanguageLabelsTests: XCTestCase {
    func testALanguageIsNamedInItself() {
        XCTAssertEqual(LanguageLabels.label("cs"), "Čeština")
        XCTAssertEqual(LanguageLabels.label("uk"), "Українська")
    }

    func testAnUnknownTagIsShownAsItself() {
        XCTAssertEqual(LanguageLabels.label("xx"), "xx")
    }

    func testTheLabelsCoverTheSupportedLanguages() {
        XCTAssertEqual(
            Set(LanguageLabels.languages.map(\.tag)),
            Set(UserDefaultsAppSettingsStore.supportedLanguageTags)
        )
    }
}
