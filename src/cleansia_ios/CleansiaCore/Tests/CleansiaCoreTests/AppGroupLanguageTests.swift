import XCTest
@testable import CleansiaCore

/// The mechanics of what each Notification Service Extension does. Every event in every language, from
/// each extension's own catalog, is pinned in the two apps' `PushLocKeyCatalogTests`.
final class AppGroupLanguageTests: XCTestCase {
    private func alert(
        title: String = "live_activity.status.completed.title",
        body: String = "live_activity.status.completed.detail",
        args: [Any]? = nil
    ) -> [AnyHashable: Any] {
        var alert: [String: Any] = ["title-loc-key": title, "loc-key": body]
        alert["loc-args"] = args
        return ["aps": ["alert": alert, "mutable-content": 1]]
    }

    // MARK: - Rendering

    func testTheAlertIsRenderedInTheGivenLanguageNotThePhones() throws {
        let czech = try XCTUnwrap(
            AppGroupLanguage.localizedAlert(userInfo: alert(), languageTag: "cs", bundle: MascotAssets.bundle)
        )
        XCTAssertEqual(czech.title, "Úklid dokončen")
        XCTAssertEqual(czech.body, "Hotovo — děkujeme")

        let ukrainian = try XCTUnwrap(
            AppGroupLanguage.localizedAlert(userInfo: alert(), languageTag: "uk", bundle: MascotAssets.bundle)
        )
        XCTAssertNotEqual(ukrainian.title, czech.title)
    }

    /// Each miss leaves the alert to iOS, which already resolved it in the phone's language.
    func testEveryMissIsLeftToIos() {
        let misses: [String: (userInfo: [AnyHashable: Any], tag: String?)] = [
            "no language written yet": (alert(), nil),
            "a language the bundle does not ship": (alert(), "de"),
            "an unknown title key": (alert(title: "push.unknown.title"), "cs"),
            "an unknown body key": (alert(body: "push.unknown.body"), "cs"),
            "a non-positional specifier": (alert(body: "live_activity.order_number", args: ["42"]), "cs"),
            "no aps": ([:], "cs"),
            "a literal alert, as the promo sends": (["aps": ["alert": ["title": "Sale", "body": "Now"]]], "cs")
        ]
        for (why, miss) in misses {
            XCTAssertNil(
                AppGroupLanguage.localizedAlert(
                    userInfo: miss.userInfo,
                    languageTag: miss.tag,
                    bundle: MascotAssets.bundle
                ),
                why
            )
        }
    }

    // MARK: - Filling the loc-args

    func testPositionalSlotsAreFilledInTheirOwnOrder() {
        XCTAssertEqual(AppGroupLanguage.filled("Order %1$@ is ready", with: ["A-17"]), "Order A-17 is ready")
        XCTAssertEqual(AppGroupLanguage.filled("%2$@ for %1$@", with: ["A-17", "120 Kč"]), "120 Kč for A-17")
        XCTAssertEqual(AppGroupLanguage.filled("No slots at all", with: ["unused"]), "No slots at all")
        XCTAssertEqual(AppGroupLanguage.filled("100%% done", with: []), "100% done")
    }

    /// `String(format:)` would read past its arguments here; a miss is what keeps the raw alert.
    func testASlotNobodySentIsAMissNotAGuess() {
        XCTAssertNil(AppGroupLanguage.filled("Order %1$@", with: []))
        XCTAssertNil(AppGroupLanguage.filled("%1$@ and %2$@", with: ["one"]))
        XCTAssertNil(AppGroupLanguage.filled("Order %0$@", with: ["A-17"]))
    }

    func testAnySpecifierAPushCannotFillIsAMiss() {
        for format in ["Order %@", "Count %d", "Count %1$d", "Trailing %", "Odd %1$"] {
            XCTAssertNil(AppGroupLanguage.filled(format, with: ["A-17"]), format)
        }
    }

    // MARK: - The shared value

    func testTheAppsLanguageRoundTripsThroughItsGroup() {
        let group = "AppGroupLanguageTests.\(UUID().uuidString)"
        defer { UserDefaults().removePersistentDomain(forName: group) }
        XCTAssertNil(AppGroupLanguage.read(appGroup: group))

        AppGroupLanguage.write("sk", appGroup: group)

        XCTAssertEqual(AppGroupLanguage.read(appGroup: group), "sk")
    }
}
