import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

final class PartnerDevicesClientTests: XCTestCase {
    private final class StubDeviceIdProvider: DeviceIdProviding {
        let deviceId: String
        init(deviceId: String) {
            self.deviceId = deviceId
        }
    }

    // D6: the client's currentDeviceId — the value fed into deviceMine's
    // currentDeviceId arg — comes from the injected DeviceIdProvider, the
    // SAME source the HeaderAdapter stamps as X-Device-Id. No second source.
    func testCurrentDeviceIdComesFromInjectedProvider() {
        let provider = StubDeviceIdProvider(deviceId: "stable-install-id")
        let client = LivePartnerDevicesClient(deviceIdProvider: provider)
        XCTAssertEqual(client.currentDeviceId, "stable-install-id")
    }

    // M1: a DTO with a valid id maps; rows with a nil or empty id are
    // dropped (unrevokable garbage), so deviceMine's compactMap omits them.
    func testMapperKeepsRowWithValidId() {
        let device = UserDevice(DeviceDto(id: "row-1", platform: "ios"))
        XCTAssertEqual(device?.id, "row-1")
    }

    func testMapperDropsRowWithNilId() {
        XCTAssertNil(UserDevice(DeviceDto(id: nil, platform: "ios")))
    }

    func testMapperDropsRowWithEmptyId() {
        XCTAssertNil(UserDevice(DeviceDto(id: "", platform: "ios")))
    }

    func testCompactMapDropsMalformedRowsFromList() {
        let mapped = [
            DeviceDto(id: "row-1", platform: "ios"),
            DeviceDto(id: nil, platform: "android"),
            DeviceDto(id: "", platform: "web")
        ].compactMap(UserDevice.init)
        XCTAssertEqual(mapped.map(\.id), ["row-1"])
    }
}
