import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

final class RegistrationCompletionTests: XCTestCase {
    private func complete(
        profile: Bool? = true,
        documents: Bool? = true,
        contract: ContractStatus? = .approved
    ) -> RegistrationCompletionStatus {
        RegistrationCompletionStatus(
            areDocumentsUploaded: documents,
            hasCompletedProfile: profile,
            hasSetAvailability: false,
            contractStatus: contract
        )
    }

    func testEmptyStatusIsLocked() {
        XCTAssertFalse(isRegistrationComplete(RegistrationCompletionStatus()))
    }

    func testProfileNilIsLocked() {
        XCTAssertFalse(isRegistrationComplete(complete(profile: nil)))
    }

    func testProfileFalseIsLocked() {
        XCTAssertFalse(isRegistrationComplete(complete(profile: false)))
    }

    func testDocumentsNilIsLocked() {
        XCTAssertFalse(isRegistrationComplete(complete(documents: nil)))
    }

    func testDocumentsFalseIsLocked() {
        XCTAssertFalse(isRegistrationComplete(complete(documents: false)))
    }

    func testContractNilIsLocked() {
        XCTAssertFalse(isRegistrationComplete(complete(contract: nil)))
    }

    func testContractPendingIsLocked() {
        XCTAssertFalse(isRegistrationComplete(complete(contract: .pending)))
    }

    func testContractTerminatedIsLocked() {
        XCTAssertFalse(isRegistrationComplete(complete(contract: .terminated)))
    }

    func testContractRejectedIsLocked() {
        XCTAssertFalse(isRegistrationComplete(complete(contract: .rejected)))
    }

    func testAvailabilityIsNotAGateClause() {
        XCTAssertTrue(isRegistrationComplete(RegistrationCompletionStatus(
            areDocumentsUploaded: true,
            hasCompletedProfile: true,
            hasSetAvailability: nil,
            contractStatus: .approved
        )))
    }

    func testProfileDocsApprovedIsUnlocked() {
        XCTAssertTrue(isRegistrationComplete(complete(contract: .approved)))
    }

    func testProfileDocsActiveIsUnlocked() {
        XCTAssertTrue(isRegistrationComplete(complete(contract: .active)))
    }

    func testBuildStepsAllMissingWhenStatusNil() {
        let steps = buildSteps(nil)
        XCTAssertEqual(steps.map(\.category), [.profile, .documents, .approval])
        XCTAssertTrue(steps.allSatisfy { $0.status == .missing })
    }

    func testBuildStepsApprovedIsAllDone() {
        let steps = buildSteps(complete(contract: .approved))
        XCTAssertTrue(steps.allSatisfy { $0.status == .done })
    }

    func testBuildStepsActiveApprovalIsDone() {
        let approval = step(buildSteps(complete(contract: .active)), .approval)
        XCTAssertEqual(approval.status, .done)
    }

    func testBuildStepsRejectedApprovalIsMissingWithSupport() {
        let approval = step(buildSteps(complete(contract: .rejected)), .approval)
        XCTAssertEqual(approval.status, .missing)
        XCTAssertTrue(approval.details.contains(.approvalRejected))
    }

    func testBuildStepsAwaitingReviewWhenProfileAndDocsDoneAndPending() {
        let approval = step(buildSteps(complete(contract: .pending)), .approval)
        XCTAssertEqual(approval.status, .pending)
        XCTAssertTrue(approval.details.contains(.approvalAwaitingReview))
    }

    func testBuildStepsCompleteProfileFirstWhenProfileIncomplete() {
        let approval = step(
            buildSteps(complete(profile: false, contract: .pending)),
            .approval
        )
        XCTAssertEqual(approval.status, .missing)
        XCTAssertTrue(approval.details.contains(.approvalCompleteProfileFirst))
    }

    func testBuildStepsProfileRowReflectsCompletion() {
        XCTAssertEqual(step(buildSteps(complete(profile: true)), .profile).status, .done)
        XCTAssertEqual(step(buildSteps(complete(profile: false)), .profile).status, .missing)
    }

    func testBuildStepsProfileRowCarriesMissingFieldsAsTypedDetails() {
        let status = RegistrationCompletionStatus(
            areDocumentsUploaded: true,
            hasCompletedProfile: false,
            missingFields: ["profile.fields.firstName", "profile.fields.iban"],
            contractStatus: .pending
        )
        let profile = step(buildSteps(status), .profile)
        XCTAssertEqual(profile.details, [
            .missingField("profile.fields.firstName"),
            .missingField("profile.fields.iban")
        ])
    }

    func testBuildStepsDocumentsRowReflectsCompletion() {
        XCTAssertEqual(step(buildSteps(complete(documents: true)), .documents).status, .done)
        let missing = step(buildSteps(complete(documents: false)), .documents)
        XCTAssertEqual(missing.status, .missing)
        XCTAssertTrue(missing.details.contains(.documentsRequired))
    }

    private func step(_ steps: [RegistrationStep], _ category: RegistrationStepCategory) -> RegistrationStep {
        guard let match = steps.first(where: { $0.category == category }) else {
            XCTFail("missing \(category) step")
            return RegistrationStep(category: category, status: .missing, details: [])
        }
        return match
    }
}
