#if DEBUG
    import MapKit
    import SwiftUI

    public struct PreviewMapProvider: MapProvider {
        public init() {}

        public func pickerMap(region _: Binding<MKCoordinateRegion>, showsUserLocation _: Bool) -> AnyView {
            AnyView(CleansiaColors.surfaceVariant)
        }

        public func fullBleedMap(coordinate _: Coordinate) -> AnyView {
            AnyView(CleansiaColors.surfaceVariant)
        }
    }
#endif
