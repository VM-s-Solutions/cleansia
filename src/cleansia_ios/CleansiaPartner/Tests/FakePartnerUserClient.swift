import CleansiaCore
import Foundation
import XCTest
@testable import CleansiaPartner

/// Locked rather than `@MainActor`: the language push runs in a task of its own, off the main actor,
/// so the recording is written from the cooperative pool while the test reads it.
final class FakePartnerUserClient: PartnerUserClient, @unchecked Sendable {
    struct Update: Equatable {
        let firstName: String
        let lastName: String
        let phoneNumber: String
        let birthDate: Date?
        let languageCode: String?
    }

    let read = XCTestExpectation(description: "profile read")
    let pushed = XCTestExpectation(description: "language pushed")

    private let lock = NSLock()
    private var storedUser: ApiResult<CurrentUser> = .success(CurrentUser.stub())
    private var storedUpdateResult: ApiResult<Void> = .success(())
    private var storedReads = 0
    private var storedUpdates: [Update] = []
    private var storedSawCallersTaskContext = false

    var currentUser: ApiResult<CurrentUser> {
        get { lock.withLock { storedUser } }
        set { lock.withLock { storedUser = newValue } }
    }

    var updateResult: ApiResult<Void> {
        get { lock.withLock { storedUpdateResult } }
        set { lock.withLock { storedUpdateResult = newValue } }
    }

    var reads: Int {
        lock.withLock { storedReads }
    }

    var updates: [Update] {
        lock.withLock { storedUpdates }
    }

    /// True when the round trip observed the task-local the caller bound around it — i.e. it ran as a
    /// continuation of the screen's context rather than in a task of the seam's own.
    var sawCallersTaskContext: Bool {
        lock.withLock { storedSawCallersTaskContext }
    }

    func getCurrentUser() async -> ApiResult<CurrentUser> {
        lock.withLock {
            storedReads += 1
            storedSawCallersTaskContext = storedSawCallersTaskContext || CallerTaskMarker.isSet
        }
        read.fulfill()
        guard await roundTripCompletes() else { return .failure(ApiError(code: ApiError.cancelledCode)) }
        return currentUser
    }

    func updateCurrentUser(
        firstName: String,
        lastName: String,
        phoneNumber: String,
        birthDate: Date?,
        languageCode: String?
    ) async -> ApiResult<Void> {
        guard await roundTripCompletes() else { return .failure(ApiError(code: ApiError.cancelledCode)) }
        lock.withLock {
            storedUpdates.append(Update(
                firstName: firstName,
                lastName: lastName,
                phoneNumber: phoneNumber,
                birthDate: birthDate,
                languageCode: languageCode
            ))
        }
        pushed.fulfill()
        return updateResult
    }

    /// `URLSession` abandons an in-flight request when its task is cancelled; a fake that ignored
    /// cancellation would make every statement about who owns the push vacuously true.
    private func roundTripCompletes() async -> Bool {
        try? await Task.sleep(nanoseconds: 2_000_000)
        return !Task.isCancelled
    }
}

/// Stands in for whatever a screen binds around the tap — a value that only a task descended from the
/// caller's can see.
enum CallerTaskMarker {
    @TaskLocal static var isSet = false
}

extension CurrentUser {
    static func stub(
        firstName: String? = "Ondrej",
        lastName: String? = "Novak",
        phoneNumber: String? = "+420777111222",
        birthDate: Date? = Date(timeIntervalSince1970: 400_000_000),
        preferredLanguageCode: String? = "en"
    ) -> CurrentUser {
        CurrentUser(
            email: "ondrej@example.com",
            firstName: firstName,
            lastName: lastName,
            phoneNumber: phoneNumber,
            birthDate: birthDate,
            preferredLanguageCode: preferredLanguageCode
        )
    }
}

final class SessionTokenStore: TokenStore, @unchecked Sendable {
    private let lock = NSLock()
    private var stored: AuthTokens?

    init(signedIn: Bool) {
        if signedIn {
            let future = Date(timeIntervalSinceNow: 9999)
            stored = AuthTokens(
                accessToken: "access",
                accessTokenExpiresAt: future,
                refreshToken: "refresh",
                refreshTokenExpiresAt: future
            )
        }
    }

    func current() -> AuthTokens? {
        lock.withLock { stored }
    }

    func save(_ tokens: AuthTokens) {
        lock.withLock { stored = tokens }
    }

    func clear() {
        lock.withLock { stored = nil }
    }
}
