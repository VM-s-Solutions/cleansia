import XCTest
@testable import CleansiaCore

private actor StubServiceAreaDataSource: ServiceAreaDataSource {
    private var results: [ApiResult<[ServicedCountry]>]
    private(set) var fetchCount = 0
    private var gate: CheckedContinuation<Void, Never>?
    private var gated = false

    init(results: [ApiResult<[ServicedCountry]>]) {
        self.results = results
    }

    func gateNextFetch() {
        gated = true
    }

    func openGate() {
        gate?.resume()
        gate = nil
        gated = false
    }

    func fetchServicedCountries() async -> ApiResult<[ServicedCountry]> {
        fetchCount += 1
        if gated {
            await withCheckedContinuation { gate = $0 }
        }
        return results.count > 1 ? results.removeFirst() : results[0]
    }
}

final class ServiceAreaProviderTests: XCTestCase {
    private let czechia = ServicedCountry(id: "cz-id", isoCode: "CZE", name: "Czechia")
    private let slovakia = ServicedCountry(id: "sk-id", isoCode: "SVK", name: "Slovakia")

    func testSuccessIsCachedSoASecondLoadDoesNotRefetch() async {
        let stub = StubServiceAreaDataSource(results: [.success([czechia])])
        let provider = ServiceAreaProvider(dataSource: stub)

        let first = await provider.loadCountries()
        let second = await provider.loadCountries()

        XCTAssertEqual(first, [czechia])
        XCTAssertEqual(second, [czechia])
        let fetches = await stub.fetchCount
        XCTAssertEqual(fetches, 1)
    }

    func testFailureIsNotCachedAndTheNextLoadRetries() async {
        let stub = StubServiceAreaDataSource(results: [
            .failure(ApiError(code: "network.unreachable")),
            .success([slovakia])
        ])
        let provider = ServiceAreaProvider(dataSource: stub)

        let failed = await provider.loadCountries()
        let retried = await provider.loadCountries()

        XCTAssertNil(failed)
        XCTAssertEqual(retried, [slovakia])
        let fetches = await stub.fetchCount
        XCTAssertEqual(fetches, 2)
    }

    func testLoadCountriesResultSurfacesTheTypedFailure() async {
        let stub = StubServiceAreaDataSource(results: [.failure(ApiError(code: "network.unreachable"))])
        let provider = ServiceAreaProvider(dataSource: stub)

        guard case let .failure(error) = await provider.loadCountriesResult() else {
            return XCTFail("expected the failure to surface")
        }
        XCTAssertEqual(error.code, "network.unreachable")
    }

    func testEmptySuccessIsCachedAsTheServerAnswerNotRetried() async {
        let stub = StubServiceAreaDataSource(results: [.success([])])
        let provider = ServiceAreaProvider(dataSource: stub)

        let first = await provider.loadCountries()
        let second = await provider.loadCountries()

        XCTAssertEqual(first, [])
        XCTAssertEqual(second, [])
        let fetches = await stub.fetchCount
        XCTAssertEqual(fetches, 1)
    }

    func testConcurrentLoadsShareOneFetch() async {
        let stub = StubServiceAreaDataSource(results: [.success([czechia])])
        await stub.gateNextFetch()
        let provider = ServiceAreaProvider(dataSource: stub)

        async let first = provider.loadCountries()
        async let second = provider.loadCountries()
        try? await Task.sleep(nanoseconds: 50_000_000)
        await stub.openGate()
        let results = await [first, second]

        XCTAssertEqual(results[0], [czechia])
        XCTAssertEqual(results[1], [czechia])
        let fetches = await stub.fetchCount
        XCTAssertEqual(fetches, 1)
    }

    func testRefreshClearsTheCacheSoTheNextLoadRefetches() async {
        let stub = StubServiceAreaDataSource(results: [.success([czechia]), .success([czechia, slovakia])])
        let provider = ServiceAreaProvider(dataSource: stub)

        _ = await provider.loadCountries()
        await provider.refresh()
        let reloaded = await provider.loadCountries()

        XCTAssertEqual(reloaded, [czechia, slovakia])
        let fetches = await stub.fetchCount
        XCTAssertEqual(fetches, 2)
    }

    func testIsoCodesNormalizeAlpha3ToLowercaseAlpha2() async {
        let stub = StubServiceAreaDataSource(results: [.success([czechia, slovakia])])
        let provider = ServiceAreaProvider(dataSource: stub)

        let codes = await provider.servicedCountryIsoCodes()

        XCTAssertEqual(codes, ["cz", "sk"])
    }

    func testIsoCodesAreNilOnFailureNeverAnEmptyBias() async {
        let stub = StubServiceAreaDataSource(results: [.failure(ApiError(code: "network.unreachable"))])
        let provider = ServiceAreaProvider(dataSource: stub)

        let codes = await provider.servicedCountryIsoCodes()

        XCTAssertNil(codes)
    }
}
