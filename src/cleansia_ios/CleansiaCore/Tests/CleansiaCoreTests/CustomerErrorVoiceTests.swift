import XCTest
@testable import CleansiaCore

/// The mirror of `PartnerErrorVoiceTests` for the booking refusals only a customer can receive. The
/// express-waiver code is why it exists: the backend gave that refusal its own key precisely because
/// `order.total_price.not_match` — the generic "the price changed" every client renders — is the one
/// sentence that cannot explain a Plus member's free express upgrade running out between the quote and
/// the submit.
final class CustomerErrorVoiceTests: XCTestCase {
    private static let locales = ["en", "cs", "sk", "uk", "ru"]

    /// Keys whose every backend emitter is a customer booking command.
    private static let customerOnly = [
        CustomerOnlyKey("membership.express_waiver.no_longer_available", emitters: "CreateOrder"),
        CustomerOnlyKey("order.span_exceeds_maximum", emitters: "CreateOrder, QuoteOrder"),
        CustomerOnlyKey("order.empty", emitters: "CreateOrder"),
        CustomerOnlyKey("order.address_exactly_one_required", emitters: "CreateOrder"),
        CustomerOnlyKey("order.cleaning_date.future", emitters: "CreateOrder"),
        CustomerOnlyKey("order.cleaning_date.below_lead_time", emitters: "CreateOrder"),
        CustomerOnlyKey("order.selected_services.invalid", emitters: "CreateOrder, QuoteOrder"),
        CustomerOnlyKey("order.selected_package.invalid", emitters: "CreateOrder, QuoteOrder"),
        CustomerOnlyKey("order.preferred_employee.not_eligible", emitters: "CreateOrder"),
        CustomerOnlyKey("order.total_price.not_match", emitters: "CreateOrder")
    ]

    /// The word for the express slot in each locale, as the booking summary already spells it.
    private static let expressVocabulary = [
        "en": "express",
        "cs": "expres",
        "sk": "expres",
        "uk": "експрес",
        "ru": "экспресс"
    ]

    override func tearDown() {
        CoreL10n.bundle = .module
        super.tearDown()
    }

    func testEveryCustomerOnlyKeyResolvesToASentenceInAllFiveLocales() {
        var gaps: [String] = []
        for locale in Self.locales {
            for entry in Self.customerOnly {
                let resolved = resolve(entry.key, locale: locale)
                if resolved == entry.key || resolved.isEmpty {
                    gaps.append("\(entry.key) · \(locale) · emitted by \(entry.emitters), shows the raw key")
                }
            }
        }
        assertNoViolations(gaps, "customer-only error keys with no Core catalog entry")
    }

    func testTheExpressWaiverRefusalNamesTheExpressSurcharge() {
        var vague: [String] = []
        for locale in Self.locales {
            let word = Self.expressVocabulary[locale] ?? ""
            let resolved = resolve("membership.express_waiver.no_longer_available", locale: locale)
            if !resolved.lowercased().contains(word) {
                vague.append("\(locale) · never says \"\(word)\": \"\(resolved)\"")
            }
        }
        assertNoViolations(vague, "express-waiver refusals that don't say what changed")
    }

    func testTheExpressWaiverRefusalIsNotTheGenericPriceChange() {
        for locale in Self.locales {
            XCTAssertNotEqual(
                resolve("membership.express_waiver.no_longer_available", locale: locale),
                resolve("order.total_price.not_match", locale: locale),
                "\(locale): the express waiver reads as the generic price-changed refusal it was split off from"
            )
        }
    }

    func testTheLocalizerReachesTheExpressWaiverCopyThroughTheServerErrorKey() {
        CoreL10n.bundle = CoreL10n.localizedBundle(for: "en")
        let error = ApiError(
            code: "membership.express_waiver.no_longer_available",
            message: "raw server text",
            httpStatus: 400
        )

        XCTAssertEqual(
            ApiErrorLocalizer().message(for: error),
            resolve("membership.express_waiver.no_longer_available", locale: "en")
        )
    }

    private func resolve(_ key: String, locale: String) -> String {
        let sentinel = "\u{1}"
        let value = CoreL10n.localizedBundle(for: locale)
            .localizedString(forKey: "error." + key, value: sentinel, table: nil)
        return value == sentinel ? key : value
    }

    private func assertNoViolations(
        _ violations: [String],
        _ what: String,
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        guard !violations.isEmpty else { return }
        let listing = violations.sorted().map { "  • \($0)" }.joined(separator: "\n")
        XCTFail("\(violations.count) \(what):\n\(listing)", file: file, line: line)
    }
}

private struct CustomerOnlyKey {
    let key: String
    let emitters: String

    init(_ key: String, emitters: String) {
        self.key = key
        self.emitters = emitters
    }
}
