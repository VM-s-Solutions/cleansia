import CleansiaCore
import Foundation

/// Pushes the display language the cleaner picked onto `User.PreferredLanguageCode`. That stamp is what
/// `PayPeriodBackgroundService` threads into both the period-closed email and the rendered payout
/// invoice PDF — a tax document the cleaner files — so a language that only ever reached the local
/// store leaves the app in Czech and the document that matters in whatever signup happened to send.
protocol LanguagePreferenceSync: AnyObject {
    /// Returns before the push completes, and deliberately is not `async`: both language pickers pop in
    /// the same tap that starts this, so a caller able to await it could bind a two-request round trip
    /// to a screen that is already gone.
    func send(languageCode: String)
}

struct LanguagePush: Equatable {
    let firstName: String
    let lastName: String
    let phoneNumber: String
    let birthDate: Date?
    let languageCode: String
}

/// `UpdateCurrentUser` still replaces first and last name outright — phone, birth date, photo and
/// language treat an absent value as "nothing to say" — so a language-only push has to replay the rest
/// of the profile verbatim, and must not run on a profile whose names the validators would reject.
///
/// A missing phone or birth date is NOT such a hole: the cleaner on the registration lock frequently has
/// neither yet, and both survive the save untouched. The phone still travels, as a blank, because an
/// omitted member is refused by the model binder rather than read as silence.
enum LanguagePreferencePush {
    static func forUser(_ user: CurrentUser?, languageCode: String) -> LanguagePush? {
        guard let user, user.preferredLanguageCode != languageCode,
              let firstName = user.firstName?.trimmedOrNil,
              let lastName = user.lastName?.trimmedOrNil
        else { return nil }
        return LanguagePush(
            firstName: firstName,
            lastName: lastName,
            phoneNumber: user.phoneNumber ?? "",
            birthDate: user.birthDate,
            languageCode: languageCode
        )
    }
}

final class LiveLanguagePreferenceSync: LanguagePreferenceSync, @unchecked Sendable {
    private let tokenStore: TokenStore
    private let client: PartnerUserClient

    init(tokenStore: TokenStore, client: PartnerUserClient) {
        self.tokenStore = tokenStore
        self.client = client
    }

    func send(languageCode: String) {
        Task.detached(priority: .utility) { [self] in await push(languageCode: languageCode) }
    }

    /// Silent on failure by design: a display-language tap is not a save the cleaner is waiting on and
    /// the local switch has already happened, so there is nothing to report and nothing to retry.
    ///
    /// The session is answered from the token store rather than by letting the call 401. The pre-auth
    /// intro carries this same seam, and a 401 there hands the shared authenticator a refresh it cannot
    /// perform on a handset that has never signed in.
    ///
    /// Not part of `LanguagePreferenceSync`, and never to be awaited from a screen: `send` is the seam,
    /// and awaiting this instead is the shape that dies when the picker pops.
    func push(languageCode: String) async {
        guard tokenStore.current() != nil,
              case let .success(user) = await client.getCurrentUser(),
              let push = LanguagePreferencePush.forUser(user, languageCode: languageCode)
        else { return }
        _ = await client.updateCurrentUser(
            firstName: push.firstName,
            lastName: push.lastName,
            phoneNumber: push.phoneNumber,
            birthDate: push.birthDate,
            languageCode: push.languageCode
        )
    }
}
