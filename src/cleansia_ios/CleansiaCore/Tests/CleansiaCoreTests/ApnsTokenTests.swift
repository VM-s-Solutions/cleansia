import Foundation
import XCTest
@testable import CleansiaCore

final class ApnsTokenTests: XCTestCase {
    func testEncodesBytesAsLowercaseHex() {
        let data = Data([0x00, 0x0F, 0xA5, 0xFF])
        XCTAssertEqual(data.apnsHexToken, "000fa5ff")
    }

    func testEmptyDataIsEmptyString() {
        XCTAssertEqual(Data().apnsHexToken, "")
    }

    func testEachByteIsTwoHexDigits() {
        let data = Data([0x01, 0x02, 0x03, 0x04])
        XCTAssertEqual(data.apnsHexToken.count, 8)
        XCTAssertEqual(data.apnsHexToken, "01020304")
    }
}
