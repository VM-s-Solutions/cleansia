import CoreLocation

extension LocationAuthorizationStatus {
    init(_ status: CLAuthorizationStatus) {
        switch status {
        case .authorizedWhenInUse, .authorizedAlways:
            self = .authorized
        case .denied:
            self = .denied
        case .restricted:
            self = .restricted
        case .notDetermined:
            self = .notDetermined
        @unknown default:
            self = .denied
        }
    }
}

/// The one-shot current-location seam — the only CoreLocation consumer besides
/// the geocoding provider (the Android `LocationService.kt` parity). Best-effort:
/// a fresh `requestLocation()` fix first, the manager's cached fix as fallback,
/// nil when both fail — never an error surfaced to the caller.
@MainActor
public final class CLLocationManagerLocationProvider: NSObject, LocationProvider {
    private let manager = CLLocationManager()
    private var authorizationContinuations: [CheckedContinuation<LocationAuthorizationStatus, Never>] = []
    private var locationContinuations: [CheckedContinuation<Coordinate?, Never>] = []

    override public init() {
        super.init()
        manager.desiredAccuracy = kCLLocationAccuracyHundredMeters
        manager.delegate = self
    }

    public var authorizationStatus: LocationAuthorizationStatus {
        LocationAuthorizationStatus(manager.authorizationStatus)
    }

    public func requestWhenInUseAuthorization() async -> LocationAuthorizationStatus {
        guard authorizationStatus == .notDetermined else { return authorizationStatus }
        return await withCheckedContinuation { continuation in
            authorizationContinuations.append(continuation)
            manager.requestWhenInUseAuthorization()
        }
    }

    public func currentLocation() async -> Coordinate? {
        guard authorizationStatus == .authorized else { return nil }
        return await withCheckedContinuation { continuation in
            locationContinuations.append(continuation)
            if locationContinuations.count == 1 {
                manager.requestLocation()
            }
        }
    }

    private func resolveAuthorization(_ status: LocationAuthorizationStatus) {
        // The delegate also fires on delegate installation and when the prompt
        // is still pending — only a settled answer resolves the waiters.
        guard status != .notDetermined else { return }
        let waiting = authorizationContinuations
        authorizationContinuations = []
        waiting.forEach { $0.resume(returning: status) }
    }

    private func resolveLocation(_ coordinate: Coordinate?) {
        let waiting = locationContinuations
        locationContinuations = []
        waiting.forEach { $0.resume(returning: coordinate) }
    }

    private var cachedFix: Coordinate? {
        manager.location.map { Coordinate(latitude: $0.coordinate.latitude, longitude: $0.coordinate.longitude) }
    }
}

/// The manager is created and delegated on the main run loop, so callbacks
/// arrive on the main thread — the @preconcurrency conformance encodes that.
extension CLLocationManagerLocationProvider: @preconcurrency CLLocationManagerDelegate {
    public func locationManagerDidChangeAuthorization(_ manager: CLLocationManager) {
        resolveAuthorization(LocationAuthorizationStatus(manager.authorizationStatus))
    }

    public func locationManager(_: CLLocationManager, didUpdateLocations locations: [CLLocation]) {
        let fix = locations.last.map {
            Coordinate(latitude: $0.coordinate.latitude, longitude: $0.coordinate.longitude)
        }
        resolveLocation(fix ?? cachedFix)
    }

    public func locationManager(_: CLLocationManager, didFailWithError _: Error) {
        resolveLocation(cachedFix)
    }
}
