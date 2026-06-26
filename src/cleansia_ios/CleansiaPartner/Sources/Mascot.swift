import SwiftUI

enum Mascot: String {
    case waving = "mascot_waving"
    case leaning = "mascot_leaning"
    case cleaning = "mascot_cleaning"
    case ready = "mascot_ready"

    var image: Image {
        Image(rawValue)
    }
}
