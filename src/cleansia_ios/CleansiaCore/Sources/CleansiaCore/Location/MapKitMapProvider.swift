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
}

private struct PickerAnnotation: Identifiable {
    let id = UUID()
    let coordinate: CLLocationCoordinate2D
}
