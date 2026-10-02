import MapKit
import SwiftUI

public struct MapKitMapProvider: MapProvider {
    public init() {}

    /// An `MKMapView` rather than SwiftUI's `Map`: the iOS-16 `Map(coordinateRegion:)` takes no
    /// configuration, so it could not hide Apple's points of interest on the floor this app supports.
    public func pickerMap(
        region: Binding<MKCoordinateRegion>,
        showsUserLocation: Bool,
        bottomInset: CGFloat
    ) -> AnyView {
        AnyView(PickerMapView(region: region, showsUserLocation: showsUserLocation, bottomInset: bottomInset))
    }

    /// Measured, so the map can tell how much of it a `SnapSheet` covers: the sheet publishes its top
    /// edge in the backdrop's coordinates, and the map's height is the rest of that sum.
    public func fullBleedMap(coordinate: Coordinate) -> AnyView {
        AnyView(GeometryReader { proxy in
            FullBleedOrderMap(coordinate: coordinate, height: proxy.size.height)
        })
    }
}

/// The one map look on every map in both apps: a muted, flat standard map with every point of
/// interest hidden and no traffic, so the only marker on it is the Cleansia pin. Street and place
/// names stay, so the customer can still find their street. Android's twin is `CleansiaMapStyle`
/// (Mapbox Standard, faded, POI and transit labels off); neither platform can recolour the other's
/// tiles, so parity is no POIs, a muted base and the same pin. → /mobile-app/overview
enum CleansiaMapStyle {
    static func apply(to mapView: MKMapView) {
        let configuration = MKStandardMapConfiguration(elevationStyle: .flat, emphasisStyle: .muted)
        configuration.pointOfInterestFilter = .excludingAll
        configuration.showsTraffic = false
        mapView.preferredConfiguration = configuration
        mapView.isRotateEnabled = false
        mapView.isPitchEnabled = false
    }
}

/// The address picker's map. The region is a two-way binding: a drag writes the settled region back
/// once it ends, and a region the binding moves to (recentre, open on the saved address) is applied
/// only when it differs from what the map last reported — otherwise every drag would set the region
/// it had just reported and fight the finger.
struct PickerMapView: UIViewRepresentable {
    @Binding var region: MKCoordinateRegion
    let showsUserLocation: Bool
    let bottomInset: CGFloat

    func makeCoordinator() -> Coordinator {
        Coordinator(region: $region)
    }

    func makeUIView(context: Context) -> MKMapView {
        Self.makeMapView(
            region: region,
            showsUserLocation: showsUserLocation,
            bottomInset: bottomInset,
            coordinator: context.coordinator
        )
    }

    static func makeMapView(
        region: MKCoordinateRegion,
        showsUserLocation: Bool,
        bottomInset: CGFloat,
        coordinator: Coordinator
    ) -> MKMapView {
        let mapView = MKMapView()
        CleansiaMapStyle.apply(to: mapView)
        mapView.showsUserLocation = showsUserLocation
        mapView.delegate = coordinator
        // Apple's logo and "Legal" link sit inside the bottom margin; both pickers lay their location
        // button and confirm card over the map's bottom edge, which would hide them.
        mapView.layoutMargins = margins(bottomInset: bottomInset)
        coordinator.bottomInset = bottomInset
        addCenterPin(to: mapView)
        coordinator.reported = region.center
        mapView.setRegion(region, animated: false)
        return mapView
    }

    func updateUIView(_ mapView: MKMapView, context: Context) {
        context.coordinator.region = $region
        mapView.showsUserLocation = showsUserLocation
        Self.apply(bottomInset: bottomInset, to: mapView, coordinator: context.coordinator)
        guard Self.needsRegionUpdate(binding: region.center, reported: context.coordinator.reported) else { return }
        context.coordinator.reported = region.center
        mapView.setRegion(region, animated: true)
    }

