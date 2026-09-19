import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// The production market client over the real generated `PartnerMarketAPI`: it reads the partner host's
/// `Market/GetOverview` anonymously and refuses a directory whose row cannot be chosen or named.
@MainActor
final class RegisterMarketClientTests: XCTestCase {
    private static let overviewPath = "/api/Market/GetOverview"

    private var bodies: WireBodies!

    override func setUp() {
        super.setUp()
        bodies = WireBodies()
    }

    override func tearDown() {
        GenMockURLProtocol.handler = nil
        bodies = nil
        super.tearDown()
    }

    private func serve(_ status: Int, _ json: String) {
        let answer = (status, Data(json.utf8))
        GeneratedWireSpine.install(recording: bodies) { _ in answer }
    }

    private static func row(
        countryId: String? = "cze",
        isoCode: String? = "CZE",
        name: String? = "Czechia",
        currencyCode: String? = "CZK",
        isDefault: Bool = true
    ) -> String {
        let quoted: (String?) -> String = { $0.map { "\"\($0)\"" } ?? "null" }
        return """
        {"countryId":\(quoted(countryId)),"isoCode":\(quoted(isoCode)),"isoAlpha2":"CZ","name":\(quoted(name)),
         "translations":{"cs":{"name":"Česko","description":null}},"currencyId":"cur-czk",
         "currencyCode":\(quoted(currencyCode)),"currencySymbol":"Kč","isDefault":\(isDefault),
         "noShowCredit":250,"insuranceCoverageAmount":1000000}
        """
    }

    func testTheDirectoryIsReadFromThePartnerHostsMarketEndpoint() async throws {
        serve(200, "[\(Self.row())]")

        let markets = try await LivePartnerMarketClient().getMarkets().get()

        XCTAssertEqual(bodies.paths, [Self.overviewPath])
        XCTAssertEqual(bodies.method(ofPath: Self.overviewPath), "GET")
        XCTAssertEqual(markets, [RegisterMarket(
            countryId: "cze",
            isoCode: "CZE",
            name: "Czechia",
            translations: ["cs": "Česko"],
            currencyCode: "CZK",
            isDefault: true
        )])
    }

    func testARowWithNoCountryIdRefusesTheWholeDirectory() async {
        serve(200, "[\(Self.row()),\(Self.row(countryId: ""))]")

        let result = await LivePartnerMarketClient().getMarkets()

        XCTAssertEqual(result.apiErrorOrNil?.code, ApiError.wireContractCode)
        XCTAssertEqual(result.apiErrorOrNil?.message?.contains("countryId"), true)
    }

    func testARowWithNoNameOrCurrencyRefusesTheWholeDirectory() async {
        for broken in [Self.row(name: nil), Self.row(currencyCode: nil)] {
            serve(200, "[\(broken)]")

            let result = await LivePartnerMarketClient().getMarkets()

            XCTAssertEqual(result.apiErrorOrNil?.code, ApiError.wireContractCode, broken)
        }
    }

    func testAServerFailureIsTheServersError() async {
        serve(503, #"{"title":"Service Unavailable","status":503}"#)

        let result = await LivePartnerMarketClient().getMarkets()

        XCTAssertEqual(result.apiErrorOrNil?.httpStatus, 503)
    }
}
