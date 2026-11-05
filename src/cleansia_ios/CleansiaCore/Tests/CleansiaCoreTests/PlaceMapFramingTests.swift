import MapKit
import SwiftUI
import XCTest
@testable import CleansiaCore

final class PlaceMapFramingTests: XCTestCase {
    /// MapKit fits a region inside the map's layout margins, so the region it is handed is chosen to draw the place
    /// where the whole map would, at the same scale.
    func testAMarginedMapDrawsThePlaceWhereTheWholeMapWould() {
        let place = Coordinate(latitude: 50.0755, longitude: 14.4378)
        let region = PlaceMap.region(for: place, metres: 700, anchor: 0.71)
        let size = CGSize(width: 393, height: 214)
        let margins = UIEdgeInsets(top: 0, left: 12, bottom: 32, right: 156)
        let inner = CGRect(x: 12, y: 0, width: 393 - 168, height: 214 - 32)

        let whole = Self.draw(region, in: CGRect(origin: .zero, size: size))
        let margined = Self.draw(PlaceMapCanvas.framed(region, in: size, margins: margins), in: inner)

        XCTAssertEqual(margined.scale, whole.scale, accuracy: whole.scale * 1e-6)
        let point = MKMapPoint(CLLocationCoordinate2D(latitude: place.latitude, longitude: place.longitude))
        XCTAssertEqual(margined.screen(point).x, whole.screen(point).x, accuracy: 0.01)
        XCTAssertEqual(margined.screen(point).y, whole.screen(point).y, accuracy: 0.01)
    }

    func testNoMarginsLeaveTheRegionAsItIs() {
        let region = PlaceMap.region(for: Coordinate(latitude: 50.0755, longitude: 14.4378), metres: 700, anchor: 0.5)
        let framed = PlaceMapCanvas.framed(region, in: CGSize(width: 393, height: 214), margins: .zero)
        let whole = Self.draw(region, in: CGRect(x: 0, y: 0, width: 393, height: 214))
        let same = Self.draw(framed, in: CGRect(x: 0, y: 0, width: 393, height: 214))
        XCTAssertEqual(same.scale, whole.scale, accuracy: whole.scale * 1e-6)
        XCTAssertEqual(same.centre.x, whole.centre.x, accuracy: 1)
        XCTAssertEqual(same.centre.y, whole.centre.y, accuracy: 1)
    }

    /// A region drawn as MapKit draws it into a rect: fitted whole, its centre on the rect's.
    private struct Drawn {
        let scale: Double
        let centre: MKMapPoint
        let rect: CGRect

        func screen(_ point: MKMapPoint) -> CGPoint {
            CGPoint(x: rect.midX + (point.x - centre.x) / scale, y: rect.midY + (point.y - centre.y) / scale)
        }
    }

    private static func draw(_ region: MKCoordinateRegion, in rect: CGRect) -> Drawn {
        let corner = MKMapPoint(CLLocationCoordinate2D(
            latitude: region.center.latitude + region.span.latitudeDelta / 2,
            longitude: region.center.longitude - region.span.longitudeDelta / 2
        ))
        let opposite = MKMapPoint(CLLocationCoordinate2D(
            latitude: region.center.latitude - region.span.latitudeDelta / 2,
            longitude: region.center.longitude + region.span.longitudeDelta / 2
        ))
        let width = opposite.x - corner.x
        let height = opposite.y - corner.y
        return Drawn(
            scale: max(width / Double(rect.width), height / Double(rect.height)),
            centre: MKMapPoint(x: corner.x + width / 2, y: corner.y + height / 2),
            rect: rect
        )
    }
}

@MainActor
final class PlaceMapMotionTests: XCTestCase {
    private let place = Coordinate(latitude: 50.0755, longitude: 14.4378)
    private let margins = EdgeInsets(top: 0, leading: 12, bottom: 300, trailing: 12)

    func testAStillMapTakesNoTouch() {
        let mapView = map().makeMapView(placement: PlaceMapView.Placement())
        XCTAssertFalse(mapView.isUserInteractionEnabled)
    }