    /// The screen's measurement changes with the text size. A new inset moves the margins' centre, and
    /// the pin with it, so the point being picked is set back under the pin's tip.
    static func apply(bottomInset: CGFloat, to mapView: MKMapView, coordinator: Coordinator) {
        guard bottomInset != coordinator.bottomInset else { return }
        coordinator.bottomInset = bottomInset
        let picked = coordinator.reported ?? mapView.centerCoordinate
        mapView.layoutMargins = margins(bottomInset: bottomInset)
        mapView.setCenter(picked, animated: false)
    }

    /// Set whole, never `layoutMargins.bottom = …`: the getter adds the safe area, so writing one edge
    /// back also writes the safe area into the others, and the top margin grows on every change.
    static func margins(bottomInset: CGFloat) -> UIEdgeInsets {
        UIEdgeInsets(top: 0, left: 0, bottom: bottomInset, right: 0)
    }

    /// Close enough to be the same point — a metre's tenth — so the region the map just reported is
    /// never set back on it.
    static func needsRegionUpdate(binding: CLLocationCoordinate2D, reported: CLLocationCoordinate2D?) -> Bool {
        guard let reported else { return true }
        let epsilon = 0.000_001
        return abs(binding.latitude - reported.latitude) > epsilon
            || abs(binding.longitude - reported.longitude) > epsilon
    }

    /// The pin points at the map's own centre — the coordinate being picked — with its tip, so it is
    /// pinned to the map view rather than laid over it by the screen, whose safe area is not the map's.
    /// The map centres its region inside its layout margins, which the attribution margin moves, so the
    /// pin follows the margins' centre, not the bounds'.
    static func addCenterPin(to mapView: MKMapView) {
        let pin = UIImageView(image: CleansiaMapMarker.pinImage(for: mapView.traitCollection))
        pin.translatesAutoresizingMaskIntoConstraints = false
        pin.isUserInteractionEnabled = false
        pin.isAccessibilityElement = false
        CleansiaMapMarker.applyShadow(to: pin.layer)
        mapView.addSubview(pin)
        NSLayoutConstraint.activate([
            pin.centerXAnchor.constraint(equalTo: mapView.layoutMarginsGuide.centerXAnchor),
            pin.bottomAnchor.constraint(equalTo: mapView.layoutMarginsGuide.centerYAnchor),
            pin.widthAnchor.constraint(equalToConstant: CleansiaMapMarker.pinSize.width),
            pin.heightAnchor.constraint(equalToConstant: CleansiaMapMarker.pinSize.height)
        ])
    }

    final class Coordinator: NSObject, MKMapViewDelegate {
        var region: Binding<MKCoordinateRegion>
        /// The centre last exchanged with the binding, in either direction.
        var reported: CLLocationCoordinate2D?
        /// The bottom inset the map's margin was last given.
        var bottomInset: CGFloat = 0

        init(region: Binding<MKCoordinateRegion>) {
            self.region = region
        }

        func mapView(_ mapView: MKMapView, regionDidChangeAnimated _: Bool) {
            reported = mapView.region.center
            region.wrappedValue = mapView.region
        }

        func mapView(_ mapView: MKMapView, viewFor annotation: MKAnnotation) -> MKAnnotationView? {
            CleansiaMapMarker.delegate.mapView(mapView, viewFor: annotation)
        }
    }
}

enum FullBleedMapGeometry {
    static let spanDelta: CLLocationDegrees = 0.01
    static let bottomCoverFraction: CLLocationDegrees = 0.75

    /// Shifts the region center SOUTH of the pin by half the covered span so the
    /// pin lands in the visible UPPER sliver above the sheet — the
    /// `MapBackdrop` `EdgeInsets(0,0,sheetPeekPx,0)` parity
    /// (OrderDetailScreen.kt:273-281) without depending on async layout.
    static func region(for coordinate: Coordinate) -> MKCoordinateRegion {
        let southwardShift = spanDelta * bottomCoverFraction / 2
        let center = CLLocationCoordinate2D(
            latitude: coordinate.latitude - southwardShift,
            longitude: coordinate.longitude
        )
        let span = MKCoordinateSpan(latitudeDelta: spanDelta, longitudeDelta: spanDelta)
        return MKCoordinateRegion(center: center, span: span)
    }
}

