import Foundation

public enum GoogleServicePlist {
    public static let resourceName = "GoogleService-Info"

    public static var isPresent: Bool {
        Bundle.main.path(forResource: resourceName, ofType: "plist") != nil
    }
}
