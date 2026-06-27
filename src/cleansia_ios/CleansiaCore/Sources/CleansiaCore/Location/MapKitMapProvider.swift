import MapKit
import SwiftUI

public struct MapKitMapProvider: MapProvider {
    public init() {}

    public func pickerMap(region: Binding<MKCoordinateRegion>, showsUserLocation: Bool) -> AnyView {
        AnyView(
            Map(
                coordinateRegion: region,
                interactionModes: .all,
                showsUserLocation: showsUserLocation,
                annotationItems: [PickerAnnotation]()
            ) { annotation in
                MapMarker(coordinate: annotation.coordinate)
            }
        )
    }

    public func fullBleedMap(coordinate: Coordinate) -> AnyView {
        AnyView(FullBleedOrderMap(coordinate: coordinate))
    }
}

private struct PickerAnnotation: Identifiable {
    let id = UUID()
    let coordinate: CLLocationCoordinate2D
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

    func makeUIView(context _: Context) -> MKMapView {
        let mapView = MKMapView()
        mapView.showsUserLocation = false
        mapView.isRotateEnabled = false
        mapView.isPitchEnabled = false
        apply(to: mapView)
        return mapView
    }

    func updateUIView(_ mapView: MKMapView, context _: Context) {
        apply(to: mapView)
    }

    func apply(to mapView: MKMapView) {
        mapView.setRegion(FullBleedMapGeometry.region(for: coordinate), animated: false)

        let pin = CLLocationCoordinate2D(latitude: coordinate.latitude, longitude: coordinate.longitude)
        mapView.removeAnnotations(mapView.annotations)
        let annotation = MKPointAnnotation()
        annotation.coordinate = pin
        mapView.addAnnotation(annotation)
    }
}
