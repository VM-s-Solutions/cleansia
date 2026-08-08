import MapKit
import SwiftUI

/// The map pin, tinted to Android's (`OrderDetailMap.kt` `MapPin` / partner `OrderDetailScreen.kt`
/// `MapBackdropPin`: a `colorScheme.primary` disc with a white location glyph). MapKit's stock
/// marker is red, so every annotation is vended through here instead.
///
/// The light/dark hexes are the source of truth and the testable surface: a SwiftUI `Color` →
/// `UIColor` roundtrip flattens dynamic providers on the iOS-16 floor.
public enum CleansiaMapMarker {
    public static let reuseIdentifier = "CleansiaMapMarker"
    public static let glyphSymbolName = "mappin"
    public static let tint: (light: UInt32, dark: UInt32) = (light: 0x0284C7, dark: 0x38BDF8)

    public static var tintColor: Color {
        .dynamic(light: Color(hex: tint.light), dark: Color(hex: tint.dark))
    }

    /// Stateless, so one instance serves every map — `MKMapView.delegate` is weak and a
    /// per-map instance would need an owner to outlive the assignment.
    static let delegate = MapMarkerDelegate()

    static var markerTintColor: UIColor {
        UIColor { traits in
            UIColor(Color(hex: traits.userInterfaceStyle == .dark ? tint.dark : tint.light))
        }
    }

    static let glyphColor = UIColor.white
}

final class CleansiaMarkerAnnotationView: MKMarkerAnnotationView {
    override init(annotation: MKAnnotation?, reuseIdentifier: String?) {
        super.init(annotation: annotation, reuseIdentifier: reuseIdentifier)
        markerTintColor = CleansiaMapMarker.markerTintColor
        glyphTintColor = CleansiaMapMarker.glyphColor
        glyphImage = UIImage(systemName: CleansiaMapMarker.glyphSymbolName)
    }

    @available(*, unavailable)
    required init?(coder _: NSCoder) {
        fatalError("CleansiaMarkerAnnotationView is not storyboard-instantiable")
    }
}

final class MapMarkerDelegate: NSObject, MKMapViewDelegate {
    func mapView(_ mapView: MKMapView, viewFor annotation: MKAnnotation) -> MKAnnotationView? {
        guard annotation is MKPointAnnotation else { return nil }
        let reused = mapView.dequeueReusableAnnotationView(withIdentifier: CleansiaMapMarker.reuseIdentifier)
        guard let marker = reused as? CleansiaMarkerAnnotationView else {
            return CleansiaMarkerAnnotationView(
                annotation: annotation,
                reuseIdentifier: CleansiaMapMarker.reuseIdentifier
            )
        }
        marker.annotation = annotation
        return marker
    }
}
