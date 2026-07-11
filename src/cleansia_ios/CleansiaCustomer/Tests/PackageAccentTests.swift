import CleansiaCore
import XCTest
@testable import CleansiaCustomer

final class PackageAccentTests: XCTestCase {
    func testAccentCyclesBluePurpleCyanByIndex() {
        XCTAssertEqual(PackageAccent.gradient(for: 0), .blue)
        XCTAssertEqual(PackageAccent.gradient(for: 1), .purple)
        XCTAssertEqual(PackageAccent.gradient(for: 2), .cyan)
    }

    func testAccentWrapsEveryThreePackages() {
        XCTAssertEqual(PackageAccent.gradient(for: 3), .blue)
        XCTAssertEqual(PackageAccent.gradient(for: 4), .purple)
        XCTAssertEqual(PackageAccent.gradient(for: 5), .cyan)
    }
}
