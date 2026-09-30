import Foundation

/// The sentences a membership surface owes a member. A running trial carries every Plus benefit; what it
/// changes is the money — nothing is charged until it ends, and a cancel inside it means no payment follows.
struct MembershipCopy: Equatable {
    /// When the running trial ends; nil when no trial is running.
    let trialEndsOn: Date?
    /// The renewal payment failed: nothing runs to a period end, so a cancel is immediate.
    let benefitsPaused: Bool
    let cancelRequested: Bool
    /// When the running trial or the paid period ends.
    let periodEnd: Date?

    init(_ membership: MyMembership?, now: Date = Date()) {
        let member = membership?.hasMembership == true ? membership : nil
        benefitsPaused = member?.benefitsPaused ?? false
        cancelRequested = member?.cancelRequested ?? false
        trialEndsOn = member?.trialEndsAtUtc.flatMap { $0 > now ? $0 : nil }
        periodEnd = trialEndsOn ?? member?.currentPeriodEnd
    }

    var isTrial: Bool {
        trialEndsOn != nil
    }

    var periodHeadline: String? {
        guard let periodEnd else { return nil }
        let date = MembershipFormat.periodEnd(periodEnd)
        if cancelRequested { return L10n.Membership.activeUntil(date) }
        return isTrial ? L10n.Membership.trialUntil(date) : L10n.Membership.renewsOn(date)
    }

    var periodHint: String {
        switch (cancelRequested, isTrial) {
        case (true, true): L10n.Membership.trialCancelledLead
        case (true, false): L10n.Membership.thenEndsHint
        case (false, true): L10n.Membership.trialFirstChargeHint
        case (false, false): L10n.Membership.autoRenewHint
        }
    }

    var cancelDialogMessage: String {
        if benefitsPaused { return L10n.Membership.cancelDialogMessagePastDue }
        guard let trialEndsOn else { return L10n.Membership.cancelDialogMessage }
        return L10n.Membership.cancelDialogMessageTrial(MembershipFormat.periodEnd(trialEndsOn))
    }

    func cancelSuccess(activeUntil date: Date) -> String {
        if benefitsPaused { return L10n.Membership.cancelSuccessPastDue }
        if isTrial { return L10n.Membership.cancelSuccessTrial }
        return L10n.Membership.cancelledUntil(MembershipFormat.periodEnd(date))
    }

    /// A swap during a trial keeps the trial and charges nothing now.
    func switchDialogMessage(price: String) -> String {
        guard let trialEndsOn else { return L10n.Membership.switchDialogMessage(price) }
        return L10n.Membership.switchDialogMessageTrial(
            trialEndsOn: MembershipFormat.periodEnd(trialEndsOn),
            price: price
        )
    }

    var successSubtitle: String {
        guard let trialEndsOn else { return L10n.Membership.successSubtitle }
        return L10n.Membership.successSubtitleTrial(MembershipFormat.periodEnd(trialEndsOn))
    }
}