    func testAMovableMapPansAndZoomsButNeverTurnsOrTilts() {
        let mapView = map(movedAway: .constant(false)).makeMapView(placement: PlaceMapView.Placement())
        XCTAssertTrue(mapView.isUserInteractionEnabled)
        XCTAssertTrue(mapView.isScrollEnabled)
        XCTAssertTrue(mapView.isZoomEnabled)
        XCTAssertFalse(mapView.isRotateEnabled)
        XCTAssertFalse(mapView.isPitchEnabled)
    }

    func testAPanOrAZoomOffThePlaceIsAMoveAndRecentringFramesItAgain() {
        let (mapView, window) = hosted(map(movedAway: .constant(false)))
        defer { window.isHidden = true }
        XCTAssertFalse(mapView.isAway, "framed on the place")

        let pin = mapView.convert(location, toPointTo: mapView)
        pan(mapView, by: CGVector(dx: -60, dy: 0))
        XCTAssertEqual(mapView.convert(location, toPointTo: mapView).x, pin.x - 60, accuracy: 1)
        XCTAssertTrue(mapView.isAway, "panned 60 off the place")
        mapView.recentre(animated: false)
        XCTAssertFalse(mapView.isAway)
        XCTAssertEqual(mapView.convert(location, toPointTo: mapView).x, pin.x, accuracy: 1)
        XCTAssertEqual(mapView.convert(location, toPointTo: mapView).y, pin.y, accuracy: 1)

        var zoomed = mapView.region
        zoomed.span.latitudeDelta *= 1.5
        zoomed.span.longitudeDelta *= 1.5
        mapView.setRegion(zoomed, animated: false)
        XCTAssertTrue(mapView.isAway, "zoomed out")
    }

    /// The panel's edge moves the logo and Legal with it: that is the map's margins, never her move.
    func testTheMarksRidingThePanelsEdgeAreNoMove() {
        let (mapView, window) = hosted(map(movedAway: .constant(false)))
        defer { window.isHidden = true }
        mapView.keepMarks(inside: NSDirectionalEdgeInsets(top: 0, leading: 12, bottom: 80, trailing: 12))
        mapView.layoutIfNeeded()
        XCTAssertFalse(mapView.isAway)
    }

    /// The page around the map settles by a few points while she looks: what she moved to stays, and a map she has
    /// not moved is framed again.
    func testANewSizeKeepsHerMoveAndFramesAMapSheLeftAlone() {
        let (mapView, window) = hosted(map(movedAway: .constant(false)))
        defer { window.isHidden = true }
        mapView.frame = CGRect(x: 0, y: 0, width: 676, height: 676)
        mapView.layoutIfNeeded()
        XCTAssertFalse(mapView.isAway, "framed again at the new size")

        pan(mapView, by: CGVector(dx: 0, dy: 70))
        mapView.keepsHerView = true
        let looking = mapView.centerCoordinate
        mapView.frame = CGRect(x: 0, y: 0, width: 680, height: 680)
        mapView.layoutIfNeeded()
        XCTAssertTrue(mapView.isAway)
        XCTAssertEqual(mapView.centerCoordinate.latitude, looking.latitude, accuracy: 1e-4)
        XCTAssertEqual(mapView.centerCoordinate.longitude, looking.longitude, accuracy: 1e-4)

        mapView.recentre()
        XCTAssertFalse(mapView.isAway, "framed at once at the size it has now")
        XCTAssertFalse(mapView.isRecentring)
    }

    func testANudgeTooSmallToSeeIsNoMove() {
        let framed = PlaceMapCanvas.Look(pin: CGPoint(x: 196, y: 152), metresPerPoint: 2)
        let near = PlaceMapCanvas.Look(pin: CGPoint(x: 196 + 5, y: 152 - 5), metresPerPoint: 2.06)
        XCTAssertFalse(PlaceMapCanvas.isAway(near, from: framed))
        XCTAssertTrue(PlaceMapCanvas.isAway(
            PlaceMapCanvas.Look(pin: CGPoint(x: 205, y: 152), metresPerPoint: 2),
            from: framed
        ))
        XCTAssertTrue(PlaceMapCanvas.isAway(
            PlaceMapCanvas.Look(pin: CGPoint(x: 196, y: 152), metresPerPoint: 2.2),
            from: framed
        ))
        XCTAssertTrue(PlaceMapCanvas.isAway(
            PlaceMapCanvas.Look(pin: CGPoint(x: 196, y: 152), metresPerPoint: 1.8),
            from: framed
        ))
    }

