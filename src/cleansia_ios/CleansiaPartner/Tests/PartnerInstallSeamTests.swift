import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// Gate-4d red tests for the T-0370 container seam: these go through the REAL
/// `installGeneratedClientAuth()` and assert the generated-client globals it
/// hardens, so deleting the install lines turns them red.
@MainActor
final class PartnerInstallSeamTests: XCTestCase {
    private struct Box: Decodable {
        let date: Date
    }

    func testInstallMovesResponseProcessingOffTheMainQueue() throws {
        try makeInstalledContainer()

        XCTAssertFalse(CleansiaPartnerApiAPI.apiResponseQueue === DispatchQueue.main)
    }

    func testInstallHardensTheActualGlobalDecoderAgainstOffsetlessDates() throws {
        try makeInstalledContainer()

        let box = try CodableHelper.jsonDecoder.decode(
            Box.self,
            from: Data(#"{"date":"2026-07-02T10:11:12"}"#.utf8)
        )

        var components = DateComponents()
        components.calendar = Calendar(identifier: .iso8601)
        components.timeZone = TimeZone(secondsFromGMT: 0)
        components.year = 2026
        components.month = 7
        components.day = 2
        components.hour = 10
        components.minute = 11
        components.second = 12
        XCTAssertEqual(box.date, try XCTUnwrap(components.date))
    }

    @discardableResult
    private func makeInstalledContainer() throws -> PartnerAppContainer {
        let baseURL = try XCTUnwrap(URL(string: "https://install-seam.test"))
        let container = PartnerAppContainer(snackbar: SnackbarController(), apiBaseURL: baseURL)
        container.installGeneratedClientAuth()
        return container
    }
}
