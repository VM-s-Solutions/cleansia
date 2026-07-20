import SwiftUI

public enum LocationAuthorizationStatus: Equatable {
    case notDetermined
    case denied
    case restricted
    case authorized
}

@MainActor
public protocol LocationProvider: AnyObject {
    var authorizationStatus: LocationAuthorizationStatus { get }
    func requestWhenInUseAuthorization() async -> LocationAuthorizationStatus
    func currentLocation() async -> Coordinate?
}

private struct LocationProviderKey: EnvironmentKey {
    @MainActor static var defaultValue: LocationProvider = CLLocationManagerLocationProvider()
}

public extension EnvironmentValues {
    var locationProvider: LocationProvider {
        get { self[LocationProviderKey.self] }
        set { self[LocationProviderKey.self] = newValue }
    }
}
