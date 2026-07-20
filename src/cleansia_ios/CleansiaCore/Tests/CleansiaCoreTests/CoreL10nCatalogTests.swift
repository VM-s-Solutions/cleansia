import XCTest
@testable import CleansiaCore

final class CoreL10nCatalogTests: XCTestCase {
    private static let regions = ["en", "cs", "sk", "uk", "ru"]
    private static let canonicalKeys = [
        "error.unauthorized",
        "error.not_found",
        "error.request",
        "error.server",
        "error.unreachable",
        "error.generic",
        "snackbar.dismiss"
    ]

    override func tearDown() {
        CoreL10n.bundle = .module
        super.tearDown()
    }

    func testEveryRegionShipsItsOwnLproj() {
        for region in Self.regions {
            let bundle = CoreL10n.localizedBundle(for: region)
            XCTAssertNotEqual(
                bundle.bundlePath, Bundle.module.bundlePath,
                "\(region).lproj missing from the Core catalog — resolution silently falls through to en"
            )
        }
    }

    func testEveryCoreKeyResolvesNonEmptyInAllFiveRegionsAndTranslatesOffEnglish() {
        let keys = catalogKeys()
        XCTAssertGreaterThanOrEqual(keys.count, Self.canonicalKeys.count)
        for canonical in Self.canonicalKeys {
            XCTAssertTrue(keys.contains(canonical), "canonical key \(canonical) missing from the catalog")
        }

        let enBaseline = resolveAll(keys, region: "en")
        for region in Self.regions {
            let resolved = resolveAll(keys, region: region)
            for key in keys {
                let value = resolved[key]
                XCTAssertNotNil(value, "\(key) did not resolve in \(region)")
                XCTAssertFalse(value?.isEmpty ?? true, "\(key) resolved empty in \(region)")
                if region != "en" {
                    XCTAssertNotEqual(
                        value, enBaseline[key],
                        "\(key) in \(region) equals the en baseline — translation fell through to English"
                    )
                }
            }
        }
    }

    func testApplyLanguageTagSwitchesCoreStringsWithoutRestart() {
        let localizer = ApiErrorLocalizer()
        let english = localizer.message(forStatus: 500)

        CoreL10n.apply(languageTag: "cs")
        let czech = localizer.message(forStatus: 500)

        XCTAssertFalse(czech.isEmpty)
        XCTAssertNotEqual(czech, english)
        let expected = CoreL10n.localizedBundle(for: "cs")
            .localizedString(forKey: "error.server", value: nil, table: nil)
        XCTAssertEqual(czech, expected)

        CoreL10n.apply(languageTag: "en")
        XCTAssertEqual(localizer.message(forStatus: 500), english)
    }

    func testApplyUnknownTagFallsBackToTheModuleDefault() {
        CoreL10n.apply(languageTag: "de")
        XCTAssertEqual(CoreL10n.bundle.bundlePath, Bundle.module.bundlePath)
    }

    private func catalogKeys() -> [String] {
        let sentinel = "\u{1}"
        guard let path = CoreL10n.localizedBundle(for: "en").path(forResource: "Localizable", ofType: "strings"),
              let dictionary = NSDictionary(contentsOfFile: path) as? [String: String]
        else {
            XCTFail("could not enumerate the compiled en Localizable.strings")
            return []
        }
        return dictionary.keys.filter { $0 != sentinel }.sorted()
    }

    private func resolveAll(_ keys: [String], region: String) -> [String: String] {
        let sentinel = "\u{1}"
        let bundle = CoreL10n.localizedBundle(for: region)
        var resolved: [String: String] = [:]
        for key in keys {
            let value = bundle.localizedString(forKey: key, value: sentinel, table: nil)
            if value != sentinel {
                resolved[key] = value
            }
        }
        return resolved
    }
}