struct FullBleedOrderMap: UIViewRepresentable {
    let coordinate: Coordinate
    /// The map's own height, measured by `fullBleedMap(coordinate:)`; 0 leaves the map uncovered.
    var height: CGFloat = 0

    func makeCoordinator() -> Placement {
        Placement()
    }

    func makeUIView(context: Context) -> MKMapView {
        makeMapView(
            placement: context.coordinator,
            coveredHeight: coveredHeight(sheetTop: context.environment.snapSheetTop)
        )
    }

    func makeMapView(placement: Placement = Placement(), coveredHeight: CGFloat = 0) -> MKMapView {
        let mapView = MKMapView()
        mapView.showsUserLocation = false
        CleansiaMapStyle.apply(to: mapView)
        apply(to: mapView, placement: placement, coveredHeight: coveredHeight)
        return mapView
    }

    func updateUIView(_ mapView: MKMapView, context: Context) {
        apply(
            to: mapView,
            placement: context.coordinator,
            coveredHeight: coveredHeight(sheetTop: context.environment.snapSheetTop)
        )
    }

    /// How far the sheet covers the map from its bottom edge. 0 outside a SnapSheet, which publishes no top.
    func coveredHeight(sheetTop: CGFloat) -> CGFloat {
        sheetTop > 0 ? max(height - sheetTop, 0) : 0
    }

    /// The region is centred on the map's whole bounds, as it always was, so the pin's place on screen
    /// depends neither on the safe area nor on the sheet; it is set again only for a new coordinate, so a
    /// sheet drag does not undo a pan. Then the bottom margin follows the sheet's edge, which lifts Apple's
    /// logo and its *Legal* link above the sheet (MapKit's terms) and moves nothing else. The margins ignore
    /// the safe area: the sheet's cover is measured from the map's own bottom edge.
    func apply(to mapView: MKMapView, placement: Placement = Placement(), coveredHeight: CGFloat = 0) {
        mapView.delegate = CleansiaMapMarker.delegate
        let pin = CLLocationCoordinate2D(latitude: coordinate.latitude, longitude: coordinate.longitude)
        if placement.coordinate != coordinate {
            placement.coordinate = coordinate
            placement.cover = 0
            mapView.insetsLayoutMarginsFromSafeArea = false
            mapView.layoutMargins = .zero
            mapView.setRegion(FullBleedMapGeometry.region(for: coordinate), animated: false)
            mapView.removeAnnotations(mapView.annotations)
            let annotation = MKPointAnnotation()
            annotation.coordinate = pin
            mapView.addAnnotation(annotation)
        }
        guard coveredHeight != placement.cover else { return }
        // Not laid out yet: MapKit fits the region when it is, against whatever margin it has then, so the
        // margin waits for that first layout.
        guard mapView.bounds.height > 0 else {
            DispatchQueue.main.async { [weak mapView] in
                guard let mapView, mapView.bounds.height > 0 else { return }
                apply(to: mapView, placement: placement, coveredHeight: coveredHeight)
            }
            return
        }
        placement.cover = coveredHeight
        // MapKit keeps the content still when the margins change (checked on iOS 16.4 and 26.3), so this
        // moves the logo and Legal and not the pin.
        mapView.layoutMargins = UIEdgeInsets(top: 0, left: 0, bottom: coveredHeight, right: Self.ornamentClearance)
    }

    /// The sheet's ornament rides its top edge at the trailing side (`SnapSheet`), and iOS 16 puts *Legal*
    /// at the bottom right — under the ornament — where iOS 26 puts it beside the logo at the bottom left.
    /// The trailing margin keeps it clear of the ornament on either.
    static let ornamentClearance = SnapSheetOrnament.defaultSize + Spacing.m

    /// What this map last applied, so an update re-applies only what changed.
    final class Placement {
        var coordinate: Coordinate?
        var cover: CGFloat = 0
    }
}
