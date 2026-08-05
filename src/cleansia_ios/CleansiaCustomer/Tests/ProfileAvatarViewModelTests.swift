import CleansiaCore
import CleansiaCustomerApi
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
