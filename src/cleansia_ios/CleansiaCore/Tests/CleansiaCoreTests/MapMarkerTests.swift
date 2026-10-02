import MapKit
import SwiftUI
import XCTest
@testable import CleansiaCore

final class CleansiaMapMarkerTests: XCTestCase {
    func testTintIsTheAndroidPrimaryPair() {
        XCTAssertEqual(CleansiaMapMarker.tint.light, 0x0284C7)
        XCTAssertEqual(CleansiaMapMarker.tint.dark, 0x38BDF8)
    }

    /// Android tints the pin with `colorScheme.primary` (Sky600 light / Sky400 dark), the same
    /// sky ramp the brand-blue gradient runs across — so a brand move that misses the marker
    /// shows up here instead of only on a device.
    func testTintTracksTheBrandBlueRamp() {
        XCTAssertEqual(
            BrandGradient.blue.stops.map(\.light),
            [CleansiaMapMarker.tint.light, CleansiaMapMarker.tint.dark]
        )
    }

    func testThePinIsFilledWithTheTintInBothSchemes() {
        XCTAssertEqual(hex(of: CleansiaMapMarker.fillColor(dark: false), in: .light), CleansiaMapMarker.tint.light)
        XCTAssertEqual(hex(of: CleansiaMapMarker.fillColor(dark: true), in: .dark), CleansiaMapMarker.tint.dark)
    }

    /// One drawing on every map, the Android `CleansiaMapPin`: a white house on the brand sky.
    func testTheGlyphIsTheWhiteHouse() {
        XCTAssertEqual(CleansiaMapMarker.glyphSymbolName, "house.fill")
        XCTAssertNotNil(UIImage(systemName: CleansiaMapMarker.glyphSymbolName))
        XCTAssertEqual(hex(of: CleansiaMapMarker.glyphColor, in: .light), 0xFFFFFF)
    }

    func testThePinIsTheSharedSizeAndDrawnPerScheme() {
        let light = CleansiaMapMarker.pinImage(for: UITraitCollection(userInterfaceStyle: .light))
        let dark = CleansiaMapMarker.pinImage(for: UITraitCollection(userInterfaceStyle: .dark))
        XCTAssertEqual(CleansiaMapMarker.pinSize, CGSize(width: 40, height: 50))
        XCTAssertEqual(light.size, CleansiaMapMarker.pinSize)
        XCTAssertEqual(dark.size, CleansiaMapMarker.pinSize)
        XCTAssertNotEqual(light.pngData(), dark.pngData())
    }

    /// The tip is the bottom-centre of the bounds — what the annotation offset and the picker's
    /// constraints both assume.
    func testTheTeardropsTipIsTheBottomCentre() {
        let rect = CGRect(origin: .zero, size: CleansiaMapMarker.pinSize)
        let path = CleansiaMapMarker.teardrop(in: rect)
        XCTAssertEqual(path.bounds.maxY, rect.maxY, accuracy: 0.01)
        XCTAssertEqual(path.bounds.midX, rect.midX, accuracy: 0.01)
        XCTAssertEqual(path.bounds.width, rect.width, accuracy: 0.01)
        XCTAssertTrue(path.contains(CGPoint(x: rect.midX, y: rect.maxY - 2)))
        XCTAssertFalse(path.contains(CGPoint(x: rect.minX + 1, y: rect.maxY - 1)))
    }
}

final class FullBleedMarkerTests: XCTestCase {
    private let prague = Coordinate(latitude: 50.0755, longitude: 14.4378)

    @MainActor
    func testApplyInstallsTheSharedMarkerDelegate() {
        let mapView = MKMapView()
        FullBleedOrderMap(coordinate: prague).apply(to: mapView)
        XCTAssertTrue(mapView.delegate === CleansiaMapMarker.delegate)
    }

    @MainActor
    func testDelegateVendsTheBrandMarkerForTheAddressPin() throws {
        let mapView = MKMapView()
        FullBleedOrderMap(coordinate: prague).apply(to: mapView)
        let pin = try XCTUnwrap(mapView.annotations.compactMap { $0 as? MKPointAnnotation }.first)

        let view = CleansiaMapMarker.delegate.mapView(mapView, viewFor: pin)
        let marker = try XCTUnwrap(view as? CleansiaPinAnnotationView, "the stock red pin is what nil returns")
        XCTAssertEqual(marker.image?.size, CleansiaMapMarker.pinSize)
        XCTAssertEqual(marker.centerOffset.y, -CleansiaMapMarker.pinSize.height / 2, "the tip sits on the coordinate")
    }

    @MainActor
    func testDelegateLeavesTheUserLocationDotToMapKit() {
        let mapView = MKMapView()
        XCTAssertNil(CleansiaMapMarker.delegate.mapView(mapView, viewFor: MKUserLocation()))
    }
}

