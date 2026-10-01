import MapKit
import SwiftUI
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

/// The one map look — a muted, flat standard map with no points of interest and no traffic — on the
/// order backdrop and the address picker alike (owner decision D7, option B).
final class CleansiaMapStyleTests: XCTestCase {
    private let prague = Coordinate(latitude: 50.0755, longitude: 14.4378)

    @MainActor
    func testTheStyleHidesEveryPointOfInterestOnAMutedFlatMap() throws {
        let mapView = MKMapView()
        CleansiaMapStyle.apply(to: mapView)
        try assertQuiet(mapView)
    }

    @MainActor
    func testTheOrderBackdropWearsTheStyle() throws {
        try assertQuiet(FullBleedOrderMap(coordinate: prague).makeMapView())
    }

    @MainActor
    func testThePickerWearsTheStyleAndCarriesTheCentrePin() throws {
        let region = MKCoordinateRegion(
            center: CLLocationCoordinate2D(latitude: prague.latitude, longitude: prague.longitude),
            span: MKCoordinateSpan(latitudeDelta: 0.01, longitudeDelta: 0.01)
        )
        let box = RegionBox(region)
        let coordinator = PickerMapView.Coordinator(region: box.binding)
        let mapView = PickerMapView.makeMapView(
            region: region,
            showsUserLocation: false,
            bottomInset: 0,
            coordinator: coordinator
        )

        try assertQuiet(mapView)
        XCTAssertTrue(mapView.delegate === coordinator)
        let pins = mapView.subviews.compactMap { $0 as? UIImageView }.filter {
            $0.image?.size == CleansiaMapMarker.pinSize
        }
        XCTAssertEqual(pins.count, 1, "one centre pin, drawn by the map itself")
    }

    /// The bottom margin lifts Apple's logo and "Legal" above what the screen lays over the map, and
    /// the map centres its region inside its margins — so the centre pin's tip has to land on the
    /// region's centre, the point being picked, in a window with the safe area a notched phone gives
    /// the full-bleed map. 278 is the location button and confirm card at the largest accessibility
    /// text size, measured on an iPhone 17 Pro Max.
    @MainActor
    func testTheCentrePinsTipIsThePickedPoint() throws {
        let region = MKCoordinateRegion(
            center: CLLocationCoordinate2D(latitude: prague.latitude, longitude: prague.longitude),
            span: MKCoordinateSpan(latitudeDelta: 0.01, longitudeDelta: 0.01)
        )
        let coordinator = PickerMapView.Coordinator(region: RegionBox(region).binding)
        let mapView = PickerMapView.makeMapView(
            region: region,
            showsUserLocation: false,
            bottomInset: 278,
            coordinator: coordinator
        )
        let window = Self.hostOnANotchedPhone(mapView)
        defer { window.isHidden = true }
        mapView.setRegion(region, animated: false)

        assertTheMarginsAreTheSafeAreaPlus(bottomInset: 278, on: mapView)
        try assertThePinsTipIsTheRegionCentre(of: mapView)
    }

    /// The screen measures its cover after the map is made, and again whenever the text size changes.
    /// A new inset moves the pin with the margins' centre, so the map has to bring the point being
    /// picked back under the pin's tip, or the card would name an address the pin no longer points at.
    @MainActor
    func testANewBottomInsetKeepsThePickedPointUnderThePin() throws {
        let picked = CLLocationCoordinate2D(latitude: prague.latitude, longitude: prague.longitude)
        let region = MKCoordinateRegion(
            center: picked,
            span: MKCoordinateSpan(latitudeDelta: 0.01, longitudeDelta: 0.01)
        )
        let coordinator = PickerMapView.Coordinator(region: RegionBox(region).binding)
        let mapView = PickerMapView.makeMapView(
            region: region,
            showsUserLocation: false,
            bottomInset: 0,
            coordinator: coordinator
        )
        let window = Self.hostOnANotchedPhone(mapView)
        defer { window.isHidden = true }
        mapView.setRegion(region, animated: false)

        // The card with an address, the same card at the largest text size, then the card while it
        // looks the address up (no second line).
        for inset: CGFloat in [206, 278, 186] {
            PickerMapView.apply(bottomInset: inset, to: mapView, coordinator: coordinator)
            mapView.layoutIfNeeded()

            assertTheMarginsAreTheSafeAreaPlus(bottomInset: inset, on: mapView)
            XCTAssertEqual(mapView.region.center.latitude, picked.latitude, accuracy: 0.000_01)
            XCTAssertEqual(mapView.region.center.longitude, picked.longitude, accuracy: 0.000_01)
            try assertThePinsTipIsTheRegionCentre(of: mapView)
        }
    }

