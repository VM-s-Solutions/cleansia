import MapKit
import SwiftUI
import XCTest
@testable import CleansiaCore

final class PlaceMapFramingTests: XCTestCase {
    /// MapKit fits a region inside the map's layout margins, so the region it is handed is chosen to draw the place
    /// where the whole map would, at the same scale.
    func testAMarginedMapDrawsThePlaceWhereTheWholeMapWould() throws {
        let place = Coordinate(latitude: 50.0755, longitude: 14.4378)
        let region = PlaceMap.region(for: place, metres: 700, anchor: 0.71)
        let size = CGSize(width: 393, height: 214)
        let margins = UIEdgeInsets(top: 0, left: 12, bottom: 32, right: 156)
        let inner = CGRect(x: 12, y: 0, width: 393 - 168, height: 214 - 32)

        let whole = Self.draw(region, in: CGRect(origin: .zero, size: size))
        let margined = try Self.draw(XCTUnwrap(PlaceMapCanvas.framed(region, in: size, margins: margins)), in: inner)

        XCTAssertEqual(margined.scale, whole.scale, accuracy: whole.scale * 1e-6)
        let point = MKMapPoint(CLLocationCoordinate2D(latitude: place.latitude, longitude: place.longitude))
        XCTAssertEqual(margined.screen(point).x, whole.screen(point).x, accuracy: 0.01)
        XCTAssertEqual(margined.screen(point).y, whole.screen(point).y, accuracy: 0.01)
    }

    func testNoMarginsLeaveTheRegionAsItIs() throws {
        let region = PlaceMap.region(for: Coordinate(latitude: 50.0755, longitude: 14.4378), metres: 700, anchor: 0.5)
        let framed = try XCTUnwrap(PlaceMapCanvas.framed(region, in: CGSize(width: 393, height: 214), margins: .zero))
        let whole = Self.draw(region, in: CGRect(x: 0, y: 0, width: 393, height: 214))
        let same = Self.draw(framed, in: CGRect(x: 0, y: 0, width: 393, height: 214))
        XCTAssertEqual(same.scale, whole.scale, accuracy: whole.scale * 1e-6)
        XCTAssertEqual(same.centre.x, whole.centre.x, accuracy: 1)
        XCTAssertEqual(same.centre.y, whole.centre.y, accuracy: 1)
    }

    /// A sheet closing over the job can lay the map out shorter than the panel's edge it is framed above: no room is
    /// left inside the margins, so there is nothing to frame.
    func testMarginsTallerThanTheMapLeaveNothingToFrame() {
        let region = PlaceMap.region(for: Coordinate(latitude: 50.0755, longitude: 14.4378), metres: 700, anchor: 0.71)
        let margins = UIEdgeInsets(top: 0, left: 12, bottom: 600, right: 156)
        XCTAssertNil(PlaceMapCanvas.framed(region, in: CGSize(width: 393, height: 214), margins: margins))
    }

    /// Whatever the map's size and margins, framing gives a region MapKit draws, or none.
    func testEveryFramedRegionIsOneMapKitDraws() {
        let place = Coordinate(latitude: 50.0755, longitude: 14.4378)
        let lengths: [CGFloat] = [0, 0.001, 1, 10, 100, 214, 393, 600, 900]
        let insets: [CGFloat] = [0, 8, 100, 300, 600, 900, 1200]
        let sizes = lengths.flatMap { width in lengths.map { CGSize(width: width, height: $0) } }
        let margins = insets.flatMap { vertical in
            insets.map { UIEdgeInsets(top: vertical / 4, left: $0 / 4, bottom: vertical, right: $0) }
        }
        var rejected: [String] = []
        for metres in [700.0, 6000] {
            let region = PlaceMap.region(for: place, metres: metres, anchor: 0.71)
            for size in sizes {
                for inset in margins {
                    let framed: MKCoordinateRegion? = PlaceMapCanvas.framed(region, in: size, margins: inset)
                    if let framed, !Self.mapKitDraws(framed) {
                        rejected.append("\(size) \(inset) -> \(framed.center) \(framed.span)")
                    }
                }
            }
        }
        XCTAssertTrue(rejected.isEmpty, "\(rejected.count) regions MapKit throws on, among them \(rejected.prefix(3))")
    }