/// A newly added map surface that hand-rolls its own annotation instead of the shared marker is
/// the failure this catches — enumerating today's two surfaces by hand would not.
enum MapAnnotationConfinement {
    static let annotationTokens = [
        "MKAnnotationView",
        "MKMarkerAnnotationView",
        "MKPinAnnotationView",
        "MapMarker(",
        "MapPin(",
        "MapAnnotation("
    ]

    static func violations(in sources: [String: String]) -> [String] {
        sources
            .filter { _, source in
                annotationTokens.contains(where: source.contains) && !source.contains("CleansiaMapMarker")
            }
            .keys
            .sorted()
    }

    /// SwiftUI's `MapMarker` exposes nothing to assert at runtime, so its tint is only observable
    /// as the token the call site names.
    static func untintedMarkers(in sources: [String: String]) -> [String] {
        sources
            .filter { _, source in
                source.contains("MapMarker(") && !source.contains("tint: CleansiaMapMarker.tintColor")
            }
            .keys
            .sorted()
    }
}

final class MapAnnotationConfinementRuleTests: XCTestCase {
    func testRuleFlagsAMapSurfaceThatSkipsTheSharedMarker() {
        let sources = [
            "Features/Tracking/TrackingMap.swift": "MapMarker(coordinate: point)",
            "Location/CleansiaMapMarker.swift": "class M: MKMarkerAnnotationView { CleansiaMapMarker.tint }",
            "Features/Orders/OrderRow.swift": "Text(\"no map here\")"
        ]
        XCTAssertEqual(MapAnnotationConfinement.violations(in: sources), ["Features/Tracking/TrackingMap.swift"])
    }

    func testRuleReadsEveryAnnotationToken() {
        for token in MapAnnotationConfinement.annotationTokens {
            XCTAssertEqual(MapAnnotationConfinement.violations(in: ["A.swift": token]), ["A.swift"], token)
        }
    }

    func testRuleFlagsAMapMarkerLeftOnTheStockTint() {
        let sources = [
            "Stock.swift": "MapMarker(coordinate: point)",
            "Branded.swift": "MapMarker(coordinate: point, tint: CleansiaMapMarker.tintColor)"
        ]
        XCTAssertEqual(MapAnnotationConfinement.untintedMarkers(in: sources), ["Stock.swift"])
    }
}

/// Reads the shipped tree, so it is one of the suites the local sandbox denies (`~/Desktop` is
/// TCC-protected) and CI is where it gates — `MapAnnotationConfinementRuleTests` covers the rule
/// itself where it can run.
final class MapAnnotationConfinementTests: XCTestCase {
    /// The two known map files are read by name so a denied or empty walk fails as a read error
    /// instead of passing vacuously; the walk is what covers files nobody has written yet.
    private static let knownMapSources = [
        "CleansiaCore/Sources/CleansiaCore/Location/CleansiaMapMarker.swift",
        "CleansiaCore/Sources/CleansiaCore/Location/MapKitMapProvider.swift"
    ]

    func testNoShippedSourceBuildsAnAnnotationWithoutTheSharedMarker() throws {
        let root = iosRoot()
        var sources: [String: String] = [:]
        for path in Self.knownMapSources {
            sources[path] = try String(contentsOf: root.appendingPathComponent(path), encoding: .utf8)
        }
        let walker = FileManager.default.enumerator(atPath: root.path)
        while let path = walker?.nextObject() as? String {
            if Self.prunedDirectories.contains(where: path.hasSuffix) {
                walker?.skipDescendants()
                continue
            }
            guard path.hasSuffix(".swift"), !Self.excludedPaths.contains(where: path.contains) else { continue }
            sources[path] = try String(contentsOf: root.appendingPathComponent(path), encoding: .utf8)
        }
        XCTAssertEqual(MapAnnotationConfinement.violations(in: sources), [])
        XCTAssertEqual(MapAnnotationConfinement.untintedMarkers(in: sources), [])
    }

    private static let prunedDirectories = [".build", "DerivedData", ".git", ".xcodeproj", "build"]
    private static let excludedPaths = ["/Tests/", "CleansiaPartnerApi/", "CleansiaCustomerApi/", "/Generated/"]

    private func iosRoot() -> URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
    }
}

private func hex(of color: UIColor?, in style: UIUserInterfaceStyle) -> UInt32? {
    guard let color else { return nil }
    let resolved = color.resolvedColor(with: UITraitCollection(userInterfaceStyle: style))
    var red: CGFloat = 0
    var green: CGFloat = 0
    var blue: CGFloat = 0
    var alpha: CGFloat = 0
    guard resolved.getRed(&red, green: &green, blue: &blue, alpha: &alpha) else { return nil }
    let channel = { (value: CGFloat) in UInt32((value * 255).rounded()) }
    return channel(red) << 16 | channel(green) << 8 | channel(blue)
}
