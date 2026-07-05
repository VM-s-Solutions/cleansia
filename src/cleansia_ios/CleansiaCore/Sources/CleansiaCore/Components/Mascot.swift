import SwiftUI

public enum Mascot: String, CaseIterable {
    case waving = "mascot_waving"
    case leaning = "mascot_leaning"
    case cleaning = "mascot_cleaning"
    case ready = "mascot_ready"
    case idea = "mascot_idea"
    case mopping = "mascot_mopping"

    public var image: Image {
        Image(rawValue)
    }
}
