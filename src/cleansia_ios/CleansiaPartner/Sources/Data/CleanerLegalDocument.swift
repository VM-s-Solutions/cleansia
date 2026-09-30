import CleansiaCore
import CleansiaPartnerApi
import Foundation

/// One contract document a cleaner works under, as it is in force for their market: the text row the
/// acceptance echoes, and whether its current version is the one they accepted.
struct CleanerLegalDocument: Equatable, Identifiable {
    let type: LegalDocumentType
    let legalDocumentTextId: String
    let version: String
    let title: String
    let contentHtml: String
    let isAccepted: Bool
    let acceptedVersion: String?
    let acceptedAt: Date?

    var id: Int {
        type.rawValue
    }
}

extension CleanerLegalDocument {
    /// **Refuse.** The text id is what the acceptance echoes, the HTML is what is accepted, and
    /// `isAccepted` decides whether the cleaner may be approved and take work: a `false` made up from a
    /// null asks again for an acceptance already given, a `true` hides one still owed. What they last
    /// accepted stays nullable, because a cleaner who never accepted has neither.
    init(_ dto: CleanerLegalDocumentDto) throws {
        type = try dto.type.require("type")
        legalDocumentTextId = try dto.legalDocumentTextId.requireNonBlank("legalDocumentTextId")
        version = try dto.version.requireNonBlank("version")
        title = try dto.title.require("title")
        contentHtml = try dto.contentHtml.require("contentHtml")
        isAccepted = try dto.isAccepted.require("isAccepted")
        acceptedVersion = dto.acceptedVersion
        acceptedAt = dto.acceptedAt
    }
}
