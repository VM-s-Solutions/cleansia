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

func buildSteps(_ status: RegistrationCompletionStatus?) -> [RegistrationStep] {
    let profileDone = status?.hasCompletedProfile == true
    let documentsDone = status?.areDocumentsUploaded == true
    let contract = status?.contractStatus

    return [
        RegistrationStep(
            category: .profile,
            status: profileDone ? .done : .missing,
            details: profileDone ? [] : (status?.missingFields ?? []).map(RegistrationStepDetail.missingField)
        ),
        RegistrationStep(
            category: .documents,
            status: documentsDone ? .done : .missing,
            details: documentsDone ? [] : [.documentsRequired]
        ),
        approvalStep(profileDone: profileDone, documentsDone: documentsDone, contract: contract)
    ]
}

private func approvalStep(
    profileDone: Bool,
    documentsDone: Bool,
    contract: ContractStatus?
) -> RegistrationStep {
    if contract == .approved || contract == .active {
        return RegistrationStep(category: .approval, status: .done, details: [])
    }
    if contract == .rejected {
        return RegistrationStep(category: .approval, status: .missing, details: [.approvalRejected])
    }
    if profileDone, documentsDone, contract == .pending {
        return RegistrationStep(category: .approval, status: .pending, details: [.approvalAwaitingReview])
    }
    return RegistrationStep(category: .approval, status: .missing, details: [.approvalCompleteProfileFirst])
}
