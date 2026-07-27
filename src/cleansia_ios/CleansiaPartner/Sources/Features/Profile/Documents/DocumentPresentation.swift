import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// One document type + how it renders. `label` is a closure, not a stored
/// `String`: the accessors read the string catalog, and the partner app can
/// switch language without relaunching, so the value has to be resolved at
/// render time rather than when the table is initialised.
struct DocumentTypeOption {
    let type: DocumentType
    let label: () -> String
}

/// The single source of truth for how a document's type and status are shown.
///
/// Both the upload picker and the document row read `types` / `typeLabel`, so
/// the two can never drift into showing different names for the same type —
/// the drift the Android screen used to have when its picker carried hardcoded
/// English literals next to a resource-backed row label.
enum DocumentPresentation {
    /// Wire order = `DocumentType.cs` (IdentityCard = 1 … Other = 10), which is
    /// also the picker's display order.
    ///
    /// Spelled out rather than derived from `DocumentType.allCases` on purpose:
    /// a regenerated client that gains an eleventh type must not reach the
    /// picker without a label. `DocumentPresentationTests` fails in that case.
    static let types: [DocumentTypeOption] = [
        DocumentTypeOption(type: ._1, label: { L10n.Profile.documentTypeIdentity }),
        DocumentTypeOption(type: ._2, label: { L10n.Profile.documentTypePassport }),
        DocumentTypeOption(type: ._3, label: { L10n.Profile.documentTypeDriversLicense }),
        DocumentTypeOption(type: ._4, label: { L10n.Profile.documentTypeWorkPermit }),
        DocumentTypeOption(type: ._5, label: { L10n.Profile.documentTypeContract }),
        DocumentTypeOption(type: ._6, label: { L10n.Profile.documentTypeCertificate }),
        DocumentTypeOption(type: ._7, label: { L10n.Profile.documentTypeBankStatement }),
        DocumentTypeOption(type: ._8, label: { L10n.Profile.documentTypeTax }),
        DocumentTypeOption(type: ._9, label: { L10n.Profile.documentTypeInsurance }),
        DocumentTypeOption(type: ._10, label: { L10n.Profile.documentTypeOther })
    ]

    /// Client-side ceiling for an upload. The server has its own limits; this
    /// exists so a 40 MB scan fails instantly instead of after a base64 upload.
    /// Mirrors the customer app's evidence cap (`DisputeFormConstants`).
    static let maxDocumentBytes = 10 * 1024 * 1024

    static let placeholder = "—"

    /// A table lookup rather than a ten-case `switch`: `swiftlint --strict`
    /// caps cyclomatic complexity at 10, and a switch over ten cases plus
    /// `.none` is 11. Do not "simplify" this back into a switch.
    static func typeLabel(_ type: DocumentType?) -> String {
        guard let type, let option = types.first(where: { $0.type == type }) else { return placeholder }
        return option.label()
    }

    static func statusLabel(_ status: DocumentStatus?) -> String {
        switch status {
        case ._1: L10n.Profile.documentStatusPending
        case ._2: L10n.Profile.documentStatusApproved
        case ._3: L10n.Profile.documentStatusRejected
        case .none: placeholder
        }
    }

    /// Matches `OrderStatusPill.tint` rather than Android's `StatusBadge`:
    /// `CleansiaColors` has no `tertiary`, and warningStar/successText/error is
    /// already the partner app's pending/good/bad triad.
    static func statusTint(_ status: DocumentStatus?) -> Color {
        switch status {
        case ._1: CleansiaColors.warningStar
        case ._2: CleansiaColors.successText
        case ._3: CleansiaColors.error
        case .none: CleansiaColors.onSurfaceVariant
        }
    }

    /// The dropdown speaks `String` ids; the wire speaks `Int`.
    static func optionId(_ type: DocumentType) -> String {
        String(type.rawValue)
    }

    static func type(forOptionId id: String?) -> DocumentType? {
        guard let id else { return nil }
        return types.first { optionId($0.type) == id }?.type
    }
}
