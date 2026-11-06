import CleansiaCore
import XCTest
@testable import CleansiaCustomer

@MainActor
final class BookingCashSessionTests: XCTestCase {
    private func makeVM(
        tokens: FakeTokenStore,
        cards: FakeSavedCardClient,
        generation: @escaping () -> UInt = { 0 }
    ) async -> BookingViewModel {
        let vm = BookingViewModel(
            quoteClient: FakeQuoteClient(result: .success(BookingQuote(
                totalPrice: 900, currencyCode: "CZK", requiredEmployees: 1
            ))),
            tokenStore: tokens, savedCardClient: cards, cashSessionGeneration: generation
        )
        vm.update { _ in
            var state = BookingState()
            state.selectedServiceIds = ["s-1"]
            return state
        }
        await vm.refreshQuoteForTest()
        return vm
    }

    func testSuccessfulCardReadSurvivesSameAccount401CredentialRotation() async throws {
        let tokens = FakeTokenStore.signedIn()
        let rotated = FakeTokenStore.jwt(nonce: "rotated")
        let bridge = GeneratedClientAuthBridge(
            headerAdapter: HeaderAdapter(deviceIdProvider: FixedDeviceId(), deviceLabel: "Test"),
            tokenStore: tokens,
            sessionRefresher: SessionRefresher(
                tokenStore: tokens, refreshClient: RotatingRefresher(access: rotated),
                sessionManager: SessionManager(), sessionScopedCaches: SessionScopedCacheRegistry()
            )
        )
        let cards = FakeSavedCardClient(reads: [.success([PaymentsFixtures.czkCard])])
        var attempts = 0
        var retried: Bool?
        cards.beforeRead = {
            retried = try? await bridge.executeWithRetry(attempt: {
                attempts += 1
                if attempts == 1 { throw ApiError(httpStatus: 401) }
                return true
            }, unauthorizedStatus: { ($0 as? ApiError)?.httpStatus })
        }
        let vm = await makeVM(tokens: tokens, cards: cards)
        let result = await vm.refreshCashCard()
        guard case .success = result else { return XCTFail("same-account refreshed response was discarded") }
        XCTAssertEqual(attempts, 2)
        XCTAssertEqual(retried, true)
        XCTAssertEqual(tokens.current()?.accessToken, rotated)
        XCTAssertEqual(vm.cashEligibility, .available)
        XCTAssertFalse(vm.cashCardReadFailed)

        try tokens.save(XCTUnwrap(FakeTokenStore.signedIn(accessToken: FakeTokenStore.jwt(nonce: "later")).current()))
        XCTAssertEqual(vm.cashEligibility, .available, "an unrelated same-account refresh hid the saved card")
    }

    func testDifferentAccountCannotReceiveAnEarlierAccountsCardResponse() async throws {
        let tokens = FakeTokenStore.signedIn()
        let cards = FakeSavedCardClient(reads: [.success([PaymentsFixtures.czkCard])])
        let vm = await makeVM(tokens: tokens, cards: cards)
        let other = try XCTUnwrap(FakeTokenStore.signedIn(accessToken: FakeTokenStore.jwt(userId: "customer-2"))
            .current())
        cards.beforeRead = { tokens.save(other) }
        let result = await vm.refreshCashCard()
        guard case .failure = result else { return XCTFail("previous account's card response was accepted") }
        XCTAssertNil(vm.cashCards)
        XCTAssertEqual(vm.cashEligibility, .cardCheckFailed)
        vm.selectPayment(.cash)
        XCTAssertNil(vm.state.paymentMethod)
    }

    func testNewLoginEpochRejectsSameAccountResponseAndCachedCards() async {
        let tokens = FakeTokenStore.signedIn()
        let cards = FakeSavedCardClient(reads: [.success([PaymentsFixtures.czkCard])])
        let captures = SavedCardCaptureStore()
        let vm = await makeVM(tokens: tokens, cards: cards, generation: { captures.generation })
        await vm.refreshCashCard()
        XCTAssertEqual(vm.cashEligibility, .available)
        await captures.clear()
        XCTAssertEqual(vm.cashEligibility, .pending)
        cards.beforeRead = { await captures.clear() }
        let result = await vm.refreshCashCard()
        guard case .failure = result else { return XCTFail("response crossed the login epoch") }
        XCTAssertNil(vm.cashCards)
    }
}

private struct FixedDeviceId: DeviceIdProviding {
    let deviceId = "test-device"
}

private struct RotatingRefresher: AuthRefreshing {
    let access: String
    func refresh(refreshToken _: String) async -> RefreshCallResult {
        let future = Date(timeIntervalSinceNow: 9999)
        return .refreshed(RefreshedTokens(
            accessToken: access, accessTokenExpiresAt: future,
            refreshToken: "rotated-refresh", refreshTokenExpiresAt: future
        ))
    }
}
