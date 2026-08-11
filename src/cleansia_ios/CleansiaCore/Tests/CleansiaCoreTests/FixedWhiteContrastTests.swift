import Foundation
import XCTest
@testable import CleansiaCore

/// Asserts the hex SOURCE, not a resolved-color roundtrip: SwiftUI `Color` → `UIColor` flattens
/// dynamic providers on the iOS-16 floor, so trait resolution is runtime-dependent while the hexes
/// the shipped `Color`s derive from are not (the `BrandGradientTests` rule). What binds this
/// arithmetic to the two heroes that render it is `AvatarDiscBindingTests` below — without it these
/// constants would only model a surface, never observe one.
final class FixedWhiteContrastTests: XCTestCase {
    func testInkClearsTheLargeTextFloorAgainstTheFixedWhiteSurface() {
        let ratio = contrastRatio(CleansiaColors.onFixedWhiteHex, CleansiaColors.fixedWhiteHex)
        XCTAssertGreaterThanOrEqual(ratio, largeTextFloor)
        XCTAssertEqual(ratio, 4.095, accuracy: 0.001)
    }

    func testTheAdaptivePrimaryWouldMissTheFloorOnThatSurface() {
        let ratio = contrastRatio(adaptivePrimaryDark, CleansiaColors.fixedWhiteHex)
        XCTAssertLessThan(ratio, largeTextFloor)
        XCTAssertEqual(ratio, 2.142, accuracy: 0.001)
    }

    func testTheFormulaReproducesTheWcagReferenceRatios() {
        XCTAssertEqual(contrastRatio(0x000000, 0xFFFFFF), 21, accuracy: 0.001)
        XCTAssertEqual(contrastRatio(0xFFFFFF, 0xFFFFFF), 1, accuracy: 0.001)
        XCTAssertEqual(contrastRatio(0x777777, 0xFFFFFF), 4.478, accuracy: 0.001)
    }
}

/// A rendered SwiftUI colour is not observable in a unit test, but the token a call site NAMES is —
/// so bind the two profile heroes to the token by source, the `ConsentCatalogTests` idiom. Both
/// files are queued for further edits on these exact hunks, and re-typing the theme-adaptive
/// `primary` there would leave every other test green, both linters clean and light mode perfect
/// while dark mode silently returned to 2.14:1.
///
/// The customer disc now lives in its own `ProfileAvatar`, shared by the hero and the edit screen, so
/// the guard follows it there and covers both surfaces — the initials remain the no-photo fallback
/// under the uploaded avatar.
final class AvatarDiscBindingTests: XCTestCase {
    private static let heroes = [
        "CleansiaCustomer/Sources/Features/Profile/ProfileAvatar.swift",
        "CleansiaPartner/Sources/Features/Profile/ProfileHubContent.swift"
    ]

    /// The fills a hero disc is known to carry, mapped to what they render. `Color.white` is a
    /// compile-time constant, so reading the source is stronger than resolving it. A fill outside
    /// this table is not itself a defect — it means the pair is unmeasured, and the ratio below is
    /// void until someone measures it and adds the entry.
    private static let discFills: [String: UInt32] = [
        "Color.white": CleansiaColors.fixedWhiteHex
    ]

    private static let discInks: [String: UInt32] = [
        "CleansiaColors.onFixedWhite": CleansiaColors.onFixedWhiteHex
    ]

    func testBothHeroDiscsPairAMeasuredInkWithAMeasuredFill() throws {
        for hero in Self.heroes {
            let block = try discBlock(of: hero)
            let fills = arguments(of: ".fill(", in: block)
            let inks = arguments(of: ".foregroundColor(", in: block)
            XCTAssertEqual(fills.count, 1, "\(hero): expected exactly one disc fill, found \(fills)")
            XCTAssertEqual(inks.count, 1, "\(hero): expected exactly one initials ink, found \(inks)")

            let fill = try XCTUnwrap(fills.first.flatMap { Self.discFills[$0] }, unmeasured(hero, fills))
            let ink = try XCTUnwrap(inks.first.flatMap { Self.discInks[$0] }, unmeasured(hero, inks))

            let ratio = contrastRatio(ink, fill)
            XCTAssertGreaterThanOrEqual(ratio, largeTextFloor, "\(hero): initials measure \(ratio):1 on the disc")
            XCTAssertEqual(ratio, 4.095, accuracy: 0.001, hero)
        }
    }

