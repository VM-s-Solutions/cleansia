import XCTest

/// VoiceOver read mascot art by its asset name ("mascot_waving"), where Android hides every mascot. The art is
/// decoration wherever it is drawn, so the one image every mascot comes from says so.
final class MascotAccessibilityTests: XCTestCase {
    func testEveryMascotImageIsDecorative() throws {
        let source = try String(
            contentsOf: URL(fileURLWithPath: #filePath)
                .deletingLastPathComponent()
                .deletingLastPathComponent()
                .deletingLastPathComponent()
                .appendingPathComponent("Sources/CleansiaCore/Components/Mascot.swift"),
            encoding: .utf8
        )
        .components(separatedBy: .whitespacesAndNewlines)
        .joined()

        XCTAssertTrue(source.contains("Image(decorative:rawValue,bundle:MascotAssets.bundle)"))
        XCTAssertFalse(source.contains("Image(rawValue,"), "a mascot is announced by its asset name again")
    }
}
