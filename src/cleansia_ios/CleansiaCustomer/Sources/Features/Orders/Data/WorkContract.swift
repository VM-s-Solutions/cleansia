import CleansiaCore
import CleansiaCustomerApi
import Foundation

/// The contract for work a cleaner accepted for one seat of the customer's order, as the server
/// rendered it: the accepted document's text in the requested language, the job facts frozen at the
/// acceptance (never the live order), and the acceptance itself.
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
    /// **Refuse.** The HTML is what was accepted and the price, window and scope are what the
    /// acceptance binds, so a screen that showed "0 Kč" over an empty text would present a contract
    /// of nothing as the one the cleaner agreed to. The labels (number, currency code, location) stay
    /// nullable and render as absent.
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
