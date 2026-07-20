#if DEBUG
    import Foundation

    @MainActor
    public final class PreviewLocationProvider: LocationProvider {
        public init() {}

        public var authorizationStatus: LocationAuthorizationStatus {
            .denied
        }

        public func requestWhenInUseAuthorization() async -> LocationAuthorizationStatus {
            .denied
        }

        public func currentLocation() async -> Coordinate? {
            nil
        }
    }
#endif
