import CleansiaCore
import Foundation
@testable import CleansiaCustomer

/// Holds an async caller until a test opens it, so a reply can be made to arrive AFTER the screen has
/// moved on — the only way to prove a stale answer is dropped rather than rendered.
final class AsyncGate: @unchecked Sendable {
    private let lock = NSLock()
    private var isOpen = false
    private var waiters: [CheckedContinuation<Void, Never>] = []

    func wait() async {
        await withCheckedContinuation { continuation in
            lock.lock()
            if isOpen {
                lock.unlock()
                continuation.resume()
                return
            }
            waiters.append(continuation)
            lock.unlock()
        }
    }

    func open() {
        lock.lock()
        isOpen = true
        let pending = waiters
        waiters.removeAll()
        lock.unlock()
        pending.forEach { $0.resume() }
    }
}

enum GuestOrderFixtures {
    static let key = GuestOrderKey(pasted: "tok-1")

    static func order(id: String = "o-1", statusValue: Int = 2, currencyCode: String = "EUR") -> GuestOrder {
        GuestOrder(
            id: id,
            displayOrderNumber: "CZ-123",
            cleaningDateTime: Date(timeIntervalSince1970: 1_800_000_000),
            totalPrice: 90,
            currencyCode: currencyCode,
            statusValue: statusValue
        )
    }

    static func quote(
        orderId: String = "o-1",
        tier: CancellationTier = .partial,
        currencyCode: String? = "EUR"
    ) -> GuestCancellationQuote {
        GuestCancellationQuote(
            orderId: orderId,
            quote: CancellationQuote(
                tier: tier,
                feeAmount: 22.5,
                refundAmount: 67.5,
                currencyCode: currencyCode,
                forfeitsExpressWaiver: false,
                oopsWindowMinutes: 15
            )
        )
    }

    static func receipt(refundInitiated: Bool = true, actualRefundAmount: Double? = 12) -> GuestOrderCancellation {
        GuestOrderCancellation(
            refundAmount: 67.5,
            refundInitiated: refundInitiated,
            actualRefundAmount: actualRefundAmount
        )
    }
}

final class FakeGuestOrderClient: GuestOrderClient, @unchecked Sendable {
    var lookupResult: ApiResult<GuestOrder> = .success(GuestOrderFixtures.order())
    var lookupGate: AsyncGate?
    private(set) var lookupKeys: [GuestOrderKey] = []

    var quoteResult: ApiResult<GuestCancellationQuote> = .success(GuestOrderFixtures.quote())
    var quoteGate: AsyncGate?
    private(set) var quoteCallCount = 0

    var cancelResult: ApiResult<GuestOrderCancellation> = .success(GuestOrderFixtures.receipt())
    var cancelGate: AsyncGate?
    private(set) var cancelCalls: [(key: GuestOrderKey, reason: String?, language: String)] = []

    /// Each call answers with the result configured WHEN IT WAS MADE, so a gated call that completes
    /// after the test has reconfigured the fake still returns the stale answer it was meant to.
    func lookup(_ key: GuestOrderKey) async -> ApiResult<GuestOrder> {
        lookupKeys.append(key)
        let result = lookupResult
        await lookupGate?.wait()
        return result
    }

    func cancellationQuote(_: GuestOrderKey) async -> ApiResult<GuestCancellationQuote> {
        quoteCallCount += 1
        let result = quoteResult
        await quoteGate?.wait()
        return result
    }

    func cancel(_ key: GuestOrderKey, reason: String?, language: String) async -> ApiResult<GuestOrderCancellation> {
        cancelCalls.append((key, reason, language))
        let result = cancelResult
        await cancelGate?.wait()
        return result
    }
}

/// Answers with the raw server key so a test can tell a localized refusal from a transport fault.
struct KeyEchoLocalizer: ApiErrorLocalizing {
    func message(for error: ApiError) -> String {
        error.code ?? error.message ?? "status:\(error.httpStatus.map(String.init) ?? "nil")"
    }
}