    /// She moves the map: the binding hears it once. The binding set back to false frames the place again.
    func testHerMoveIsWrittenToTheBindingAndClearingItRecentres() async {
        var away = false
        let binding = Binding(get: { away }, set: { away = $0 })
        let view = map(movedAway: binding)
        let placement = PlaceMapView.Placement()
        let (mapView, window) = hosted(view, placement: placement)
        defer { window.isHidden = true }

        pan(mapView, by: CGVector(dx: 0, dy: -80))
        placement.mapViewDidChangeVisibleRegion(mapView)
        await Task.yield()
        try? await Task.sleep(nanoseconds: 50_000_000)
        XCTAssertTrue(away, "her pan is written back")

        away = false
        XCTAssertTrue(mapView.isAway)
        view.apply(to: mapView, placement: placement)
        // iOS 16 animates the move back; iOS 26 finishes it inside `setRegion`, reporting the region change before
        // it returns. Either way the map is on its way back to the place, or already there.
        XCTAssertTrue(
            mapView.isRecentring || !mapView.isAway,
            "clearing the binding sends the map back to the place"
        )
    }

    func testTheStreetRidesOnThePinAsPartOfIt() throws {
        let placement = PlaceMapView.Placement()
        let mapView = map(label: "Zenklova 6").makeMapView(placement: placement)
        let annotation = try XCTUnwrap(mapView.annotations.first)
        let pin = try XCTUnwrap(placement.mapView(mapView, viewFor: annotation))
        let street = try XCTUnwrap(pin.subviews.first { $0 is PlaceMapStreetLabel })
        XCTAssertEqual(street.center.x, CleansiaMapMarker.pinSize.width / 2, accuracy: 0.5)
        XCTAssertEqual(street.center.y, CleansiaMapMarker.pinSize.height - PlaceMap.pinLift, accuracy: 0.5)
        XCTAssertGreaterThan(street.bounds.width, 0)

        let unlabelled = PlaceMapView.Placement()
        let plain = map().makeMapView(placement: unlabelled)
        let bare = try XCTUnwrap(unlabelled.mapView(plain, viewFor: XCTUnwrap(plain.annotations.first)))
        XCTAssertFalse(bare.subviews.contains { $0 is PlaceMapStreetLabel })
    }

    /// The drawing moved by `offset`, as her finger moves it: MapKit centres a coordinate inside the margins.
    private func pan(_ mapView: MKMapView, by offset: CGVector) {
        let centre = mapView.convert(mapView.centerCoordinate, toPointTo: mapView)
        let next = CGPoint(x: centre.x - offset.dx, y: centre.y - offset.dy)
        mapView.setCenter(mapView.convert(next, toCoordinateFrom: mapView), animated: false)
    }

    private var location: CLLocationCoordinate2D {
        CLLocationCoordinate2D(latitude: place.latitude, longitude: place.longitude)
    }

    private func map(label: String? = nil, movedAway: Binding<Bool>? = nil) -> PlaceMapView {
        PlaceMapView(
            coordinate: place,
            metres: 2500,
            anchor: 0.25,
            pinned: true,
            label: label,
            veiled: false,
            attributionInsets: margins,
            movedAway: movedAway
        )
    }

    private func hosted(
        _ view: PlaceMapView,
        placement: PlaceMapView.Placement = PlaceMapView.Placement()
    ) -> (PlaceMapCanvas, UIWindow) {
        let mapView = view.makeMapView(placement: placement)
        let host = UIViewController()
        host.view = mapView
        let window = UIWindow(frame: CGRect(x: 0, y: 0, width: 680, height: 680))
        window.rootViewController = host
        window.makeKeyAndVisible()
        mapView.layoutIfNeeded()
        return (mapView, window)
    }
}
