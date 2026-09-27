import Foundation

/// The sentences a membership surface owes a member. `hasMembership` counts a running trial, but no Plus
/// benefit runs during one — every benefit starts with the first paid month — so a trialing member is told
/// what a paid membership will include and when, never that a benefit is already on.
struct MembershipCopy: Equatable {
    /// When the running trial ends; nil when no trial is running.
    let trialEndsOn: Date?

    init(_ membership: MyMembership?, now: Date = Date()) {
        guard let membership, membership.hasMembership,
              let trialEndsAtUtc = membership.trialEndsAtUtc, trialEndsAtUtc > now
        else {
            trialEndsOn = nil
            return
        }
        trialEndsOn = trialEndsAtUtc
    }

    var isTrial: Bool {
        trialEndsOn != nil
    }

    var perksTitle: String? {
        isTrial ? L10n.Membership.trialPerksTitle : nil
    }

    var perksNote: String? {
        isTrial ? L10n.Membership.trialPerksNote : nil
    }

    var cancelledHint: String {
        isTrial ? L10n.Membership.trialCancelledLead : L10n.Membership.thenEndsHint
    }

    var cancelDialogMessage: String {
        guard let trialEndsOn else { return L10n.Membership.cancelDialogMessage }
        return L10n.Membership.cancelDialogMessageTrial(MembershipFormat.periodEnd(trialEndsOn))
    }

    func cancelSuccess(activeUntil date: Date) -> String {
        isTrial ? L10n.Membership.cancelSuccessTrial : L10n.Membership.cancelledUntil(MembershipFormat.periodEnd(date))
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
        isTrial ? L10n.Membership.successSubtitleTrial : L10n.Membership.successSubtitle
    }

    var successPerksHeader: String {
        isTrial ? L10n.Membership.trialPerksTitle : L10n.Membership.successPerksHeader
    }

    /// The server refuses a schedule until a paid month begins.
    var offersRecurringSetup: Bool {
        !isTrial
    }
}
