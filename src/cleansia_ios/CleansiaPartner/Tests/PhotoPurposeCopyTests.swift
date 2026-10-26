import Foundation
import XCTest
@testable import CleansiaPartner

/// The camera and photo-library prompts name the documents a cleaner now photographs or picks, and the
/// library one no longer promises job photos, which come from the camera only. Read through the BUILT
/// `InfoPlist.strings` tables, the `LocationPurposeCopyTests` idiom, so every shipped language answers.
final class PhotoPurposeCopyTests: XCTestCase {
    private let appBundle = Bundle(identifier: "cz.cleansia.partner") ?? .main
    private static let camera = "NSCameraUsageDescription"
    private static let library = "NSPhotoLibraryUsageDescription"

    private static let documentWord = [
        "en": "document",
        "cs": "dokument",
        "sk": "dokument",
        "uk": "документ",
        "ru": "документ"
    ]

    private static let jobPhotoClaim = [
        "en": "before and after",
        "cs": "před úklidem",
        "sk": "pred upratovaním",
        "uk": "до та після",
        "ru": "до и после"
    ]

    func testBothPromptsNameTheDocumentsInEveryLocale() throws {
        for (language, word) in Self.documentWord {
            for key in [Self.camera, Self.library] {
                let value = try purpose(key, in: language)
                XCTAssertNotNil(
                    value.range(of: word, options: .caseInsensitive),
                    "\(language) \(key) does not name documents: \(value)"
                )
            }
        }
    }

    func testTheLibraryPromptNoLongerOffersJobPhotos() throws {
        for (language, claim) in Self.jobPhotoClaim {
            let value = try purpose(Self.library, in: language)
            XCTAssertNil(
                value.range(of: claim, options: .caseInsensitive),
                "\(language) still offers job photos from the library: \(value)"
            )
        }
    }

    /// The Info.plist values are what a language without its own table falls back to.
    func testTheBaseInfoPlistReadsLikeTheEnglishTable() throws {
        for key in [Self.camera, Self.library] {
            let base = try XCTUnwrap(appBundle.infoDictionary?[key] as? String, "no base \(key)")
            XCTAssertEqual(base, try purpose(key, in: "en"))
        }
    }

    private func purpose(_ key: String, in language: String) throws -> String {
        let lproj = try XCTUnwrap(
            appBundle.url(forResource: language, withExtension: "lproj"),
            "\(language).lproj missing from the app bundle"
        )
        let table = try XCTUnwrap(
            NSDictionary(contentsOf: lproj.appendingPathComponent("InfoPlist.strings")) as? [String: String],
            "InfoPlist.strings unreadable for \(language)"
        )
        return try XCTUnwrap(table[key], "\(language) has no \(key)")
    }
}
