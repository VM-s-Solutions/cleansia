import XCTest
@testable import CleansiaCustomer

/// Owner ruling 2026-09-28: the cleaner reports that they cannot get in no earlier than
/// `BookingPolicy.LockoutWaitMinutes` past the start, and a confirmed lockout costs the whole price. The
/// FAQ answer about not being home states both, read through the built bundle in every locale.
final class LockoutFaqTests: XCTestCase {
    private static let fullPrice = [
        "en": "full price",
        "cs": "plná cena",
        "sk": "plná cena",
        "uk": "повна вартість",
        "ru": "полная стоимость"
    ]

    private var restoreBundle: Bundle?

    override func setUp() {
        super.setUp()
        restoreBundle = L10n.bundle
    }

    override func tearDown() {
        L10n.bundle = restoreBundle ?? .main
        super.tearDown()
    }

    func testTheAnswerStatesTheWaitAndTheWholePriceInEveryLocale() throws {
        let wait = try Self.lockoutWaitMinutes()
        for (language, fee) in Self.fullPrice {
            L10n.bundle = try Self.localeBundle(language)
            let answer = L10n.Help.faqA2
            XCTAssertTrue(answer.contains("\(wait)"), "\(language) does not state the \(wait)-minute wait: \(answer)")
            XCTAssertTrue(answer.lowercased().contains(fee), "\(language) does not state the full price: \(answer)")
        }
    }

    private static func lockoutWaitMinutes() throws -> Int {
        let policy = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs")
        let source = try String(contentsOf: policy, encoding: .utf8)
        let regex = try NSRegularExpression(pattern: #"public\s+const\s+int\s+LockoutWaitMinutes\s*=\s*(\d+)\s*;"#)
        let match = try XCTUnwrap(
            regex.firstMatch(in: source, range: NSRange(source.startIndex..., in: source)),
            "BookingPolicy.LockoutWaitMinutes not found — the parser needs updating"
        )
        let digits = try XCTUnwrap(Range(match.range(at: 1), in: source))
        return try XCTUnwrap(Int(source[digits]))
    }

    private static func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
    }
}
