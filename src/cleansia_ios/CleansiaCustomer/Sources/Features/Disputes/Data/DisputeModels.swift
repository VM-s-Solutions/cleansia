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
    func toDetail() -> DisputeDetail? {
        guard let id, !id.isEmpty else { return nil }
        return DisputeDetail(
            id: id,
            displayOrderNumber: displayOrderNumber,
            reasonName: reason?.name,
            description: description,
            statusName: status?.name,
            statusValue: status?.value,
            createdOn: createdOn,
            messages: (messages ?? []).enumerated().compactMap { $1.toMessage(fallbackId: $0) },
            evidence: (evidence ?? []).compactMap { $0.toEvidence() }
        )
    }
}

extension DisputeMessageDto {
    func toMessage(fallbackId: Int) -> DisputeMessage {
        DisputeMessage(
            id: id ?? "message-\(fallbackId)",
            body: message,
            isStaffMessage: isStaffMessage ?? false,
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
