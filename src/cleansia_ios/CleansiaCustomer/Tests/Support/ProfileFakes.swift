import CleansiaCore
import Combine
import Foundation
@testable import CleansiaCustomer

final class FakeGdprDeleteClient: GdprDeleteClient, @unchecked Sendable {
    var deleteResult: ApiResult<Void> = .success(())
    private(set) var deleteCallCount = 0

    func deleteMyAccount() async -> ApiResult<Void> {
        // Force a real suspension. The @MainActor VM sets .submitting synchronously before awaiting this,
        // so yielding here lets a concurrent re-entry deterministically observe the in-flight state and be
        // dropped by the guard. Without it the call can complete inline (no actor hop), letting the first
        // delete finish and reset state before the second checks the guard — which flaked the reentry test.
        await Task.yield()
        deleteCallCount += 1
        return deleteResult
    }
}

@MainActor
final class FakeAuthClient: AuthClient {
    private(set) var signOutLocalCount = 0
    private(set) var logoutCount = 0

    func signOutLocal() async {
        signOutLocalCount += 1
    }

    func logout() async {
        logoutCount += 1
    }
}

final class FakeUserProfileClient: UserProfileClient, @unchecked Sendable {
    var currentUserResult: ApiResult<CurrentUserProfile> = .success(ProfileFixtures.user())
    private(set) var currentUserCallCount = 0

    var updateResult: ApiResult<Void> = .success(())
    private(set) var updateCallCount = 0
    private(set) var lastUpdate: ProfileUpdate?

    func currentUser() async -> ApiResult<CurrentUserProfile> {
        currentUserCallCount += 1
        return currentUserResult
    }

    func updateCurrentUser(_ update: ProfileUpdate) async -> ApiResult<Void> {
        updateCallCount += 1
        lastUpdate = update
        return updateResult
    }
}

final class FakeCustomerDevicesClient: CustomerDevicesClient, @unchecked Sendable {
    var currentDeviceIdValue = "device-current"
    var myDevicesResult: ApiResult<[UserDevice]> = .success([])
    var revokeResult: ApiResult<Void> = .success(())
    private(set) var revokedRowIds: [String] = []
    private(set) var sentCurrentDeviceIds: [String] = []

    var currentDeviceId: String {
        currentDeviceIdValue
    }

    func myDevices() async -> ApiResult<[UserDevice]> {
        sentCurrentDeviceIds.append(currentDeviceIdValue)
        return myDevicesResult
    }

    func revoke(rowId: String) async -> ApiResult<Void> {
        revokedRowIds.append(rowId)
        return revokeResult
    }
}

final class FakeNotificationPreferencesClient: NotificationPreferencesClient, @unchecked Sendable {
    var getMineResult: ApiResult<NotificationPreferences> = .success(ProfileFixtures.preferences())
    var updateResult: ApiResult<NotificationPreferences>?
    private(set) var getMineCallCount = 0
    private(set) var updatedPayloads: [NotificationPreferences] = []

    func getMine() async -> ApiResult<NotificationPreferences> {
        getMineCallCount += 1
        return getMineResult
    }

    func update(_ preferences: NotificationPreferences) async -> ApiResult<NotificationPreferences> {
        updatedPayloads.append(preferences)
        return updateResult ?? .success(preferences)
    }
}

final class FakeChangePasswordClient: ChangePasswordClient, @unchecked Sendable {
    var requestCodeResult: ApiResult<Void> = .success(())
    var changePasswordResult: ApiResult<Void> = .success(())
    private(set) var requestedEmails: [String] = []
    private(set) var changeCalls: [(email: String, code: String, newPassword: String)] = []

    func requestCode(email: String, language _: String) async -> ApiResult<Void> {
        requestedEmails.append(email)
        return requestCodeResult
    }

    func changePassword(email: String, code: String, newPassword: String) async -> ApiResult<Void> {
        changeCalls.append((email, code, newPassword))
        return changePasswordResult
    }
}

enum ProfileFixtures {
    static func user(
        id: String = "user-1",
        email: String = "jane@example.com",
        firstName: String = "Jane",
        lastName: String = "Doe",
        phoneNumber: String? = "+420123456789"
    ) -> CurrentUserProfile {
        CurrentUserProfile(
            id: id,
            email: email,
            firstName: firstName,
            lastName: lastName,
            phoneNumber: phoneNumber,
            birthDate: nil,
            preferredLanguageCode: "en",
            isEmailConfirmed: true
        )
    }

    static func preferences(promo: Bool = false) -> NotificationPreferences {
        NotificationPreferences(
            orderUpdates: true,
            cleanerOnTheWay: true,
            orderCompleted: true,
            orderCancelled: true,
            refundIssued: true,
            membershipExpiring: true,
            membershipCancelled: true,
            tierUpgrade: true,
            promo: promo,
            disputeReply: true,
            recurringScheduled: true
        )
    }
}
