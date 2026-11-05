import CleansiaCore
import CleansiaPartnerApi
import Foundation

/// A contract for work rendered for one job: the text row the take echoes, the facts the acceptance
/// binds, and — on a read of an accepted contract — the acceptance behind it. A preview carries no
/// `acceptance`; a read carries the facts as they were stored, never the live order.
struct WorkContract: Equatable {
    let legalDocumentTextId: String
    let version: String
    let language: String?
    let title: String?
    let contentHtml: String
    let facts: WorkContractJobFacts
    let acceptance: WorkContractAcceptanceFacts?
}

struct WorkContractJobFacts: Equatable {
    let orderNumber: String?
    let cleaningDateTimeUtc: Date
    let estimatedMinutes: Int
    /// The accepting cleaner's reward for one seat, which the contract prices the work at; the wire
    /// still names it `totalPrice`.
    let reward: Double
    let currencyCode: String?
    let locationApproximate: String?
    let rooms: Int
    let bathrooms: Int
    let services: [String]
    let packages: [String]
    let extraSlugs: [String]
    /// The id of each name in `services` / `packages`, index for index, so the take sheet can show the board
    /// row's translated name of the same service.
    var serviceIds: [String] = []
    var packageIds: [String] = []
}

struct WorkContractAcceptanceFacts: Equatable {
    let acceptedOn: Date
    let documentVersion: String
    let acceptedLanguage: String?
}

extension WorkContract {
    /// The versions whose clauses the four summary lines are true to (contract.md §3-6). A version not listed
    /// here — the lawyer's next text — shows no summary until the lines are checked against it.
    static let summaryVersions: Set<String> = ["2026-10-05", "2026-10-27"]
    /// The versions that carry the release and cash clauses the "free to cancel" and cash lines summarise.
    static let releaseVersions: Set<String> = ["2026-09-29", "2026-10-05", "2026-10-27"]
}

extension WorkContract {
    /// **Refuse.** The text id is what the take echoes, the HTML is what is accepted, and the reward,
    /// window and scope are what the acceptance binds — a null in any of them is a broken wire, and a
    /// sheet that showed "0 Kč" over an empty text would record an acceptance of nothing. The labels
    /// (number, currency code, location) stay nullable and render as absent.
    init(_ dto: WorkContractDto) throws {
        legalDocumentTextId = try dto.legalDocumentTextId.requireNonBlank("legalDocumentTextId")
        version = try dto.version.requireNonBlank("version")
        language = dto.language
        title = dto.title
        contentHtml = try dto.contentHtml.require("contentHtml")
        facts = try WorkContractJobFacts(dto.facts.require("facts"))
        acceptance = try dto.acceptance.map(WorkContractAcceptanceFacts.init)
    }
}

extension WorkContractJobFacts {
    init(_ dto: WorkContractFacts) throws {
        orderNumber = dto.orderNumber
        cleaningDateTimeUtc = try dto.cleaningDateTimeUtc.require("facts.cleaningDateTimeUtc")
        estimatedMinutes = try dto.estimatedMinutes.require("facts.estimatedMinutes")
        reward = try dto.totalPrice.require("facts.totalPrice")
        currencyCode = dto.currencyCode
        locationApproximate = dto.locationApproximate
        rooms = try dto.rooms.require("facts.rooms")
        bathrooms = try dto.bathrooms.require("facts.bathrooms")
        let services = Self.lines(dto.services)
        let packages = Self.lines(dto.packages)
        self.services = services.map(\.name)
        self.packages = packages.map(\.name)
        extraSlugs = dto.extraSlugs ?? []
        serviceIds = services.map(\.id)
        packageIds = packages.map(\.id)
    }

    /// The named lines, each name with its id, built in one pass so index *i* of the ids is the id of name *i*.
    private static func lines(_ lines: [WorkContractFactsLine]?) -> [(id: String, name: String)] {
        (lines ?? []).compactMap { line in
            guard let name = line.name, !name.isBlank else { return nil }
            return (line.id ?? "", name)
        }
    }
}

extension WorkContractAcceptanceFacts {
    init(_ dto: WorkContractAcceptanceDetails) throws {
        acceptedOn = try dto.acceptedOn.require("acceptance.acceptedOn")
        documentVersion = try dto.documentVersion.requireNonBlank("acceptance.documentVersion")
        acceptedLanguage = dto.acceptedLanguage
    }
}

#if DEBUG
    extension WorkContract {
        /// The previews' contract (the detail's photo and note previews hand it out as their preview).
        static let preview = WorkContract(
            legalDocumentTextId: "text-1",
            version: "2026-10-27",
            language: "en",
            title: "Contract for work",
            contentHtml: "<p>This contract for work is concluded between the operating company (the client)"
                + " and you (the contractor).</p>",
            facts: WorkContractJobFacts(
                orderNumber: "CL-2026-0042",
                cleaningDateTimeUtc: Date(timeIntervalSince1970: 1_786_200_000),
                estimatedMinutes: 180,
                reward: 620,
                currencyCode: "CZK",
                locationApproximate: "Praha 4",
                rooms: 3,
                bathrooms: 1,
                services: ["Standard cleaning"],
                packages: [],
                extraSlugs: ["inside-oven"]
            ),
            acceptance: nil
        )
    }
#endif
