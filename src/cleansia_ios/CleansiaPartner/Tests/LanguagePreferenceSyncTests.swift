import CleansiaCore
import XCTest
@testable import CleansiaPartner

/// `User.PreferredLanguageCode` is what `PayPeriodBackgroundService` renders the period-closed mail AND
/// the payout invoice PDF in — a tax document the cleaner files — so a language that only ever reached
/// `UserDefaults` left the app in Czech and the document that matters in whatever signup sent.
final class LanguagePreferenceSyncTests: XCTestCase {
    private var client: FakePartnerUserClient!

    override func setUp() {
        super.setUp()
        client = FakePartnerUserClient()
    }

    // MARK: - The pure decision

    func testBuildsAFullProfileReplayCarryingTheNewLanguage() throws {
        let push = try XCTUnwrap(LanguagePreferencePush.forUser(.stub(preferredLanguageCode: "en"), languageCode: "uk"))

        XCTAssertEqual(push.languageCode, "uk")
        XCTAssertEqual(push.firstName, "Ondrej")
        XCTAssertEqual(push.lastName, "Novak")
        XCTAssertEqual(push.phoneNumber, "+420777111222")
        XCTAssertEqual(push.birthDate, Date(timeIntervalSince1970: 400_000_000))
    }

    func testNoPushWhenTheServerAlreadyHoldsThatLanguage() {
        XCTAssertNil(LanguagePreferencePush.forUser(.stub(preferredLanguageCode: "uk"), languageCode: "uk"))
    }

    func testNoPushWhenSignedOut() {
        XCTAssertNil(LanguagePreferencePush.forUser(nil, languageCode: "uk"))
    }

    /// First and last name are a blind replace on the handler and their validators reject blanks, so
    /// replaying a nameless profile would 400 rather than save the language.
    func testNoPushWhenTheProfileHasNoNameToReplay() {
        XCTAssertNil(LanguagePreferencePush.forUser(.stub(firstName: ""), languageCode: "uk"))
        XCTAssertNil(LanguagePreferencePush.forUser(.stub(lastName: "  "), languageCode: "uk"))
        XCTAssertNil(LanguagePreferencePush.forUser(.stub(firstName: nil), languageCode: "uk"))
        XCTAssertNil(LanguagePreferencePush.forUser(.stub(lastName: nil), languageCode: "uk"))
    }

    /// A cleaner waiting on approval may not have filled a phone or a birth date yet, and that is the
    /// population the registration lock's chooser exists for. Both are "nothing to say" fields the
    /// handler preserves, so an absent one must not cost them the push — but the phone still has to
    /// reach the wire as a blank string, because the command's `PhoneNumber` is a non-nullable
    /// reference type and the host would refuse an omitted member outright.
    func testAnIncompleteProfileStillPushesReplayingBlanksRatherThanDroppingTheLanguage() throws {
        let push = try XCTUnwrap(
            LanguagePreferencePush.forUser(.stub(phoneNumber: nil, birthDate: nil), languageCode: "uk")
        )

        XCTAssertEqual(push.languageCode, "uk")
        XCTAssertEqual(push.phoneNumber, "")
        XCTAssertNil(push.birthDate)
    }

    func testPushesWhenTheServerHasNoLanguageYet() {
        XCTAssertEqual(
            LanguagePreferencePush.forUser(.stub(preferredLanguageCode: nil), languageCode: "cs")?.languageCode,
            "cs"
        )
    }

    // MARK: - The live sync

    func testLiveSyncReplaysTheWholeProfileAlongsideTheNewLanguage() async {
        client.currentUser = .success(.stub(preferredLanguageCode: "en"))

        await sync().push(languageCode: "uk")

        XCTAssertEqual(client.updates, [FakePartnerUserClient.Update(
            firstName: "Ondrej",
            lastName: "Novak",
            phoneNumber: "+420777111222",
            birthDate: Date(timeIntervalSince1970: 400_000_000),
            languageCode: "uk"
        )])
    }

    func testLiveSyncSendsNothingWhenThereIsNoSession() async {
        await sync(signedIn: false).push(languageCode: "uk")

        XCTAssertEqual(client.reads, 0)
        XCTAssertEqual(client.updates, [])
    }

    func testLiveSyncSendsNothingWhenNothingChanged() async {
        client.currentUser = .success(.stub(preferredLanguageCode: "uk"))

        await sync().push(languageCode: "uk")

        XCTAssertEqual(client.updates, [])
    }

    func testLiveSyncSendsNothingWhenTheProfileCannotBeRead() async {
        client.currentUser = .failure(ApiError(code: "network.unreachable"))

        await sync().push(languageCode: "uk")

        XCTAssertEqual(client.updates, [])
    }

    /// A display-language tap is not a save anyone is waiting on: a rejected push must not throw, and
    /// the one attempt is the whole of it — the local preference stands and the next language change
    /// is what re-sends.
    func testLiveSyncSwallowsAFailedPushAndDoesNotRetry() async {
        client.updateResult = .failure(ApiError(httpStatus: 400))

        await sync().push(languageCode: "uk")

        XCTAssertEqual(client.updates.count, 1)
    }

    // MARK: - Who owns the push

    /// Both language pickers pop in the same tap that starts this, so a push bound to the screen's own
    /// task would be cancelled somewhere between the profile read and the write. The first arrangement
    /// is that hazard, run deliberately so the second one cannot pass vacuously.
    func testAPushOwnedByTheCallingScreenDiesWithIt() async {
        let owned = Task { await self.sync().push(languageCode: "uk") }
        owned.cancel()
        await owned.value

        XCTAssertEqual(client.updates, [], "the fake completes a cancelled round trip — nothing below means anything")

        let sync = sync()
        let screen = Task { sync.send(languageCode: "uk") }
        screen.cancel()
        await screen.value

        await fulfillment(of: [client.pushed], timeout: 5)
        XCTAssertEqual(client.updates.count, 1)
    }

    /// The lifetime is owned deliberately rather than by accident: the push runs in a task of the
    /// seam's own, not a continuation of whatever context asked for it.
    func testThePushDoesNotInheritTheCallingScreensTaskContext() async {
        let sync = sync()

        CallerTaskMarker.$isSet.withValue(true) {
            sync.send(languageCode: "uk")
        }

        await fulfillment(of: [client.pushed], timeout: 5)
        XCTAssertFalse(client.sawCallersTaskContext)
    }

    private func sync(signedIn: Bool = true) -> LiveLanguagePreferenceSync {
        LiveLanguagePreferenceSync(tokenStore: SessionTokenStore(signedIn: signedIn), client: client)
    }
}
