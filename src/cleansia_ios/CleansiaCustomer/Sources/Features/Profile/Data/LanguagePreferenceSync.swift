import CleansiaCore
import Foundation

/// Pushes the display language the user picked onto `User.PreferredLanguageCode`, which is what every
/// server-rendered mail (booking confirmations, reminders, receipts) is written in. Without it the
/// stamp is frozen at whatever signup happened to send.
@MainActor
protocol LanguagePreferenceSync: AnyObject {
    func send(languageCode: String) async

    /// What a beginning session calls. It differs from `send` in the one way that decides whether the
    /// reconcile means anything: it reads the server before comparing. `send` answers from the cached
    /// profile because the picker only runs on a screen that already has one — and that cache is
    /// cleared with the session, so a reconcile trusting it would compare against nothing on the exact
    /// launch it exists for and silently do nothing.
    func reconcile(languageCode: String) async
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

    /// A read then a conditional write, and silent for the same reason. A server that already holds this
    /// language costs the read and nothing else — an unconditional write would replay a stale local
    /// profile over the server's on every launch, for a value that was already right. Signed out, the
    /// read is refused by the token store before a request is made.
    func reconcile(languageCode: String) async {
        await repository.refresh()
        await send(languageCode: languageCode)
    }
}
