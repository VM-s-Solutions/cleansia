import CleansiaPartnerApi
import Foundation

extension ContractStatus {
    static let pending = ContractStatus._1
    static let active = ContractStatus._2
    static let terminated = ContractStatus._3
    static let approved = ContractStatus._4
    static let rejected = ContractStatus._5
}

func isRegistrationComplete(_ status: RegistrationCompletionStatus) -> Bool {
    status.hasCompletedProfile == true &&
        status.areDocumentsUploaded == true &&
        (status.contractStatus == .approved || status.contractStatus == .active)
}

enum RegistrationStepCategory {
    case profile
    case documents
    case legalDocuments
    case approval
}

enum RegistrationStepStatus {
    case done
    case pending
    case missing
}

enum RegistrationStepDetail: Equatable {
    case documentsRequired
    case approvalRejected
    case approvalAwaitingReview
    case approvalCompleteProfileFirst
    case missingField(String)
}

struct RegistrationStep: Equatable {
    let category: RegistrationStepCategory
    let status: RegistrationStepStatus
    let details: [RegistrationStepDetail]
}

/// A row opens whatever its status, because the lock replaces the whole app until approval: a finished
/// section has no other way back in, and "Documents: Done" means one active document, not every type
/// approval needs. Approval is the admin's decision, so its row has nothing to open.
/// -> /partner-app/onboarding#registration-lock-screen
func isFixable(_ step: RegistrationStep) -> Bool {
    switch step.category {
    case .profile, .documents, .legalDocuments: true
    case .approval: false
    }
}

/// The contract-documents step appears only while a document is in force; until then approval does
/// not wait on it.
func buildSteps(
    _ status: RegistrationCompletionStatus?,
    legalDocuments: [CleanerLegalDocument]? = nil
) -> [RegistrationStep] {
    let profileDone = status?.hasCompletedProfile == true
    let documentsDone = status?.areDocumentsUploaded == true
    let legalDone = (legalDocuments ?? []).allSatisfy(\.isAccepted)
    let contract = status?.contractStatus

    var steps = [
        RegistrationStep(
            category: .profile,
            status: profileDone ? .done : .missing,
            details: profileDone ? [] : (status?.missingFields ?? []).map(RegistrationStepDetail.missingField)
        ),
        RegistrationStep(
            category: .documents,
            status: documentsDone ? .done : .missing,
            details: documentsDone ? [] : [.documentsRequired]
        )
    ]
    if let legalDocuments, !legalDocuments.isEmpty {
        steps.append(RegistrationStep(category: .legalDocuments, status: legalDone ? .done : .missing, details: []))
    }
    steps.append(approvalStep(
        profileDone: profileDone,
        documentsDone: documentsDone,
        legalDone: legalDone,
        contract: contract
    ))
    return steps
}

private func approvalStep(
    profileDone: Bool,
    documentsDone: Bool,
    legalDone: Bool,
    contract: ContractStatus?
) -> RegistrationStep {
    if contract == .approved || contract == .active {
        return RegistrationStep(category: .approval, status: .done, details: [])
    }
    if contract == .rejected {
        return RegistrationStep(category: .approval, status: .missing, details: [.approvalRejected])
    }
    if profileDone, documentsDone, legalDone, contract == .pending {
        return RegistrationStep(category: .approval, status: .pending, details: [.approvalAwaitingReview])
    }
    return RegistrationStep(category: .approval, status: .missing, details: [.approvalCompleteProfileFirst])
}
