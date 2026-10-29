import CleansiaCore
import Foundation
import XCTest
@testable import CleansiaCustomer

@MainActor
private final class RegistrationRecorder: LiveActivityRegistering {
    var calls: [String] = []
    var result: ApiResult<Void> = .success(())
    var onCall: ((String) async -> ApiResult<Void>)?

    func register(orderId _: String, orderNumber _: String, token: String) async -> ApiResult<Void> {
        calls.append(token)
        return await onCall?(token) ?? result
    }

    func registerPushToStart(token: String) async -> ApiResult<Void> {
        await register(orderId: "", orderNumber: "", token: token)
    }

    func deregister(orderId _: String) async {}
}

@MainActor
final class LiveActivityRegistrationQueueTests: XCTestCase {
    private func drain() async {
        for _ in 0 ..< 30 {
            await Task.yield()
        }
    }

    func testTransientFailureUsesThreeAttemptsWithTwoAndFourSecondBackoffThenResumeRetriesLatest() async {
        let recorder = RegistrationRecorder()
        recorder.result = .failure(ApiError(httpStatus: 503))
        var pauses: [UInt64] = []
        let queue = LiveActivityRegistrationQueue(registrar: recorder, sleep: { pauses.append($0) })
        queue.submit(scope: .order("o1"), orderNumber: "1042", token: "latest")
        await drain()
        XCTAssertEqual(recorder.calls, ["latest", "latest", "latest"])
        XCTAssertEqual(pauses, [2_000_000_000, 4_000_000_000])

        recorder.result = .success(())
        queue.resume()
        queue.resume()
        await drain()
        XCTAssertEqual(recorder.calls, ["latest", "latest", "latest", "latest"])
        queue.resume()
        await drain()
        XCTAssertEqual(recorder.calls.count, 4, "an accepted token must not become a foreground heartbeat")
    }

    func testAcceptedTokenIsNotRegisteredAgainOnForegroundResumeOrRepeatedEmission() async {
        let recorder = RegistrationRecorder()
        let queue = LiveActivityRegistrationQueue(registrar: recorder)
        queue.submit(scope: .pushToStart, token: "accepted")
        await drain()
        queue.resume()
        queue.submit(scope: .pushToStart, token: "accepted")
        await drain()
        XCTAssertEqual(recorder.calls, ["accepted"])
    }

    func testPermanentRefusalDoesNotRetryOnResumeButNewTokenCanRegister() async {
        let recorder = RegistrationRecorder()
        recorder.result = .failure(ApiError(httpStatus: 409))
        let queue = LiveActivityRegistrationQueue(
            registrar: recorder,
            sleep: { _ in XCTFail("permanent refusal retried") }
        )
        queue.submit(scope: .pushToStart, token: "old")
        await drain()
        queue.resume()
        await drain()
        XCTAssertEqual(recorder.calls, ["old"])
        queue.submit(scope: .pushToStart, token: "new")
        await drain()
        XCTAssertEqual(recorder.calls, ["old", "new"])
    }

    func testRateLimitWaitsAtLeastThirtySecondsBeforeForegroundRetry() async {
        let recorder = RegistrationRecorder()
        recorder.result = .failure(ApiError(httpStatus: 429))
        var now = Date(timeIntervalSince1970: 100)
        let queue = LiveActivityRegistrationQueue(
            registrar: recorder,
            now: { now },
            sleep: { _ in XCTFail("429 retried immediately") }
        )
        queue.submit(scope: .pushToStart, token: "token")
        await drain()
        now.addTimeInterval(29)
        queue.resume()
        await drain()
        XCTAssertEqual(recorder.calls.count, 1)
        now.addTimeInterval(1)
        queue.resume()
        await drain()
        XCTAssertEqual(recorder.calls.count, 2)
    }

    func testRotationAndSessionWipeRejectLateResponsesAndCancelOldRetries() async {
        let recorder = RegistrationRecorder()
        var suspended: CheckedContinuation<ApiResult<Void>, Never>?
        recorder.onCall = { token in
            if token == "old" {
                return await withCheckedContinuation { suspended = $0 }
            }
            return .success(())
        }
        let queue = LiveActivityRegistrationQueue(registrar: recorder, sleep: { _ in XCTFail("stale token retried") })
        queue.submit(scope: .order("o1"), orderNumber: "1042", token: "old")
        await drain()
        queue.submit(scope: .order("o1"), orderNumber: "1042", token: "new")
        await drain()
        suspended?.resume(returning: .failure(ApiError(httpStatus: 503)))
        await drain()
        XCTAssertEqual(recorder.calls, ["old", "new"])
        queue.clear()
        queue.resume()
        await drain()
        XCTAssertEqual(recorder.calls, ["old", "new"])
    }

    func testCancelledRequestStopsWithoutRetryAndDuplicateEmissionsCoalesce() async {
        let recorder = RegistrationRecorder()
        recorder.result = .failure(ApiError(code: ApiError.cancelledCode))
        let queue = LiveActivityRegistrationQueue(
            registrar: recorder,
            sleep: { _ in XCTFail("cancelled request retried") }
        )
        queue.submit(scope: .pushToStart, token: "token")
        queue.submit(scope: .pushToStart, token: "token")
        await drain()
        queue.submit(scope: .pushToStart, token: "token")
        await drain()
        XCTAssertEqual(recorder.calls, ["token"])
    }
}
