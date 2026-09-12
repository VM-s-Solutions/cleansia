import Foundation
import XCTest
@testable import CleansiaCustomer

/// `BookingViewModelTests` proves which code the wizard resolves and `CatalogWireContractTests` proves
/// where the catalogue's comes from; neither can see whether a screen still hands the formatter a
/// `"CZK"` literal. The booking steps have no view harness, so the call sites are pinned by source
/// assertions — the sanctioned fallback, scoped to the booking feature and its two price strings.
final class BookingCurrencyBindingTests: XCTestCase {
    private static let bookingDir = "CleansiaCustomer/Sources/Features/Booking"
    private static let catalogPath = "CleansiaCustomer/Resources/Localizable.xcstrings"
    private static let priceKeys = ["booking_price_from", "booking_price_per_room"]
    private static let locales = ["en", "cs", "sk", "uk", "ru"]

    private static let literalCurrency = #"(currencyCode:|\?\?)\s*"[A-Z]{3}""#
    private static let currencyLiteral = "CZK|Kč|EUR|€"

    func testNoWizardAmountIsLabelledWithACurrencyLiteral() throws {
        var offenders: [String] = []
        for file in try swiftFiles(under: Self.bookingDir) {
            let lines = try shippedSource(of: file).components(separatedBy: "\n")
            for (index, line) in lines.enumerated() where matches(Self.literalCurrency, line) {
                offenders.append("\(file.lastPathComponent):\(index + 1)")
            }
        }
        XCTAssertEqual(offenders, [], "these amounts are labelled by construction rather than from the payload")
    }

    func testTheServiceRowPricesThroughTheFormatterRatherThanACurrencyString() throws {
        let flat = try shippedSource(of: url("\(Self.bookingDir)/Steps/ServicesStepComponents.swift"))
            .replacingOccurrences(of: "\\s+", with: " ", options: .regularExpression)
        XCTAssertTrue(
            flat.contains("L10n.Booking.priceFrom(price(service.basePrice))"),
            "the base price is no longer formatted with the catalogue currency"
        )
        XCTAssertTrue(
            flat.contains("L10n.Booking.pricePerRoom(price(service.perRoomPrice))"),
            "the per-room price is no longer formatted with the catalogue currency"
        )
    }

    func testThePriceStringsCarryNoCurrencyOfTheirOwnInAnyLocale() throws {
        let data = try Data(contentsOf: url(Self.catalogPath))
        let catalog = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        let strings = try XCTUnwrap(catalog["strings"] as? [String: Any])
        for key in Self.priceKeys {
            let entry = try XCTUnwrap(strings[key] as? [String: Any], "\(key) missing from the catalog")
            let localizations = try XCTUnwrap(entry["localizations"] as? [String: Any])
            for locale in Self.locales {
                let unit = (localizations[locale] as? [String: Any])?["stringUnit"] as? [String: Any]
                let value = try XCTUnwrap(unit?["value"] as? String, "\(locale) lost \(key)")
                XCTAssertTrue(value.contains("%1$@"), "\(locale)/\(key) takes a formatted amount, not a bare number")
                XCTAssertFalse(
                    matches(Self.currencyLiteral, value),
                    "\(locale)/\(key) still names a currency: \(value)"
                )
            }
        }
    }

    func testTheConfirmStepLabelsEveryRowWithTheResolvedDisplayCurrency() throws {
        let confirm = try shippedSource(of: url("\(Self.bookingDir)/Confirm/ConfirmStep.swift"))
        XCTAssertTrue(
            confirm.contains("viewModel.displayCurrencyCode"),
            "the confirm step no longer reads the wizard's resolved currency"
        )
    }

    private func matches(_ pattern: String, _ text: String) -> Bool {
        text.range(of: pattern, options: .regularExpression) != nil
    }

    /// Previews and comments are stripped: a `#if DEBUG` fixture and a prose mention of `"CZK"` are
    /// not call sites. Lines are blanked rather than dropped so a reported line number is real.
    private func shippedSource(of file: URL) throws -> String {
        let text = try String(contentsOf: file, encoding: .utf8)
        var kept: [String] = []
        var depth = 0
        var debugDepth: Int?
        for line in text.components(separatedBy: "\n") {
            let trimmed = line.trimmingCharacters(in: .whitespaces)
            if trimmed.hasPrefix("#if") {
                depth += 1
                if debugDepth == nil, trimmed.hasPrefix("#if DEBUG") { debugDepth = depth }
            } else if trimmed.hasPrefix("#endif") {
                if debugDepth == depth { debugDepth = nil }
                depth -= 1
            } else if debugDepth == nil, !trimmed.hasPrefix("//") {
                kept.append(line)
                continue
            }
            kept.append("")
        }
        return kept.joined(separator: "\n")
    }

    private func swiftFiles(under relativeDir: String) throws -> [URL] {
        let root = url(relativeDir)
        let enumerator = try XCTUnwrap(FileManager.default.enumerator(at: root, includingPropertiesForKeys: nil))
        return enumerator.compactMap { $0 as? URL }.filter { $0.pathExtension == "swift" }
    }

    private func url(_ relativePath: String) -> URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent(relativePath)
    }
}
