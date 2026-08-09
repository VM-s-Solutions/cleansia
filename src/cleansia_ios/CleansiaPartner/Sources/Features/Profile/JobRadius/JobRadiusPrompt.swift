import Foundation

/// "Never set a radius" is not the same question as "never been asked". A null radius is a legitimate
/// standing choice — the country-wide board — so asking on the null alone would re-ask the cleaner who
/// chose it, every launch, forever. The device remembers the ask; the server owns the preference.
enum JobRadiusPrompt {
    /// The `AppSettingsStore` prompt id. Keyed per cleaner, so a second account on a shared device is
    /// still asked once.
    static let settingsKey = "job_radius"

    static func shouldPresent(radiusKm: Int?, hasBeenAsked: Bool) -> Bool {
        radiusKm == nil && !hasBeenAsked
    }
}
