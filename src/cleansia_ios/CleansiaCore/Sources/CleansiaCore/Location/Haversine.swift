import Foundation

public extension Coordinate {
    /// Great-circle distance in kilometres to `other` (the haversine formula —
    /// the `haversineKm` parity used for the Available-card distance metric).
    func distanceKm(to other: Coordinate) -> Double {
        let earthRadiusKm = 6371.0088 // IUGG mean radius (Android Distance.kt parity)
        let dLat = (other.latitude - latitude).radians
        let dLon = (other.longitude - longitude).radians
        let lat1 = latitude.radians
        let lat2 = other.latitude.radians

        let chord = sin(dLat / 2) * sin(dLat / 2)
            + cos(lat1) * cos(lat2) * sin(dLon / 2) * sin(dLon / 2)
        let angularDistance = 2 * atan2(sqrt(chord), sqrt(1 - chord))
        return earthRadiusKm * angularDistance
    }
}

private extension Double {
    var radians: Double {
        self * .pi / 180
    }
}
