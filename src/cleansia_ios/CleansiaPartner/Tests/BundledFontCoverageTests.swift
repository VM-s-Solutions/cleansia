import CoreText
import CryptoKit
import SwiftUI
import XCTest
@testable import CleansiaCore

/// The premise the Cyrillic fallback rests on, asserted against the binaries this app ships.
final class BundledFontCoverageTests: XCTestCase {
    static let cyrillic: [UnicodeScalar] = {
        var scalars = Set((0x0400 ... 0x045F).compactMap { UnicodeScalar($0) })
        scalars.formUnion("ҐґЄєІіЇї".unicodeScalars)
        return scalars.sorted { $0.value < $1.value }
    }()

    /// Latin plus the cs/sk diacritics the brand face is bundled to draw. This is the positive
    /// control: "Poppins covers none of the Cyrillic" passes trivially if the font never loaded.
    private static let latin = Array("AZaz09ěščřžýáíéůúďťňĚŠČŘŽÝÁÍÉŮÚ".unicodeScalars)

    /// Identical to the six the Android app ships and to the six the partner app ships.
    private static let expectedSHA256 = [
        "Poppins-Medium": "90373e7d838d32468438fc3e152dca0bdb12edcab99ea639f158790b1ba1fd05",
        "Poppins-SemiBold": "d3bf1bdaf0550e83da9ac0b1d1d9fe6db086835a83aa28578e609a394b9a0286",
        "Poppins-Bold": "983676516167748b74de6f4771fb384c664fd913acb8b471122ecacf5da5ea6c",
        "Nunito-Regular": "1aaa595c48c316d48195d1ec4f23e42244c7353dc19d4785f91d9cbef5b09d20",
        "Nunito-SemiBold": "3829f6303e435baf3f0eec58f244b6314a968c1234e0ec4e2e17a84c01319904",
        "Nunito-Bold": "49b3154dbba906658d6b5c31dd3b1ebc1db7bb2c014a10b8533167b5d39d647b"
    ]

    static func face(_ face: CleansiaFont.BundledFace, size: CGFloat = 18) throws -> CTFont {
        try XCTUnwrap(
            UIFont(name: face.rawValue, size: size),
            "\(face.rawValue) is not registered — check UIAppFonts and the bundled .ttf"
        ) as CTFont
    }

    private func covered(_ scalars: [UnicodeScalar], by font: CTFont) -> Int {
        scalars.filter { scalar in
            var utf16 = Array(String(scalar).utf16)
            var glyphs = [CGGlyph](repeating: 0, count: utf16.count)
            return CTFontGetGlyphsForCharacters(font, &utf16, &glyphs, utf16.count)
        }.count
    }

    func testTargetSetIsTheNinetyEightCyrillicCodePoints() {
        XCTAssertEqual(Self.cyrillic.count, 98)
    }

    func testEveryBundledFaceIsRegistered() {
        for face in CleansiaFont.BundledFace.allCases {
            XCTAssertNotNil(UIFont(name: face.rawValue, size: 18), face.rawValue)
        }
    }

    func testPoppinsDrawsLatin() throws {
        for face in CleansiaFont.BundledFace.allCases where face.family == .poppins {
            XCTAssertEqual(try covered(Self.latin, by: Self.face(face)), Self.latin.count, face.rawValue)
        }
    }

    func testPoppinsDrawsNoCyrillic() throws {
        for face in CleansiaFont.BundledFace.allCases where face.family == .poppins {
            XCTAssertEqual(try covered(Self.cyrillic, by: Self.face(face)), 0, face.rawValue)
        }
    }

    func testNunitoDrawsEveryCyrillicCodePoint() throws {
        for face in CleansiaFont.BundledFace.allCases where face.family == .nunito {
            XCTAssertEqual(try covered(Self.cyrillic, by: Self.face(face)), Self.cyrillic.count, face.rawValue)
        }
    }

    func testDeclaredCyrillicCapabilityMatchesTheBinaries() throws {
        for face in CleansiaFont.BundledFace.allCases {
            let drawsAll = try covered(Self.cyrillic, by: Self.face(face)) == Self.cyrillic.count
            XCTAssertEqual(face.family.drawsCyrillic, drawsAll, face.rawValue)
        }
    }

    func testBundledBinariesAreTheExpectedFiles() throws {
        for face in CleansiaFont.BundledFace.allCases {
            let url = try XCTUnwrap(Bundle.main.url(forResource: face.rawValue, withExtension: "ttf"), face.rawValue)
            let digest = try SHA256.hash(data: Data(contentsOf: url)).map { String(format: "%02x", $0) }.joined()
            XCTAssertEqual(digest, Self.expectedSHA256[face.rawValue], face.rawValue)
        }
    }
}
