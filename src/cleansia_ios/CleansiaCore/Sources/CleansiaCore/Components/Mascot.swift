import SwiftUI

/// Every mascot — still imagesets and animated data assets alike — ships in CleansiaCore's own
/// bundle, so the customer and partner apps read one copy instead of carrying a catalog each.
public enum MascotAssets {
    public static let bundle = Bundle.module
}

public enum Mascot: String, CaseIterable {
    case waving = "mascot_waving"
    case leaning = "mascot_leaning"
    case cleaning = "mascot_cleaning"
    case ready = "mascot_ready"
    case idea = "mascot_idea"
    case mopping = "mascot_mopping"
    case sprayAndCloth = "mascot_spray_and_cloth"
    case thumbsUp = "mascot_thumbs_up"

    public var image: Image {
        Image(rawValue, bundle: MascotAssets.bundle)
    }
}
