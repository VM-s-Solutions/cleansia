import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// The level the customer picks travels as the server's `DirtinessLevel`, is described in five
/// locales, states the rates `BookingPolicy` charges, and is drawn where it is chosen and shown.
final class DirtinessLevelTests: XCTestCase {
    func testEachLevelTravelsAsTheServersValueAndComesBack() {
        XCTAssertEqual(Dirtiness.normal.wire, ._0)
        XCTAssertEqual(Dirtiness.increased.wire, ._1)
        XCTAssertEqual(Dirtiness.heavy.wire, ._2)
        for level in Dirtiness.allCases {
            XCTAssertEqual(Dirtiness(wire: level.wire), level)
        }
    }

    func testTheLevelsAreOfferedMildestFirst() {
        XCTAssertEqual(Dirtiness.allCases, [.normal, .increased, .heavy])
    }

    // MARK: - Copy

    func testEveryLevelIsDescribedInEveryLocale() throws {
        let restore = L10n.bundle
        defer { L10n.bundle = restore }

        for language in ["en", "cs", "sk", "uk", "ru"] {
            L10n.bundle = try localeBundle(language)
            var copy = [L10n.Booking.dirtinessQuestion, L10n.Booking.dirtinessHint, L10n.Booking.dirtinessLevelLabel]
            for level in Dirtiness.allCases {
                copy.append(L10n.Booking.dirtinessName(level))
                copy.append(L10n.Booking.dirtinessRate(level))
                copy.append(L10n.Booking.dirtinessLead(level))
                let signs = L10n.Booking.dirtinessSigns(level)
                XCTAssertEqual(Set(signs).count, 4, "\(level) has repeated or missing signs in \(language)")
                copy += signs
                copy += L10n.Booking.dirtinessSurcharge(level).map { [$0] } ?? []
            }
            for text in copy {
                XCTAssertFalse(text.isBlank, "empty dirtiness copy in \(language)")
                XCTAssertFalse(text.hasPrefix("booking_dirtiness_"), "\(text) is unlocalized in \(language)")
            }
        }
    }

    func testOnlyASurchargedLevelNamesASurchargeLine() {
        XCTAssertNil(L10n.Booking.dirtinessSurcharge(.normal))
        XCTAssertNotNil(L10n.Booking.dirtinessSurcharge(.increased))
        XCTAssertNotNil(L10n.Booking.dirtinessSurcharge(.heavy))
    }

    /// The percentages on the choice and on the surcharge line are copy, so they are held to the rates
    /// the server charges in every locale.
    func testTheStatedRatesAreTheServers() throws {
        let increased = try Self.policyPercent("IncreasedDirtinessSurchargeRate")
        let heavy = try Self.policyPercent("HeavyDirtinessSurchargeRate")
        let restore = L10n.bundle
        defer { L10n.bundle = restore }

        for language in ["en", "cs", "sk", "uk", "ru"] {
            L10n.bundle = try localeBundle(language)
            for (level, percent) in [(Dirtiness.increased, increased), (Dirtiness.heavy, heavy)] {
                XCTAssertEqual(
                    Self.integers(in: L10n.Booking.dirtinessRate(level)),
                    [percent],
                    "\(level) in \(language)"
                )
                XCTAssertEqual(
                    Self.integers(in: L10n.Booking.dirtinessSurcharge(level) ?? ""),
                    [percent],
                    "\(level) surcharge line in \(language)"
                )
            }
        }
    }

    // MARK: - Where the level is drawn

    func testTheWizardAsksForTheLevelOnItsOwnStep() throws {
        XCTAssertTrue(
            try read("Features/Booking/BookingSheetView.swift")
                .contains("case 2: DirtinessStep(viewModel: viewModel)"),
            "the level step lost its slot"
        )
        XCTAssertTrue(
            try read("Features/Booking/Steps/DirtinessStep.swift")
                .contains("DirtinessPicker(selected: viewModel.state.dirtiness, onSelect: viewModel.setDirtiness)"),
            "the step does not bind the booking's level"
        )
    }

    func testTheRecurringFormBindsTheSchedulesLevel() throws {
        XCTAssertTrue(
            try read("Features/Recurring/CreateRecurringScreen.swift")
                .contains("DirtinessPicker(selected: vm.formState.dirtiness, onSelect: vm.setDirtiness)"),
            "the schedule's level cannot be seen or changed"
        )
    }

    func testTheSurchargeRowIsDrawnWhereTheMoneyIsShown() throws {
        XCTAssertTrue(
            try read("Features/Booking/Confirm/ConfirmStepComponents.swift")
                .contains("L10n.Booking.dirtinessSurcharge(summary.dirtiness)"),
            "the booking summary drops the surcharge row"
        )
        XCTAssertTrue(
            try read("Features/Orders/OrderDetailSummary.swift")
                .contains("L10n.Booking.dirtinessSurcharge(breakdown.dirtiness)"),
            "the order's price breakdown drops the surcharge row"
        )
        XCTAssertTrue(
            try read("Features/Orders/OrderDetailDetailsCards.swift")
                .contains("value: L10n.Booking.dirtinessName(order.dirtiness)"),
            "the order detail does not name its level"
        )
        XCTAssertTrue(
            try read("Features/Booking/Confirm/ConfirmStepComponents.swift")
                .contains("value: state.dirtiness.map(L10n.Booking.dirtinessName) ?? \"—\""),
            "the booking summary does not restate the level"
        )
    }

    // MARK: - Helpers

    private static func integers(in text: String) -> [Int] {
        text.split { !$0.isASCII || !$0.isNumber }.compactMap { Int($0) }
    }

    private static func policyPercent(_ name: String) throws -> Int {
        let policy = iosRoot()
            .deletingLastPathComponent()
            .appendingPathComponent("Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs")
        let source = try String(contentsOf: policy, encoding: .utf8)
        let pattern = #"public\s+const\s+decimal\s+"# + name + #"\s*=\s*([0-9.]+)m\s*;"#
        let regex = try NSRegularExpression(pattern: pattern)
        let match = try XCTUnwrap(
            regex.firstMatch(in: source, range: NSRange(source.startIndex..., in: source)),
            "BookingPolicy.\(name) not found — the parser needs updating"
        )
        let digits = try XCTUnwrap(Range(match.range(at: 1), in: source))
        let rate = try XCTUnwrap(Double(source[digits]))
        return Int((rate * 100).rounded())
    }

    private func read(_ sourcePath: String) throws -> String {
        let url = Self.iosRoot()
            .appendingPathComponent("CleansiaCustomer/Sources")
            .appendingPathComponent(sourcePath)
        return try String(contentsOf: url, encoding: .utf8)
    }

    private static func iosRoot() -> URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
    }

    private func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
    }
}
