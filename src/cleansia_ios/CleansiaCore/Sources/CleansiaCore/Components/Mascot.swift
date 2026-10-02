import SwiftUI

/// Every mascot — still imagesets and animated data assets alike — ships in CleansiaCore's own
/// bundle, so the customer and partner apps read one copy instead of carrying a catalog each.
public enum MascotAssets {
    public static let bundle = Bundle.module
}

public enum Mascot: String, CaseIterable {
    case waving = "mascot_waving"
    case leaning = "mascot_leaning"
    case resting = "mascot_resting"
    case cleaning = "mascot_cleaning"
    case ready = "mascot_ready"
    case idea = "mascot_idea"
    case mopping = "mascot_mopping"
    case sprayAndCloth = "mascot_spray_and_cloth"
    case thumbsUp = "mascot_thumbs_up"
    /// The web's dedicated Plus drawing (holding a star card), decoded from `mascot-plus.webp` — the
    /// web's `.png` copy carries a baked halo.
    case plus = "mascot_plus"
    /// Holding a slip — the partner app's drawing, for the Home credit slide.
    case invoice = "mascot_invoice"
    /// Pushing a floor scrubber, thumb up — the web's drawing, for the Home express slide.
    case floorScrubber = "mascot_floor_scrubber"
    /// Vacuuming — the web's drawing, for the Home quick-size slide.
    case vacuuming = "mascot_vacuuming"

    public var image: Image {
        Image(rawValue, bundle: MascotAssets.bundle)
    }
}
