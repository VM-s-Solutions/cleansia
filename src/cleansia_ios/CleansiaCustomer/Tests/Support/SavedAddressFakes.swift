import CleansiaCore
import Foundation
@testable import CleansiaCustomer

final class FakeSavedAddressClient: SavedAddressClient, @unchecked Sendable {
    var pages: [[SavedAddress]] = []
    var getMineError: ApiError?
    private(set) var getMineCallCount = 0

    var addResult: ApiResult<SavedAddress>?
    private(set) var addCallCount = 0
    private(set) var lastAddDraft: SavedAddressDraft?

    var updateResult: ApiResult<SavedAddress>?
    private(set) var updateCallCount = 0
    private(set) var lastUpdate: (id: String, draft: SavedAddressDraft)?

    var setDefaultResult: ApiResult<Void> = .success(())
    private(set) var setDefaultCallCount = 0
    private(set) var lastSetDefaultId: String?

    var deleteResult: ApiResult<Void> = .success(())
    private(set) var deleteCallCount = 0
    private(set) var lastDeleteId: String?

    func getMine() async -> ApiResult<[SavedAddress]> {
        defer { getMineCallCount += 1 }
        if let getMineError { return .failure(getMineError) }
        let index = min(getMineCallCount, pages.count - 1)
        guard index >= 0 else { return .success([]) }
        return .success(pages[index])
    }

    func add(_ draft: SavedAddressDraft) async -> ApiResult<SavedAddress> {
        addCallCount += 1
        lastAddDraft = draft
        return addResult ?? .success(SavedAddressFixtures.address(id: "added"))
    }

    func update(id: String, draft: SavedAddressDraft) async -> ApiResult<SavedAddress> {
        updateCallCount += 1
        lastUpdate = (id, draft)
        return updateResult ?? .success(SavedAddressFixtures.address(id: id, label: draft.label))
    }

    func setDefault(id: String) async -> ApiResult<Void> {
        setDefaultCallCount += 1
        lastSetDefaultId = id
        return setDefaultResult
    }

    func delete(id: String) async -> ApiResult<Void> {
        deleteCallCount += 1
        lastDeleteId = id
        return deleteResult
    }
}

final class StubGeocodingService: GeocodingService, @unchecked Sendable {
    func reverseGeocode(_: Coordinate) async -> GeocodedAddress? {
        nil
    }

    func forwardGeocode(query _: String, countryIsoCodes _: [String]) async -> [GeocodedAddress] {
        []
    }
}

enum SavedAddressFixtures {
    static func address(
        id: String,
        label: String = "Home",
        isDefault: Bool = false,
        latitude: Double? = 50.0,
        longitude: Double? = 14.0
    ) -> SavedAddress {
        SavedAddress(
            id: id,
            label: label,
            street: "Main 1",
            city: "Prague",
            zipCode: "11000",
            country: "Czechia",
            latitude: latitude,
            longitude: longitude,
            isDefault: isDefault
        )
    }

    static func geocoded(
        street: String = "Main 1",
        city: String = "Prague",
        zipCode: String = "11000",
        latitude: Double = 50.0,
        longitude: Double = 14.0
    ) -> GeocodedAddress {
        GeocodedAddress(
            latitude: latitude,
            longitude: longitude,
            street: street,
            city: city,
            zipCode: zipCode,
            country: "Czechia",
            countryIsoCode: "cz",
            formatted: "\(street), \(city)"
        )
    }
}
