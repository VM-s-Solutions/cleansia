import SwiftUI

enum Mascot: String {
    case waving = "mascot_waving"
    case leaning = "mascot_leaning"

    var image: Image {
        Image(rawValue)
    }
}
