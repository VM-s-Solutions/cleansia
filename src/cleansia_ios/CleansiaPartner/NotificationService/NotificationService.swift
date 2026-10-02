import UserNotifications

/// Re-renders a loc-key push in the language picked inside the app, which iOS cannot do: it resolves the
/// keys in the app's SYSTEM language. The server marks every loc-key alert mutable-content, so this runs
/// for each of them; on any miss the alert goes out exactly as iOS resolved it.
/// -> /architecture/push-notifications
final class NotificationService: UNNotificationServiceExtension {
    override func didReceive(
        _ request: UNNotificationRequest,
        withContentHandler contentHandler: @escaping (UNNotificationContent) -> Void
    ) {
        guard let content = request.content.mutableCopy() as? UNMutableNotificationContent,
              let alert = AppGroupLanguage.localizedAlert(
                  userInfo: request.content.userInfo,
                  languageTag: AppGroupLanguage.read(appGroup: AppGroupLanguage.partnerAppGroup),
                  bundle: .main
              )
        else {
            contentHandler(request.content)
            return
        }
        content.title = alert.title
        content.body = alert.body
        contentHandler(content)
    }
}