    func testNeitherHeroDiscUsesTheThemeAdaptivePrimary() throws {
        for hero in Self.heroes {
            let block = try discBlock(of: hero)
            let tokens = arguments(of: ".fill(", in: block) + arguments(of: ".foregroundColor(", in: block)
            XCTAssertFalse(
                tokens.contains("CleansiaColors.primary"),
                "\(hero): the disc is fixed white in both schemes, so primary measures 2.14:1 in dark"
            )
        }
    }

    private func unmeasured(_ hero: String, _ found: [String]) -> String {
        "\(hero): \(found) is not a measured avatar-disc colour — measure the pair and add it to the table"
    }

    /// The innermost brace-delimited block holding both the initials `Text` and the disc's `Circle`,
    /// matched outward from the text — anchored on the two elements themselves, so it survives
    /// reformatting and fails loudly on a restructure instead of drifting to a neighbouring view the
    /// way a fixed line window would.
    private func discBlock(of hero: String, file: StaticString = #filePath, line: UInt = #line) throws -> String {
        let source = try String(contentsOf: iosRoot().appendingPathComponent(hero), encoding: .utf8)
        let chars = Array(source)
        let anchors = occurrences(of: "Text(initials)", in: chars)
        XCTAssertEqual(anchors.count, 1, "\(hero): expected one initials Text", file: file, line: line)
        var cursor = try XCTUnwrap(anchors.first, "\(hero): no initials Text", file: file, line: line)

        for _ in 0 ..< 6 {
            guard let open = enclosingOpenBrace(chars, before: cursor),
                  let close = matchingCloseBrace(chars, from: open) else { break }
            let block = String(chars[open ... close])
            if block.contains("Circle(") { return block }
            cursor = open
        }
        XCTFail("\(hero): no block encloses both the initials Text and the disc Circle", file: file, line: line)
        return ""
    }

    private func iosRoot() -> URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
    }

    private func occurrences(of needle: String, in chars: [Character]) -> [Int] {
        let pattern = Array(needle)
        guard !pattern.isEmpty, chars.count >= pattern.count else { return [] }
        return (0 ... chars.count - pattern.count).filter { Array(chars[$0 ..< $0 + pattern.count]) == pattern }
    }

    private func enclosingOpenBrace(_ chars: [Character], before index: Int) -> Int? {
        var depth = 0
        var cursor = index - 1
        while cursor >= 0 {
            if chars[cursor] == "}" { depth += 1 }
            if chars[cursor] == "{" {
                if depth == 0 { return cursor }
                depth -= 1
            }
            cursor -= 1
        }
        return nil
    }

    private func matchingCloseBrace(_ chars: [Character], from index: Int) -> Int? {
        var depth = 0
        var cursor = index
        while cursor < chars.count {
            if chars[cursor] == "{" { depth += 1 }
            if chars[cursor] == "}" {
                depth -= 1
                if depth == 0 { return cursor }
            }
            cursor += 1
        }
        return nil
    }

    private func arguments(of label: String, in block: String) -> [String] {
        let chars = Array(block)
        let pattern = Array(label)
        var results: [String] = []
        var index = 0
        while index + pattern.count <= chars.count {
            guard Array(chars[index ..< index + pattern.count]) == pattern else {
                index += 1
                continue
            }
            var depth = 1
            var cursor = index + pattern.count
            while cursor < chars.count, depth > 0 {
                if chars[cursor] == "(" { depth += 1 }
                if chars[cursor] == ")" { depth -= 1 }
                if depth > 0 { cursor += 1 }
            }
            let argument = String(chars[(index + pattern.count) ..< cursor])
            results.append(argument.trimmingCharacters(in: .whitespacesAndNewlines))
            index = cursor
        }
        return results
    }
}

/// WCAG 2.x 1.4.3 floor for large text — the headline-small initials qualify.
private let largeTextFloor = 3.0

/// sky400 — what the theme-adaptive `CleansiaColors.primary` resolves to in dark mode.
private let adaptivePrimaryDark: UInt32 = 0x38BDF8

private func contrastRatio(_ foreground: UInt32, _ background: UInt32) -> Double {
    let first = relativeLuminance(foreground)
    let second = relativeLuminance(background)
    return (max(first, second) + 0.05) / (min(first, second) + 0.05)
}

private func relativeLuminance(_ hex: UInt32) -> Double {
    let channels = [16, 8, 0].map { Double((hex >> UInt32($0)) & 0xFF) / 255 }
    let linear = channels.map { $0 <= 0.03928 ? $0 / 12.92 : pow(($0 + 0.055) / 1.055, 2.4) }
    return 0.2126 * linear[0] + 0.7152 * linear[1] + 0.0722 * linear[2]
}
