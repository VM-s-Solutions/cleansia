import Foundation

/// Neither mobile spec declares a `required` array, so the generator types every property
/// optional-with-null regardless of `nullable: false` and the contract lands in the hand-written
/// mapper. A null in a field the spec marks non-nullable is a renamed or broken wire field, never a
/// zero — "0 Kč" to a cleaner who earned 4,800 is worse than an error screen.
public struct WireContractViolation: Error, Equatable {
    public let field: String

    public init(field: String) {
        self.field = field
    }
}

public extension Optional {
    /// Refuses the payload rather than supplying a value for it, naming the field that broke.
    /// `try dto.totalPay.require("totalPay")` inside an ``apiResult`` body is the whole idiom: Swift's
    /// `throws` carries the refusal and the one shared wrapper decides what it becomes, so no call site
    /// gets to answer that question differently.
    func require(_ field: String) throws -> Wrapped {
        guard let value = self else { throw WireContractViolation(field: field) }
        return value
    }
}

public extension String? {
    /// The identity flavour. ASP.NET serializes a missing string as `""`, not null, so for a field the
    /// client navigates by, blank is the same absence as nil.
    func requireNonBlank(_ field: String) throws -> String {
        let value = try require(field)
        guard !value.isBlank else { throw WireContractViolation(field: field) }
        return value
    }
}

public extension ApiError {
    static let wireContractCode = "wire.contract_violation"

    /// A 200 whose body breaks the contract is a **server** fault, so it is attributed as one:
    /// `network.*` would tell the cleaner to check their connection and send an investigator to the
    /// one subsystem that did not fail.
    ///
    /// The field name and the endpoint ride in `message`. iOS has **no error-reporting sink** today —
    /// no Sentry, no Crashlytics, and Firebase is Messaging only — so the name reaches nobody yet; it
    /// is carried for the day a sink exists, because a name preserved can be routed later and a name
    /// lost cannot. The user sees the generic line: `error.wire.contract_violation` resolves in the
    /// catalog, so ``ApiErrorLocalizer`` never falls through to the raw key.
    init(_ violation: WireContractViolation, endpoint: String) {
        self.init(
            code: Self.wireContractCode,
            message: "\(violation.field) is null but the mobile API contract declares it non-nullable"
                + " (\(endpoint))",
            httpStatus: 200
        )
    }
}