    @MainActor
    private static func hostOnANotchedPhone(_ mapView: MKMapView) -> UIWindow {
        let host = UIViewController()
        host.view = mapView
        host.additionalSafeAreaInsets = UIEdgeInsets(top: 59, left: 0, bottom: 34, right: 0)
        let window = UIWindow(frame: CGRect(x: 0, y: 0, width: 390, height: 844))
        window.rootViewController = host
        window.makeKeyAndVisible()
        mapView.layoutIfNeeded()
        return window
    }

    /// The safe area and the screen's cover, nothing more: a top margin that kept a previous safe area
    /// would push the pin a little further down on every change.
    @MainActor
    private func assertTheMarginsAreTheSafeAreaPlus(
        bottomInset: CGFloat,
        on mapView: MKMapView,
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        XCTAssertEqual(mapView.layoutMargins.top, mapView.safeAreaInsets.top, accuracy: 0.5, file: file, line: line)
        XCTAssertEqual(
            mapView.layoutMargins.bottom,
            mapView.safeAreaInsets.bottom + bottomInset,
            accuracy: 0.5,
            file: file,
            line: line
        )
    }

    @MainActor
    private func assertThePinsTipIsTheRegionCentre(
        of mapView: MKMapView,
        file: StaticString = #filePath,
        line: UInt = #line
    ) throws {
        let pin = try XCTUnwrap(
            mapView.subviews.compactMap { $0 as? UIImageView }.first { $0.image?.size == CleansiaMapMarker.pinSize },
            file: file,
            line: line
        )
        let picked = mapView.convert(mapView.region.center, toPointTo: mapView)
        XCTAssertEqual(pin.frame.midX, picked.x, accuracy: 1, file: file, line: line)
        XCTAssertEqual(pin.frame.maxY, picked.y, accuracy: 1, file: file, line: line)
    }

    @MainActor
    func testADragWritesTheSettledRegionBackToTheBinding() {
        let start = MKCoordinateRegion(
            center: CLLocationCoordinate2D(latitude: 0, longitude: 0),
            span: MKCoordinateSpan(latitudeDelta: 1, longitudeDelta: 1)
        )
        let box = RegionBox(start)
        let coordinator = PickerMapView.Coordinator(region: box.binding)
        let mapView = MKMapView(frame: CGRect(x: 0, y: 0, width: 300, height: 300))
        mapView.setRegion(MKCoordinateRegion(
            center: CLLocationCoordinate2D(latitude: prague.latitude, longitude: prague.longitude),
            span: MKCoordinateSpan(latitudeDelta: 0.01, longitudeDelta: 0.01)
        ), animated: false)

        coordinator.mapView(mapView, regionDidChangeAnimated: false)

        XCTAssertEqual(box.region.center.latitude, mapView.region.center.latitude, accuracy: 0.000_001)
        XCTAssertFalse(
            PickerMapView.needsRegionUpdate(binding: box.region.center, reported: coordinator.reported),
            "the region the map just reported is not set back on it"
        )
    }

    func testOnlyARegionTheBindingMovedToIsAppliedToTheMap() {
        let here = CLLocationCoordinate2D(latitude: 50.0755, longitude: 14.4378)
        XCTAssertTrue(PickerMapView.needsRegionUpdate(binding: here, reported: nil))
        XCTAssertFalse(PickerMapView.needsRegionUpdate(binding: here, reported: here))
        let recentred = CLLocationCoordinate2D(latitude: 50.08, longitude: 14.42)
        XCTAssertTrue(PickerMapView.needsRegionUpdate(binding: recentred, reported: here))
    }

    @MainActor
    private func assertQuiet(_ mapView: MKMapView, file: StaticString = #filePath, line: UInt = #line) throws {
        let configuration = try XCTUnwrap(
            mapView.preferredConfiguration as? MKStandardMapConfiguration,
            file: file,
            line: line
        )
        XCTAssertEqual(configuration.emphasisStyle, .muted, file: file, line: line)
        XCTAssertEqual(configuration.elevationStyle, .flat, file: file, line: line)
        let filter = try XCTUnwrap(configuration.pointOfInterestFilter, file: file, line: line)
        for category in [MKPointOfInterestCategory.store, .publicTransport, .restaurant, .stadium, .museum] {
            XCTAssertTrue(filter.excludes(category), "\(category.rawValue) still shows", file: file, line: line)
        }
        XCTAssertFalse(configuration.showsTraffic, file: file, line: line)
    }
}

private final class RegionBox {
    var region: MKCoordinateRegion

    init(_ region: MKCoordinateRegion) {
        self.region = region
    }

    var binding: Binding<MKCoordinateRegion> {
        Binding(get: { self.region }, set: { self.region = $0 })
    }
}
