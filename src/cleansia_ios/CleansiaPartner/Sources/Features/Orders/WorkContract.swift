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
    let totalPrice: Double
    let currencyCode: String?
    let locationApproximate: String?
    let rooms: Int
    let bathrooms: Int
    let services: [String]
    let packages: [String]
    let extraSlugs: [String]
}

struct WorkContractAcceptanceFacts: Equatable {
    let acceptedOn: Date
    let documentVersion: String
    let acceptedLanguage: String?
}

extension WorkContract {
    /// **Refuse.** The text id is what the take echoes, the HTML is what is accepted, and the price,
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
        totalPrice = try dto.totalPrice.require("facts.totalPrice")
        currencyCode = dto.currencyCode
        locationApproximate = dto.locationApproximate
        rooms = try dto.rooms.require("facts.rooms")
        bathrooms = try dto.bathrooms.require("facts.bathrooms")
        services = Self.names(dto.services)
        packages = Self.names(dto.packages)
        extraSlugs = dto.extraSlugs ?? []
    }

    private static func names(_ lines: [WorkContractFactsLine]?) -> [String] {
        (lines ?? []).compactMap { line in
            guard let name = line.name, !name.isBlank else { return nil }
            return name
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
