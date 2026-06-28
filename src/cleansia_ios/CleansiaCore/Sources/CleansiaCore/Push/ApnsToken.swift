import Foundation

public extension Data {
    var apnsHexToken: String {
        map { String(format: "%02x", $0) }.joined()
    }
}
