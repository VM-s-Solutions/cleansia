import MapKit
import SwiftUI

public protocol MapProvider {
    /// `bottomInset` is how far the screen's own controls cover the map's bottom edge, above the safe
    /// area. The screen measures it, because those controls grow with the text size. The map keeps
    /// Apple's logo, its *Legal* link and the picked centre above it.
    func pickerMap(region: Binding<MKCoordinateRegion>, showsUserLocation: Bool, bottomInset: CGFloat) -> AnyView
    func fullBleedMap(coordinate: Coordinate) -> AnyView
}
