import MapKit
import XCTest
@testable import CleansiaCore

/// T-0307 Slice A — the additive `fullBleedMap(coordinate:)` (ADR-0014 D6′):
/// the `MKMapView` representable centers on the coordinate-derived region, adds
/// EXACTLY ONE pin, and applies the bottom inset (the pin in the visible upper
/// sliver — the `MapBackdrop` camera-padding parity).
final class MapKitMapProviderFullBleedTests: XCTestCase {
    private let prague = Coordinate(latitude: 50.0755, longitude: 14.4378)

    @MainActor
    func testProviderReturnsAFullBleedView() {
        let view = MapKitMapProvider().fullBleedMap(coordinate: prague)
        XCTAssertNotNil(view)
    }

    @MainActor
    func testApplyAddsExactlyOneAnnotationAtTheCoordinate() {
        let mapView = MKMapView()
        FullBleedOrderMap(coordinate: prague).apply(to: mapView)

        let pins = mapView.annotations.compactMap { $0 as? MKPointAnnotation }
        XCTAssertEqual(pins.count, 1)
        XCTAssertEqual(pins[0].coordinate.latitude, prague.latitude, accuracy: 0.0001)
        XCTAssertEqual(pins[0].coordinate.longitude, prague.longitude, accuracy: 0.0001)
    }

    @MainActor
    func testApplyIsIdempotentNeverStacksPins() {
        let mapView = MKMapView()
        let map = FullBleedOrderMap(coordinate: prague)
        map.apply(to: mapView)
        map.apply(to: mapView)
        map.apply(to: mapView)

        let pins = mapView.annotations.compactMap { $0 as? MKPointAnnotation }
        XCTAssertEqual(pins.count, 1)
    }

    func testRegionLongitudeMatchesCoordinate() {
        let region = FullBleedMapGeometry.region(for: prague)
        XCTAssertEqual(region.center.longitude, prague.longitude, accuracy: 0.0001)
    }

    func testRegionCenterIsShiftedSouthSoPinSitsAboveCenter() {
        // The bottom inset pushes the region center SOUTH of the pin so the
        // actual coordinate renders in the upper, sheet-uncovered portion.
        let region = FullBleedMapGeometry.region(for: prague)
        XCTAssertLessThan(region.center.latitude, prague.latitude)

        let expectedShift = FullBleedMapGeometry.spanDelta * FullBleedMapGeometry.bottomCoverFraction / 2
        XCTAssertEqual(region.center.latitude, prague.latitude - expectedShift, accuracy: 0.00001)
    }

    func testRegionSpanIsTheConfiguredDelta() {
        let region = FullBleedMapGeometry.region(for: prague)
        XCTAssertEqual(region.span.latitudeDelta, FullBleedMapGeometry.spanDelta, accuracy: 0.00001)
        XCTAssertEqual(region.span.longitudeDelta, FullBleedMapGeometry.spanDelta, accuracy: 0.00001)
    }

    @MainActor
    func testApplyPinsTheExactCoordinateNotTheShiftedRegionCenter() throws {
        // The pin sits on the true coordinate; only the region center is shifted
        // south for the bottom inset — the pin must NOT move with it.
        let mapView = MKMapView()
        FullBleedOrderMap(coordinate: prague).apply(to: mapView)

        let pin = try XCTUnwrap(mapView.annotations.compactMap { $0 as? MKPointAnnotation }.first)
        XCTAssertEqual(pin.coordinate.latitude, prague.latitude, accuracy: 0.00001)
        XCTAssertGreaterThan(prague.latitude, FullBleedMapGeometry.region(for: prague).center.latitude)
    }
}
