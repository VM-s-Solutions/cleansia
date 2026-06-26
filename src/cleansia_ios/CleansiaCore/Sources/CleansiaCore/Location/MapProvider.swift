import MapKit
import SwiftUI

public protocol MapProvider {
    func pickerMap(region: Binding<MKCoordinateRegion>, showsUserLocation: Bool) -> AnyView
}
