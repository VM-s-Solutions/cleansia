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

    func testTheRejectedRowCarriesTheAdminsReasonTrimmed() {
        var status = complete(contract: .rejected)
        status.rejectionReason = "  Upload a readable ID card.\n"
        let approval = step(buildSteps(status), .approval)
        XCTAssertEqual(approval.details, [.approvalRejected, .rejectionReason("Upload a readable ID card.")])
    }

    func testARejectionWithoutAReasonShowsOnlyTheRejectedLine() {
        for reason in [nil, "", "   \n"] {
            var status = complete(contract: .rejected)
            status.rejectionReason = reason
            XCTAssertEqual(
                step(buildSteps(status), .approval).details,
                [.approvalRejected],
                "\(String(describing: reason))"
            )
        }
    }

    /// The reason field outlives the decision on the server; only a rejection may show it.
    func testAReasonIsNeverShownOnAnApplicationThatIsNotRejected() {
        for contract in [ContractStatus.pending, .approved, .active] {
            var status = complete(contract: contract)
            status.rejectionReason = "stale"
            let details = step(buildSteps(status), .approval).details
            XCTAssertFalse(details.contains(.rejectionReason("stale")), "\(contract)")
        }
    }

    func testOnlyARejectedApprovalRowOpensSupport() {
        XCTAssertTrue(isFixable(step(buildSteps(complete(contract: .rejected)), .approval)))
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

    // MARK: contract documents

    func testWithNothingInForceThereIsNoDocumentsStepAndApprovalWaitsOnNothing() {
        for documents in [nil, [CleanerLegalDocument]()] {
            let steps = buildSteps(complete(contract: .pending), legalDocuments: documents)
            XCTAssertEqual(steps.map(\.category), [.profile, .documents, .approval])
            XCTAssertEqual(step(steps, .approval).status, .pending)
        }
    }

    func testAnUnacceptedDocumentIsAMissingStepThatHoldsApprovalBack() {
        let steps = buildSteps(
            complete(contract: .pending),
            legalDocuments: [.sample(type: ._3, isAccepted: true), .sample(type: ._5)]
        )

        XCTAssertEqual(steps.map(\.category), [.profile, .documents, .legalDocuments, .approval])
        XCTAssertEqual(step(steps, .legalDocuments).status, .missing)
        XCTAssertEqual(step(steps, .approval).status, .missing)
        XCTAssertEqual(step(steps, .approval).details, [.approvalCompleteProfileFirst])
    }

    func testEveryDocumentAcceptedIsADoneStepAndTheApplicationWaitsForReview() {
        let steps = buildSteps(
            complete(contract: .pending),
            legalDocuments: [.sample(type: ._3, isAccepted: true), .sample(type: ._5, isAccepted: true)]
        )

        XCTAssertEqual(step(steps, .legalDocuments).status, .done)
        XCTAssertEqual(step(steps, .approval).status, .pending)
    }

    // MARK: re-entry

    /// The lock replaces the app until approval, so a row that stopped opening once Done left a
    /// cleaner no way to correct a section or add the second document approval needs.
    func testEveryCleanerOwnedRowStillOpensOnceDone() {
        let steps = buildSteps(
            complete(contract: .pending),
            legalDocuments: [.sample(type: ._3, isAccepted: true)]
        )
        for category in [RegistrationStepCategory.profile, .documents, .legalDocuments] {
            let done = step(steps, category)
            XCTAssertEqual(done.status, .done, "\(category)")
            XCTAssertTrue(isFixable(done), "\(category) must reopen when Done")
        }
    }

    func testARejectedCleanerCanStillReopenTheirSections() {
        let steps = buildSteps(complete(contract: .rejected))
        XCTAssertTrue(isFixable(step(steps, .profile)))
        XCTAssertTrue(isFixable(step(steps, .documents)))
    }

    func testTheApprovalRowOpensNothingWhileTheAdminDecides() {
        for contract in [ContractStatus.pending, .approved, .active] {
            XCTAssertFalse(isFixable(step(buildSteps(complete(contract: contract)), .approval)), "\(contract)")
        }
        XCTAssertFalse(isFixable(step(buildSteps(complete(profile: false, contract: .pending)), .approval)))
    }

    private func step(_ steps: [RegistrationStep], _ category: RegistrationStepCategory) -> RegistrationStep {
        guard let match = steps.first(where: { $0.category == category }) else {
            XCTFail("missing \(category) step")
            return RegistrationStep(category: category, status: .missing, details: [])
        }
        return match
    }
}
