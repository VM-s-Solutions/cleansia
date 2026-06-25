import Foundation

enum AppConfig {
    static let apiBaseURL: URL = {
        guard
            let raw = Bundle.main.object(forInfoDictionaryKey: "API_BASE_URL") as? String,
            let url = URL(string: raw)
        else {
            fatalError("API_BASE_URL missing or malformed in Info.plist")
        }
        return url
    }()
}
