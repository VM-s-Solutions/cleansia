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

    func testMarkerViewResolvesTheTintInBothSchemes() {
        let marker = CleansiaMarkerAnnotationView(annotation: nil, reuseIdentifier: nil)
        XCTAssertEqual(hex(of: marker.markerTintColor, in: .light), CleansiaMapMarker.tint.light)
        XCTAssertEqual(hex(of: marker.markerTintColor, in: .dark), CleansiaMapMarker.tint.dark)
    }

    func testGlyphIsTheWhiteMapPinSymbol() {
        let marker = CleansiaMarkerAnnotationView(annotation: nil, reuseIdentifier: nil)
        // MapKit re-renders the glyph as a template, so normalise both sides before comparing.
        XCTAssertEqual(
            marker.glyphImage?.withRenderingMode(.alwaysTemplate),
            UIImage(systemName: "mappin")?.withRenderingMode(.alwaysTemplate)
        )
        XCTAssertEqual(hex(of: marker.glyphTintColor, in: .light), 0xFFFFFF)
        XCTAssertEqual(hex(of: marker.glyphTintColor, in: .dark), 0xFFFFFF)
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
        let marker = try XCTUnwrap(view as? CleansiaMarkerAnnotationView, "the stock red pin is what nil returns")
        XCTAssertEqual(hex(of: marker.markerTintColor, in: .light), CleansiaMapMarker.tint.light)
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