    /// MapKit's `setRegion` throws "Invalid Region" on any other.
    private static func mapKitDraws(_ region: MKCoordinateRegion) -> Bool {
        (-90 ... 90).contains(region.center.latitude) && (-180 ... 180).contains(region.center.longitude)
            && region.span.latitudeDelta > 0 && region.span.latitudeDelta <= 180
            && region.span.longitudeDelta > 0 && region.span.longitudeDelta <= 360
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
    private let anchor: CGFloat = 0.25

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

    /// The job map is keyed on its height, so a page that grows or shrinks re-keys the map and resizes it, in either
    /// order. MapKit reports the drawing at the new size before the map frames the place at it: a map she left alone
    /// is framed at the new size, and none of that is her move.
    func testANewSizeFramesAMapSheLeftAloneReKeyedFirst() async {
        var away = false
        let binding = Binding(get: { away }, set: { away = $0 })
        let placement = PlaceMapView.Placement()
        let (mapView, window) = hosted(map(movedAway: binding), placement: placement)
        defer { window.isHidden = true }

        map(metres: 2500 * 380 / 680, movedAway: binding).apply(to: mapView, placement: placement)
        mapView.frame = CGRect(x: 0, y: 0, width: 680, height: 380)
        mapView.layoutIfNeeded()
        try? await Task.sleep(nanoseconds: 100_000_000)
        assertFramed(mapView)
        XCTAssertFalse(mapView.keepsHerView)
        XCTAssertFalse(away)
    }

    func testANewSizeFramesAMapSheLeftAloneResizedFirst() async {
        var away = false
        let binding = Binding(get: { away }, set: { away = $0 })
        let placement = PlaceMapView.Placement()
        let (mapView, window) = hosted(map(movedAway: binding), placement: placement)
        defer { window.isHidden = true }

        mapView.frame = CGRect(x: 0, y: 0, width: 680, height: 580)
        mapView.layoutIfNeeded()
        try? await Task.sleep(nanoseconds: 100_000_000)
        assertFramed(mapView)
        XCTAssertFalse(mapView.keepsHerView)
        XCTAssertFalse(away)

        map(metres: 2500 * 580 / 680, movedAway: binding).apply(to: mapView, placement: placement)
        mapView.layoutIfNeeded()
        try? await Task.sleep(nanoseconds: 100_000_000)
        assertFramed(mapView)
        XCTAssertFalse(mapView.keepsHerView)
        XCTAssertFalse(away)
    }

    /// What she moved the map to stays at a new size, and recentring then frames the place at once at that size.
    func testANewSizeKeepsTheViewSheMovedTo() async {
        var away = false
        let binding = Binding(get: { away }, set: { away = $0 })
        let placement = PlaceMapView.Placement()
        let (mapView, window) = hosted(map(movedAway: binding), placement: placement)
        defer { window.isHidden = true }

        pan(mapView, by: CGVector(dx: 0, dy: 70))
        placement.mapViewDidChangeVisibleRegion(mapView)
        let looking = mapView.centerCoordinate
        mapView.frame = CGRect(x: 0, y: 0, width: 680, height: 580)
        mapView.layoutIfNeeded()
        try? await Task.sleep(nanoseconds: 100_000_000)
        XCTAssertTrue(mapView.isAway)
        XCTAssertTrue(mapView.keepsHerView)
        XCTAssertTrue(away)
        XCTAssertEqual(mapView.centerCoordinate.latitude, looking.latitude, accuracy: 1e-4)
        XCTAssertEqual(mapView.centerCoordinate.longitude, looking.longitude, accuracy: 1e-4)

        mapView.recentre()
        XCTAssertFalse(mapView.isAway, "framed at once at the size it has now")
        XCTAssertFalse(mapView.isRecentring)
    }

    func testAStillMapLaidOutShorterThanItsMarginsFramesThePlaceOnceItHasRoom() {
        waitsForRoom(map())
    }

    func testAMovableMapLaidOutShorterThanItsMarginsFramesThePlaceOnceItHasRoom() {
        waitsForRoom(map(movedAway: .constant(false)))
    }

    /// Recentred while the panel's edge stands over the whole map, at the size it framed the place at or at a new one,
    /// the map waits, and frames the place once the edge comes down.
    func testRecentringAMapWithNoRoomFramesThePlaceOnceTheEdgeComesDown() {
        let (mapView, window) = hosted(map(movedAway: .constant(false)))
        defer { window.isHidden = true }
        let low = NSDirectionalEdgeInsets(top: 0, leading: 12, bottom: 80, trailing: 12)
        let over = NSDirectionalEdgeInsets(top: 0, leading: 12, bottom: 300, trailing: 12)
        mapView.frame = CGRect(x: 0, y: 0, width: 680, height: 200)
        mapView.keepMarks(inside: low)
        mapView.layoutIfNeeded()
        assertFramed(mapView)

        pan(mapView, by: CGVector(dx: 0, dy: 40))
        mapView.keepsHerView = true
        mapView.keepMarks(inside: over)
        mapView.layoutIfNeeded()
        mapView.recentre()
        XCTAssertFalse(mapView.isRecentring)
        mapView.keepMarks(inside: low)
        mapView.layoutIfNeeded()
        assertFramed(mapView)

        pan(mapView, by: CGVector(dx: 0, dy: 40))
        mapView.keepsHerView = true
        mapView.keepMarks(inside: over)
        mapView.frame = CGRect(x: 0, y: 0, width: 680, height: 210)
        mapView.layoutIfNeeded()
        mapView.recentre()
        mapView.keepMarks(inside: low)
        mapView.layoutIfNeeded()
        assertFramed(mapView)
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

    /// A sheet closing over the job lays the map out shorter than the panel's edge before the edge follows. The map
    /// frames nothing then and reads nothing MapKit draws as her move; it frames the place once the edge comes down,
    /// and once a new size gives it room.
    private func waitsForRoom(_ view: PlaceMapView) {
        let placement = PlaceMapView.Placement()
        let (mapView, window) = hosted(view, placement: placement)
        defer { window.isHidden = true }

        mapView.frame = CGRect(x: 0, y: 0, width: 680, height: 200)
        mapView.layoutIfNeeded()
        placement.mapViewDidChangeVisibleRegion(mapView)
        XCTAssertFalse(mapView.keepsHerView)
        mapView.keepMarks(inside: NSDirectionalEdgeInsets(top: 0, leading: 12, bottom: 80, trailing: 12))
        mapView.layoutIfNeeded()
        assertFramed(mapView)

        mapView.keepMarks(inside: NSDirectionalEdgeInsets(top: 0, leading: 12, bottom: 300, trailing: 12))
        mapView.frame = CGRect(x: 0, y: 0, width: 680, height: 190)
        mapView.layoutIfNeeded()
        mapView.frame = CGRect(x: 0, y: 0, width: 680, height: 600)
        mapView.layoutIfNeeded()
        assertFramed(mapView)
    }

    /// The place where the map's region puts it on a map wider than tall: centred across, `anchor` of the way down.
    private func assertFramed(_ mapView: PlaceMapCanvas, line: UInt = #line) {
        let pin = mapView.convert(location, toPointTo: mapView)
        XCTAssertEqual(pin.x, mapView.bounds.midX, accuracy: 1, line: line)
        XCTAssertEqual(pin.y, mapView.bounds.height * anchor, accuracy: 1, line: line)
        XCTAssertFalse(mapView.isAway, line: line)
    }

    private var location: CLLocationCoordinate2D {
        CLLocationCoordinate2D(latitude: place.latitude, longitude: place.longitude)
    }

    private func map(
        label: String? = nil,
        metres: CLLocationDistance = 2500,
        movedAway: Binding<Bool>? = nil
    ) -> PlaceMapView {
        PlaceMapView(
            coordinate: place,
            metres: metres,
            anchor: anchor,
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
