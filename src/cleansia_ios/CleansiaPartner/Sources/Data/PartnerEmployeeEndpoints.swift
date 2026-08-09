import CleansiaCore
import CleansiaPartnerApi
import Foundation

/// `EmployeeItem.jobRadiusKm` and `PUT /api/Employee/UpdateJobRadius` are on the wire but not in the
/// committed mobile spec, so the generated client decodes neither. Both requests are built from the
/// generated `requestBuilderFactory`, which is the ADR-0019 Core spine — the Bearer, the device
/// headers and the single-flight 401 refresh are stamped by it, not here. Delete this file and read
/// `employee.jobRadiusKm` directly once `manual_step: mobile-spec-regen` lands.
enum PartnerEmployeeEndpoints {
    static func getCurrentEmployeeProfile() async throws -> EmployeeProfile {
        try await builder(
            EmployeeProfile.self,
            method: "GET",
            urlString: PartnerEmployeeAPI.employeeGetCurrentEmployeeWithRequestBuilder().URLString,
            parameters: nil
        ).execute().body
    }

    static func updateJobRadius(_ command: UpdateJobRadiusCommand) async throws -> UpdateJobRadiusResponse {
        try await builder(
            UpdateJobRadiusResponse.self,
            method: "PUT",
            urlString: CleansiaPartnerApiAPI.basePath + updateJobRadiusPath,
            parameters: JSONEncodingHelper.encodingParameters(forEncodableObject: command)
        ).execute().body
    }

    static let updateJobRadiusPath = "/api/Employee/UpdateJobRadius"

    private static func builder<T: Decodable>(
        _: T.Type,
        method: String,
        urlString: String,
        parameters: [String: Any]?
    ) -> RequestBuilder<T> {
        let builderType: RequestBuilder<T>.Type = CleansiaPartnerApiAPI.requestBuilderFactory.getBuilder()
        return builderType.init(
            method: method,
            URLString: urlString,
            parameters: parameters,
            headers: ["Content-Type": "application/json"],
            requiresAuthentication: false
        )
    }
}

/// The generated `EmployeeItem` plus the one field the stale spec drops, decoded from the same
/// response so seeding the radius control costs no extra round trip.
struct EmployeeProfile: Decodable, Equatable {
    let employee: EmployeeItem
    let jobRadiusKm: Int?

    init(employee: EmployeeItem, jobRadiusKm: Int? = nil) {
        self.employee = employee
        self.jobRadiusKm = jobRadiusKm
    }

    private enum CodingKeys: String, CodingKey {
        case jobRadiusKm
    }

    init(from decoder: Decoder) throws {
        employee = try EmployeeItem(from: decoder)
        jobRadiusKm = try decoder.container(keyedBy: CodingKeys.self)
            .decodeIfPresent(Int.self, forKey: .jobRadiusKm)
    }
}

struct UpdateJobRadiusCommand: Encodable, Equatable {
    let employeeId: String?
    let radiusKm: Int?

    /// Explicit over `encodeIfPresent`: clearing the radius is a choice the customer-facing digest
    /// reads as "country-wide", so the body has to carry `null` rather than leave the field out.
    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(employeeId, forKey: .employeeId)
        try container.encode(radiusKm, forKey: .radiusKm)
    }

    private enum CodingKeys: String, CodingKey {
        case employeeId
        case radiusKm
    }
}

struct UpdateJobRadiusResponse: Decodable, Equatable {
    let employeeId: String?
    let radiusKm: Int?
}
