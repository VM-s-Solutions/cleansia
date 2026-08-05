import CleansiaCore
import CleansiaCustomerApi
import Combine
import UIKit
import XCTest
@testable import CleansiaCustomer

@MainActor
final class ProfileAvatarViewModelTests: XCTestCase {
    private var client: FakeUserProfileClient!
    private var repository: UserProfileRepository!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakeUserProfileClient()
        repository = UserProfileRepository(client: client)
        snackbar = SnackbarController()
    }

    private func makeVM() -> ProfileViewModel {
        ProfileViewModel(
            repository: repository,
            settings: UserDefaultsAppSettingsStore(defaults: UserDefaults(suiteName: "test.\(UUID().uuidString)")!),
            snackbar: snackbar
        )
    }

    // MARK: - What the editor draws

    func testTheEditorShowsTheStoredPhotoUntilSomethingIsPicked() async {
        client.currentUserResult = .success(ProfileFixtures.user(profilePhoto: ProfileFixtures.photo()))
        let vm = makeVM()
        await vm.refresh()

        XCTAssertEqual(vm.editorAvatar, .remote(ProfileFixtures.photo()))
        XCTAssertTrue(vm.canRemoveAvatar)
    }

    func testTheEditorShowsInitialsForAnAccountWithNoPhoto() async {
        client.currentUserResult = .success(ProfileFixtures.user())
        let vm = makeVM()
        await vm.refresh()

        XCTAssertEqual(vm.editorAvatar, .initials)
        XCTAssertFalse(vm.canRemoveAvatar)
    }

    func testPickingAnImageStagesItAndPreviewsIt() async {
        client.currentUserResult = .success(ProfileFixtures.user())
        let vm = makeVM()
        await vm.refresh()

        let picked = ProfileFixtures.image()
        vm.pickAvatar(picked)

        XCTAssertEqual(vm.editorAvatar, .picked(picked))
        XCTAssertTrue(vm.canRemoveAvatar)
        XCTAssertFalse(vm.avatarEdit.upload?.base64.isEmpty ?? true)
        XCTAssertEqual(vm.avatarEdit.upload?.contentType, "image/jpeg")
    }

    func testAnUnreadableImageIsRejectedWithoutStagingAnything() async {
        client.currentUserResult = .success(ProfileFixtures.user())
        let vm = makeVM()
        await vm.refresh()

        vm.pickAvatar(UIImage())

        XCTAssertEqual(vm.avatarEdit, .unchanged)
        XCTAssertNotNil(snackbar.current)
    }

    func testRemovingFallsBackToInitialsAndLeavesNothingToRemove() async {
        client.currentUserResult = .success(ProfileFixtures.user(profilePhoto: ProfileFixtures.photo()))
        let vm = makeVM()
        await vm.refresh()

        vm.removeAvatar()

        XCTAssertEqual(vm.editorAvatar, .initials)
        XCTAssertFalse(vm.canRemoveAvatar)
    }

    func testDiscardingTheEditRestoresTheStoredPhoto() async {
        client.currentUserResult = .success(ProfileFixtures.user(profilePhoto: ProfileFixtures.photo()))
        let vm = makeVM()
        await vm.refresh()
        vm.removeAvatar()

        vm.discardAvatarEdit()

        XCTAssertEqual(vm.editorAvatar, .remote(ProfileFixtures.photo()))
    }

    // MARK: - What the save sends

    func testSaveUploadsThePickedPhoto() async {
        client.currentUserResult = .success(ProfileFixtures.user())
        client.updateResult = .success(())
        let vm = makeVM()
        await vm.refresh()
        vm.pickAvatar(ProfileFixtures.image())

        await save(vm)

        XCTAssertNotNil(client.lastUpdate?.photo)
        XCTAssertEqual(client.lastUpdate?.photo?.contentType, "image/jpeg")
        XCTAssertEqual(client.lastUpdate?.removePhoto, false)
    }

    /// The whole client chain in one assertion — picked bitmap, compressed, base64'd, mapped onto the
    /// generated command — decoded back into a JPEG inside the stated bound. Each hop is covered
    /// separately; this is the one that fails if two of them stop agreeing.
    func testTheUploadedBytesAreABoundedJpegOnTheGeneratedCommand() async throws {
        client.currentUserResult = .success(ProfileFixtures.user())
        client.updateResult = .success(())
        let vm = makeVM()
        await vm.refresh()
        vm.pickAvatar(ProfileFixtures.image(width: 4000, height: 2000))

        await save(vm)

        let update = try XCTUnwrap(client.lastUpdate)
        let base64 = try XCTUnwrap(UpdateCurrentUserCommand(update).photo?.base64Content)
        let bytes = try XCTUnwrap(Data(base64Encoded: base64))
        let decoded = try XCTUnwrap(UIImage(data: bytes))

        XCTAssertEqual(max(decoded.size.width, decoded.size.height) * decoded.scale, 1920, accuracy: 1)
        XCTAssertEqual(bytes.prefix(3), Data([0xFF, 0xD8, 0xFF]), "the backend accepts a JPEG by signature")
    }

    func testSaveAsksForRemovalWithoutSendingAPhoto() async {
        client.currentUserResult = .success(ProfileFixtures.user(profilePhoto: ProfileFixtures.photo()))
        client.updateResult = .success(())
        let vm = makeVM()
        await vm.refresh()
        vm.removeAvatar()

        await save(vm)

        XCTAssertEqual(client.lastUpdate?.removePhoto, true)
        XCTAssertNil(client.lastUpdate?.photo)
    }

    /// The `fe0c985b` regression: an ordinary field edit must say nothing about the avatar, or every
    /// name change wipes the user's photo.
    func testSaveWithAnUntouchedAvatarSendsNeitherAPhotoNorARemoval() async {
        client.currentUserResult = .success(ProfileFixtures.user(profilePhoto: ProfileFixtures.photo()))
        client.updateResult = .success(())
        let vm = makeVM()
        await vm.refresh()

        await save(vm)

        XCTAssertNil(client.lastUpdate?.photo)
        XCTAssertEqual(client.lastUpdate?.removePhoto, false)
    }

    func testCompletingOnboardingSaysNothingAboutTheAvatar() async {
        client.currentUserResult = .success(ProfileFixtures.user(phoneNumber: nil))
        client.updateResult = .success(())
        let vm = makeVM()
        await vm.refresh()

        await vm.completeOnboarding(firstName: "Ada", lastName: "Lovelace", phoneNumber: "+420111", birthDate: nil)

        XCTAssertNil(client.lastUpdate?.photo)
        XCTAssertEqual(client.lastUpdate?.removePhoto, false)
    }

    func testASavedEditIsNotResentOnTheNextSave() async {
        client.currentUserResult = .success(ProfileFixtures.user())
        client.updateResult = .success(())
        let vm = makeVM()
        await vm.refresh()
        vm.pickAvatar(ProfileFixtures.image())

        await save(vm)
        XCTAssertEqual(vm.avatarEdit, .unchanged)

        await save(vm)
        XCTAssertNil(client.lastUpdate?.photo)
        XCTAssertEqual(client.lastUpdate?.removePhoto, false)
    }

    func testAFailedSaveKeepsTheStagedPhotoForTheRetry() async {
        client.currentUserResult = .success(ProfileFixtures.user())
        client.updateResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        await vm.refresh()
        let picked = ProfileFixtures.image()
        vm.pickAvatar(picked)

        await save(vm)

        XCTAssertEqual(vm.editorAvatar, .picked(picked))
        XCTAssertNotNil(vm.avatarEdit.upload)
    }

    // MARK: - Load failure

    func testAFailedImageLoadRefetchesTheProfileExactlyOnce() async {
        client.currentUserResult = .success(ProfileFixtures.user(profilePhoto: ProfileFixtures.photo()))
        let vm = makeVM()
        await vm.refresh()
        let fetchesAfterLoad = client.currentUserCallCount

        await vm.avatarLoadFailed(fileName: "blob-1")
        await vm.avatarLoadFailed(fileName: "blob-1")

        XCTAssertEqual(client.currentUserCallCount, fetchesAfterLoad + 1)
    }

    func testANewlyUploadedPhotoEarnsItsOwnRetry() async {
        client.currentUserResult = .success(ProfileFixtures.user(profilePhoto: ProfileFixtures.photo()))
        let vm = makeVM()
        await vm.refresh()
        let fetchesAfterLoad = client.currentUserCallCount

        await vm.avatarLoadFailed(fileName: "blob-1")
        await vm.avatarLoadFailed(fileName: "blob-2")

        XCTAssertEqual(client.currentUserCallCount, fetchesAfterLoad + 2)
    }

    /// The retry a refetch buys is spent, not forfeited: once the fresh signature has rendered, the
    /// next expiry of the same blob is a new failure and earns its own refetch. Without this the disc
    /// falls back to initials for the rest of the session on the second expiry.
    func testAnImageThatLoadsAfterTheRetryEarnsAnotherOne() async {
        client.currentUserResult = .success(ProfileFixtures.user(profilePhoto: ProfileFixtures.photo()))
        let vm = makeVM()
        await vm.refresh()
        let fetchesAfterLoad = client.currentUserCallCount

        await vm.avatarLoadFailed(fileName: "blob-1")
        vm.avatarLoadSucceeded()
        await vm.avatarLoadFailed(fileName: "blob-1")

        XCTAssertEqual(client.currentUserCallCount, fetchesAfterLoad + 2)
    }

    // MARK: - What a saved change confirms

    func testASavedPickConfirmsTheUpload() async {
        client.currentUserResult = .success(ProfileFixtures.user())
        client.updateResult = .success(())
        let vm = makeVM()
        await vm.refresh()
        let log = record()
        vm.pickAvatar(ProfileFixtures.image())

        await save(vm)

        XCTAssertEqual(log.successes.map(\.text), [L10n.EditProfile.photoUploadSuccess])
    }

    func testASavedRemovalConfirmsTheRemoval() async {
        client.currentUserResult = .success(ProfileFixtures.user(profilePhoto: ProfileFixtures.photo()))
        client.updateResult = .success(())
        let vm = makeVM()
        await vm.refresh()
        let log = record()
        vm.removeAvatar()

        await save(vm)

        XCTAssertEqual(log.successes.map(\.text), [L10n.EditProfile.photoRemoveSuccess])
    }

    /// A rejected save leaves the stored photo exactly as it was, so claiming otherwise is a lie —
    /// and this is the only test that separates "a photo was chosen" from "a photo was saved". The
    /// positive tests above see one emit either way.
    func testAFailedSaveClaimsNothingAboutThePhoto() async {
        client.currentUserResult = .success(ProfileFixtures.user())
        client.updateResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        await vm.refresh()
        let log = record()
        vm.pickAvatar(ProfileFixtures.image())

        await save(vm)

        XCTAssertTrue(log.successes.isEmpty)
    }

    /// Remove is offered for a pick that has never been saved, because it is also how a pick is undone.
    /// The server has nothing to delete then, so the edit goes back to `unchanged` and the save says
    /// nothing rather than confirming a removal that never happened.
    func testRemovingAPickWithNoStoredPhotoDiscardsItAndClaimsNoRemoval() async {
        client.currentUserResult = .success(ProfileFixtures.user())
        client.updateResult = .success(())
        let vm = makeVM()
        await vm.refresh()
        vm.pickAvatar(ProfileFixtures.image())
        let log = record()

        vm.removeAvatar()
        XCTAssertEqual(vm.avatarEdit, .unchanged)
        XCTAssertEqual(vm.editorAvatar, .initials)

        await save(vm)

        XCTAssertEqual(client.lastUpdate?.removePhoto, false)
        XCTAssertNil(client.lastUpdate?.photo)
        XCTAssertEqual(log.successes.map(\.text), [L10n.Profile.saveSuccess])
    }

    /// One expression at the call site, so exactly one message: the specific claim beats the general
    /// one and they are never both emitted for the same save.
    func testASaveThatLeavesTheAvatarAloneConfirmsTheProfileInstead() async {
        client.currentUserResult = .success(ProfileFixtures.user(profilePhoto: ProfileFixtures.photo()))
        client.updateResult = .success(())
        let vm = makeVM()
        await vm.refresh()
        let log = record()

        await save(vm)

        XCTAssertEqual(log.successes.map(\.text), [L10n.Profile.saveSuccess])
    }

    func testAnAvatarSaveConfirmsTheAvatarAndNotAlsoTheProfile() async {
        client.currentUserResult = .success(ProfileFixtures.user())
        client.updateResult = .success(())
        let vm = makeVM()
        await vm.refresh()
        let log = record()
        vm.pickAvatar(ProfileFixtures.image())

        await save(vm)

        XCTAssertEqual(log.successes.map(\.text), [L10n.EditProfile.photoUploadSuccess])
    }

    /// First-run completion is also a profile save, and it deliberately stays silent: a toast while the
    /// user is being handed back to what they were doing reads as friction. Android does the same.
    func testCompletingOnboardingConfirmsNothingAtAll() async {
        client.currentUserResult = .success(ProfileFixtures.user(phoneNumber: nil))
        client.updateResult = .success(())
        let vm = makeVM()
        await vm.refresh()
        let log = record()

        await vm.completeOnboarding(firstName: "Ada", lastName: "Lovelace", phoneNumber: "+420111", birthDate: nil)

        XCTAssertTrue(log.successes.isEmpty)
    }

    /// The stored NAME is what says a photo exists, not the URL beside it: that URL is a per-fetch SAS
    /// and a blank one would otherwise hide a photo the user is entitled to delete.
    func testAPhotoWhoseSignedUrlIsMissingIsStillRemovable() async {
        let unsigned = ProfilePhoto(fileName: "blob-1", blobURL: nil)
        client.currentUserResult = .success(ProfileFixtures.user(profilePhoto: unsigned))
        client.updateResult = .success(())
        let vm = makeVM()
        await vm.refresh()
        XCTAssertEqual(vm.editorAvatar, .initials, "an unsigned photo cannot be drawn this fetch")
        XCTAssertTrue(vm.canRemoveAvatar, "but it is still there to delete")
        let log = record()

        vm.removeAvatar()
        XCTAssertEqual(vm.avatarEdit, .removed)

        await save(vm)

        XCTAssertEqual(client.lastUpdate?.removePhoto, true)
        XCTAssertEqual(log.successes.map(\.text), [L10n.EditProfile.photoRemoveSuccess])
    }

    func testTheConfirmationIsAPureFunctionOfTheEdit() {
        XCTAssertEqual(AvatarSaveConfirmation.forEdit(.unchanged), nil)
        XCTAssertEqual(AvatarSaveConfirmation.forEdit(.removed), .removed)
        XCTAssertEqual(
            AvatarSaveConfirmation.forEdit(.picked(image: ProfileFixtures.image(), upload: Self.upload)),
            .uploaded
        )
    }

    private static let upload = ProfilePhotoUpload(base64: "QUJD", contentType: "image/jpeg", fileName: "photo.jpg")

    // MARK: - The confirmation copy, read out of the compiled bundle

    private static let uploadedCopy = [
        "en": "Profile photo updated",
        "cs": "Profilová fotka byla aktualizována",
        "sk": "Profilová fotka bola aktualizovaná",
        "uk": "Фото профілю оновлено",
        "ru": "Фото профиля обновлено"
    ]

    private static let removedCopy = [
        "en": "Profile photo removed",
        "cs": "Profilová fotka byla odstraněna",
        "sk": "Profilová fotka bola odstránená",
        "uk": "Фото профілю видалено",
        "ru": "Фото профиля удалено"
    ]

    /// Android's `profile_avatar_upload_success`/`_remove_success` and web's `pages.profile.avatar.*`
    /// verbatim, so all three platforms confirm the same save in the same words. Reads the COMPILED
    /// bundle: a value that survives the catalog but not the build fails here.
    func testBothConfirmationsCarryTheSharedWordingInEveryLocale() throws {
        let restore = L10n.bundle
        defer { L10n.bundle = restore }

        for (tag, expected) in Self.uploadedCopy {
            L10n.bundle = try localeBundle(tag)
            XCTAssertEqual(AvatarSaveConfirmation.uploaded.message, expected, "the upload confirmation in \(tag)")
        }
        for (tag, expected) in Self.removedCopy {
            L10n.bundle = try localeBundle(tag)
            XCTAssertEqual(AvatarSaveConfirmation.removed.message, expected, "the removal confirmation in \(tag)")
        }
    }

    /// Two outcomes of one tap: a single sentence for both tells the user nothing, and an untranslated
    /// key echoes back as the key.
    func testTheTwoConfirmationsStayDistinctAndTranslatedInEveryLocale() throws {
        let restore = L10n.bundle
        defer { L10n.bundle = restore }

        for tag in Self.uploadedCopy.keys {
            L10n.bundle = try localeBundle(tag)
            let uploaded = AvatarSaveConfirmation.uploaded.message
            let removed = AvatarSaveConfirmation.removed.message

            XCTAssertNotEqual(uploaded, removed, "one sentence serves both avatar outcomes in \(tag)")
            for message in [uploaded, removed] {
                XCTAssertFalse(message.isEmpty, "an empty avatar confirmation in \(tag)")
                XCTAssertFalse(message.hasPrefix("profile_photo_"), "\(message) is the key, not a translation (\(tag))")
            }
        }
    }

    private func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
    }

    /// Every snackbar the controller publishes, not just the one left standing: an optimistic success
    /// fired at pick time is overwritten by the save's error, so reading `current` alone would miss it.
    private func record() -> MessageLog {
        let log = MessageLog()
        log.cancellable = snackbar.$current.sink { message in
            if let message { log.messages.append(message) }
        }
        return log
    }

    private final class MessageLog {
        var cancellable: AnyCancellable?
        var messages: [SnackbarMessage] = []

        var successes: [SnackbarMessage] {
            messages.filter { $0.severity == .success }
        }
    }

    private func save(_ vm: ProfileViewModel) async {
        await vm.save(
            firstName: "Jane",
            lastName: "Doe",
            phoneNumber: "+420123456789",
            birthDate: nil,
            languageCode: "en"
        )
    }
}
