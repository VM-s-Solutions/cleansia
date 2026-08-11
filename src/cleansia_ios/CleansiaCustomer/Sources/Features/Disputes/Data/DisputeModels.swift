import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct DisputesPage: Equatable {
    let items: [DisputeListEntry]
    let total: Int
}

struct DisputeListEntry: Equatable, Identifiable {
    let id: String
    let displayOrderNumber: String?
    let reasonName: String?
    let statusName: String?
    let statusValue: Int?
    let createdOn: Date?
}

struct DisputeDetail: Equatable {
    let id: String
    let displayOrderNumber: String?
    let reasonName: String?
    let description: String?
    let statusName: String?
    let statusValue: Int?
    let createdOn: Date?
    let messages: [DisputeMessage]
    let evidence: [DisputeEvidence]

    var allowsMessages: Bool {
        DisputeMessagePolicy.allowsMessages(statusValue: statusValue)
    }
}

struct DisputeMessage: Equatable, Identifiable {
    let id: String
    let body: String?
    let isStaffMessage: Bool
    let createdOn: Date?
}

struct DisputeEvidence: Equatable, Identifiable {
    let id: String
    let fileName: String?
    let blobURL: String?
    let uploadedOn: Date?

    var kind: EvidenceKind {
        EvidenceKind(fileName: fileName)
    }
}

enum EvidenceKind {
    case image
    case pdf
    case other

    init(fileName: String?) {
        let ext = (fileName as NSString?)?.pathExtension.lowercased() ?? ""
        switch ext {
        case "jpg", "jpeg", "png", "webp", "gif": self = .image
        case "pdf": self = .pdf
        default: self = .other
        }
    }
}

extension PagedDataOfDisputeListItem {
    func toDisputesPage() -> DisputesPage {
        DisputesPage(
            items: (data ?? []).compactMap { $0.toEntry() },
            total: total ?? 0
        )
    }
}

extension DisputeListItem {
    func toEntry() -> DisputeListEntry? {
        guard let id, !id.isEmpty else { return nil }
        return DisputeListEntry(
            id: id,
            displayOrderNumber: displayOrderNumber,
            reasonName: reason?.name,
            statusName: status?.name,
            statusValue: status?.value,
            createdOn: createdOn
        )
    }
}

extension DisputeDetails {
    func toDetail() throws -> DisputeDetail? {
        guard let id, !id.isEmpty else { return nil }
        return try DisputeDetail(
            id: id,
            displayOrderNumber: displayOrderNumber,
            reasonName: reason?.name,
            description: description,
            statusName: status?.name,
            statusValue: status?.value,
            createdOn: createdOn,
            messages: (messages ?? []).enumerated().map { try $1.toMessage(fallbackId: $0) },
            evidence: (evidence ?? []).compactMap { $0.toEvidence() }
        )
    }
}

extension DisputeMessageDto {
    /// **Refuse the thread.** `isStaffMessage` is which side of the conversation a bubble is drawn
    /// on, and `false` is a claim: coerced, a reply from support is attributed to the customer, who
    /// then reads their own dispute as one nobody answered.
    ///
    /// The id keeps its positional fallback. It is a `ForEach` key and nothing else — no mutation
    /// addresses a message — so a synthesized one identifies a row on screen rather than a row on
    /// the server.
    func toMessage(fallbackId: Int) throws -> DisputeMessage {
        try DisputeMessage(
            id: id ?? "message-\(fallbackId)",
            body: message,
            isStaffMessage: isStaffMessage.require("isStaffMessage"),
            createdOn: createdOn
        )
    }
}

extension DisputeEvidenceDto {
    func toEvidence() -> DisputeEvidence? {
        guard let id, !id.isEmpty else { return nil }
        return DisputeEvidence(
            id: id,
            fileName: fileName,
            blobURL: blobUrl,
            uploadedOn: uploadedOn
        )
    }
}
