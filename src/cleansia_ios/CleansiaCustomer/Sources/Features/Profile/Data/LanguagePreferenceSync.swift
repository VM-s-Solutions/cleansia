import CleansiaCore
import Foundation

/// Pushes the display language the user picked onto `User.PreferredLanguageCode`, which is what every
/// server-rendered mail (booking confirmations, reminders, receipts) is written in. Without it the
/// stamp is frozen at whatever signup happened to send.
@MainActor
protocol LanguagePreferenceSync: AnyObject {
    func send(languageCode: String) async
}

/// `UpdateCurrentUser` is a blind full replace of first name / last name / phone / birth date and only
/// then applies the language, so a language-only push has to replay the rest of the profile verbatim —
/// and must not run at all on a profile that has holes the validators would reject.
enum LanguagePreferencePush {
    static func update(for user: CurrentUserProfile, languageCode: String) -> ProfileUpdate? {
        guard user.isComplete, user.preferredLanguageCode != languageCode else { return nil }
        return ProfileUpdate(
            id: user.id,
            firstName: user.firstName,
            lastName: user.lastName,
            phoneNumber: user.phoneNumber,
            birthDate: user.birthDate,
            languageCode: languageCode
        )
    }
}

@MainActor
final class LiveLanguagePreferenceSync: LanguagePreferenceSync {
    private let repository: UserProfileRepository

    init(repository: UserProfileRepository) {
        self.repository = repository
    }

    /// Silent on failure by design: a display-language tap is not a save the user is waiting on, and the
    /// local switch has already happened. The next profile save re-sends it.
    func send(languageCode: String) async {
        guard let user = repository.currentUser,
              let update = LanguagePreferencePush.update(for: user, languageCode: languageCode)
        else { return }
        _ = await repository.update(update)
    }
}
