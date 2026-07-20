import CleansiaCore
import CleansiaCustomerApi
import Foundation

/// Boundary onto the generated `CustomerLiveActivityAPI` (POST /api/LiveActivity/Register,
/// DELETE /api/LiveActivity/{orderId}). The live impl routes through the same hand-written auth/base-url
/// spine every other customer client uses — `CustomerCoreSpineRequestBuilderFactory` supplies the session,
/// Authorization header, and base path (ADR-0013). Split out only so the registrar's command mapping is
/// unit-testable without a socket.
protocol LiveActivityApi: Sendable {
    func register(_ command: RegisterLiveActivityTokenCommand) async throws
    func unregister(orderId: String, deviceId: String) async throws
}

struct GeneratedLiveActivityApi: LiveActivityApi {
    func register(_ command: RegisterLiveActivityTokenCommand) async throws {
        _ = try await CustomerLiveActivityAPI.liveActivityRegister(registerLiveActivityTokenCommand: command)
    }

    func unregister(orderId: String, deviceId: String) async throws {
        _ = try await CustomerLiveActivityAPI.liveActivityUnregister(orderId: orderId, deviceId: deviceId)
    }
}

/// Live `LiveActivityRegistering`: hands ActivityKit push tokens to the backend so it can drive the
/// in-progress-clean Live Activity (ADR-0029 LA-5). `deviceId` is the same keychain id used for
/// Device/Register. A non-nil `orderId` is a per-activity update token; nil is the per-install
/// push-to-start token. Every call swallows failure — a registration that never lands must never crash;
/// the local activity keeps running, unpushed.
struct CustomerLiveActivityRegistrar: LiveActivityRegistering {
    private let api: LiveActivityApi
    private let deviceIdProvider: DeviceIdProviding

    init(deviceIdProvider: DeviceIdProviding, api: LiveActivityApi = GeneratedLiveActivityApi()) {
        self.deviceIdProvider = deviceIdProvider
        self.api = api
    }

    func register(orderId: String, orderNumber _: String, token: String) async {
        await send(orderId: orderId, token: token)
    }

    func registerPushToStart(token: String) async {
        await send(orderId: nil, token: token)
    }

    func deregister(orderId: String) async {
        _ = await apiResult(mapError: ApiError.fromGenerated) {
            try await api.unregister(orderId: orderId, deviceId: deviceIdProvider.deviceId)
        }
    }

    private func send(orderId: String?, token: String) async {
        _ = await apiResult(mapError: ApiError.fromGenerated) {
            try await api.register(RegisterLiveActivityTokenCommand(
                deviceId: deviceIdProvider.deviceId,
                token: token,
                orderId: orderId
            ))
        }
    }
}
