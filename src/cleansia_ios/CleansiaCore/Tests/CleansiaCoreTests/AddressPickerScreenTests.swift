import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCore

/// The picker hands a confirmed address to the caller's review step when it has one, and the map it returns to
/// still holds that address; with no review step the address goes straight to the caller.
@MainActor
final class AddressPickerScreenTests: XCTestCase {
    private static let picked = GeocodedAddress(
        latitude: 50.0755,
        longitude: 14.4378,
        street: "Vinohradská 12",
        city: "Praha",
        zipCode: "120 00",
        country: "Czechia",
        countryIsoCode: "cz",
        formatted: "Vinohradská 12, Praha"
    )

    override static func setUp() {
        super.setUp()
        ApplicationAccessibility.enable()
    }

    func testAReviewStepTakesTheConfirmedAddressAndTheMapKeepsItOnTheWayBack() async throws {
        var confirmed: [GeocodedAddress] = []
        var reviewing: GeocodedAddress?
        var back: (() -> Void)?
        let canvas = HostedCanvas(size: CGSize(width: 393, height: 852)) {
            AddressPickerScreen(
                geocoding: FixedGeocoder(),
                mapProvider: PreviewMapProvider(),
                serviceArea: nil,
                copy: TestCopy.self,
                review: { address, goBack in
                    reviewing = address
                    back = goBack
                    return Text(verbatim: "Review")
                },
                onConfirmed: { confirmed.append($0) },
                onBack: {}
            )
            .environment(\.locationProvider, GrantedLocation())
        }
        defer { canvas.close() }

        try await confirm(on: canvas)
        await canvas.settle(until: { self.element("Review", in: canvas) != nil })

        XCTAssertEqual(reviewing, Self.picked)
        XCTAssertTrue(confirmed.isEmpty, "the review step, not the caller, takes the confirmed address")
        XCTAssertNil(element(TestCopy.confirm, in: canvas))

        try XCTUnwrap(back)()
        await canvas.settle(until: { self.element(TestCopy.confirm, in: canvas) != nil })

        XCTAssertNil(element("Review", in: canvas))
        XCTAssertNotNil(element(Self.picked.street, in: canvas), "the map comes back with the address it confirmed")
        XCTAssertEqual(element(TestCopy.confirm, in: canvas)?.accessibilityTraits.contains(.notEnabled), false)
    }

    func testWithNoReviewStepTheConfirmedAddressGoesToTheCaller() async throws {
        var confirmed: [GeocodedAddress] = []
        let canvas = HostedCanvas(size: CGSize(width: 393, height: 852)) {
            AddressPickerScreen(
                geocoding: FixedGeocoder(),
                mapProvider: PreviewMapProvider(),
                serviceArea: nil,
                copy: TestCopy.self,
                onConfirmed: { confirmed.append($0) },
                onBack: {}
            )
            .environment(\.locationProvider, GrantedLocation())
        }
        defer { canvas.close() }

        try await confirm(on: canvas)
        await canvas.settle(until: { !confirmed.isEmpty })

        XCTAssertEqual(confirmed, [Self.picked])
    }

    /// Waits for the lookup the location fix starts to land, then confirms through the card's button.
    private func confirm(on canvas: HostedCanvas) async throws {
        await canvas.settle(until: {
            self.element(TestCopy.confirm, in: canvas)?.accessibilityTraits.contains(.notEnabled) == false
        })
        let button = try XCTUnwrap(element(TestCopy.confirm, in: canvas))
        XCTAssertFalse(button.accessibilityTraits.contains(.notEnabled), "the lookup never landed")
        button.accessibilityActivate()
    }

    private func element(_ label: String, in canvas: HostedCanvas) -> NSObject? {
        Self.elements(under: canvas.window).first { $0.accessibilityLabel == label }
    }

    private static func elements(under view: NSObject, depth: Int = 0) -> [NSObject] {
        guard depth < 60 else { return [] }
        if view.isAccessibilityElement { return [view] }
        if let children = view.accessibilityElements as? [NSObject], !children.isEmpty {
            return children.flatMap { elements(under: $0, depth: depth + 1) }
        }
        let count = view.accessibilityElementCount()
        if count != NSNotFound, count > 0 {
            return (0 ..< count).compactMap { view.accessibilityElement(at: $0) as? NSObject }
                .flatMap { elements(under: $0, depth: depth + 1) }
        }
        return ((view as? UIView)?.subviews ?? []).flatMap { elements(under: $0, depth: depth + 1) }
    }
}

private enum TestCopy: AddressPickerCopy {
    static let back = "Back"
    static let searchHint = "Search"
    static let clear = "Clear"
    static let searching = "Searching"
    static let noResults = "No results"
    static let myLocation = "My location"
    static let locationDenied = "Denied"
    static let locationUnavailable = "Unavailable"
    static let lookingUp = "Looking up"
    static let dragToPick = "Drag to pick"
    static let confirm = "Use this address"
}

private final class FixedGeocoder: GeocodingService {
    func reverseGeocode(_: Coordinate) async -> GeocodedAddress? {
        GeocodedAddress(
            latitude: 50.0755,
            longitude: 14.4378,
            street: "Vinohradská 12",
            city: "Praha",
            zipCode: "120 00",
            country: "Czechia",
            countryIsoCode: "cz",
            formatted: "Vinohradská 12, Praha"
        )
    }

    func forwardGeocode(query _: String, countryIsoCodes _: [String]) async -> [GeocodedAddress] {
        []
    }
}

@MainActor
private final class GrantedLocation: LocationProvider {
    var authorizationStatus: LocationAuthorizationStatus {
        .authorized
    }

    func requestWhenInUseAuthorization() async -> LocationAuthorizationStatus {
        .authorized
    }

    func currentLocation() async -> Coordinate? {
        Coordinate(latitude: 50.08, longitude: 14.42)
    }
}
