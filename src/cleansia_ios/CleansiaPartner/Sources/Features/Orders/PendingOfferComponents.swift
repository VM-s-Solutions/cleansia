import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// A refusal the cleaner is owed an explanation for, paired with the offer it belongs to.
///
/// The two refusals are different failures and the copy says so. A refused CONFIRM is the platform's
/// mistake — nothing gates the reservation on the weekly cap, so the take gate can refuse a job the
/// cleaner was told was theirs. A refused RELEASE is a write that did not land: nothing changed, no
/// mistake to own in either direction, and the honest line is to try again.
struct OfferRefusal: Equatable {
    enum Kind: Equatable {
        case confirm
        case release
    }

    let kind: Kind
    let displayOrderNumber: String?
    let reason: String

    var headline: String {
        switch kind {
        case .confirm: L10n.Offers.blockedTitle
        case .release: L10n.Offers.releaseFailedTitle
        }
    }

    var title: String {
        guard let displayOrderNumber, !displayOrderNumber.isBlank else { return headline }
        return "\(headline) · \(displayOrderNumber)"
    }

    var message: String {
        switch kind {
        case .confirm: L10n.Offers.blockedBody(reason)
        case .release: L10n.Offers.releaseFailedBody(reason)
        }
    }
}

enum OfferLabels {
    /// The deadline is stated, never counted down: the hold's real expiry lives on the server, so a
    /// remaining-time label on a screen left open drifts into a promise the client cannot keep.
    static func reservedUntil(_ respondByUtc: Date?, now: Date = Date()) -> String {
        guard let deadline = PendingOfferPresentation.respondBy(respondByUtc, now: now) else {
            return L10n.Offers.reservedEnded
        }
        switch deadline.day {
        case .today: return L10n.Offers.reservedUntilToday(deadline.time)
        case .tomorrow: return L10n.Offers.reservedUntilTomorrow(deadline.time)
        case .later: return L10n.Offers.reservedUntilDate(deadline.date, deadline.time)
        case .ended: return L10n.Offers.reservedEnded
        }
    }
}

/// The disclosure that turns a priority into an assignment: this job is held for you, and until when.
struct ReservedForYouRow: View {
    let respondByUtc: Date?
    var now: Date = .init()

    var body: some View {
        HStack(spacing: Spacing.xxs) {
            Image(systemName: "clock")
                .font(.system(size: 14))
                .foregroundColor(CleansiaColors.primary)
            Text(OfferLabels.reservedUntil(respondByUtc, now: now))
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(CleansiaColors.primary)
            Spacer(minLength: 0)
        }
    }
}

/// The server's own reason is quoted verbatim inside platform-owned framing, and which framing depends
/// on which promise broke. Shared by the offers list and the order detail so the same failure reads
/// identically wherever it is met.
struct OfferRefusalDialog: View {
    let refusal: OfferRefusal
    let onDismiss: () -> Void

    var body: some View {
        CleansiaDialog(
            title: refusal.title,
            confirmLabel: L10n.Offers.blockedDismiss,
            onConfirm: onDismiss,
            onDismiss: onDismiss,
            message: refusal.message,
            icon: "exclamationmark.triangle"
        )
    }
}

/// Refusing the reservation is destructive and irreversible for this cleaner, so it asks first. The
/// copy says what happens to the JOB and never what the customer will be told — one sentence covers a
/// refusal and a silence on their side, and naming them here would make a claim the platform does not.
struct OfferDeclineDialog: View {
    let onConfirm: () -> Void
    let onDismiss: () -> Void

    var body: some View {
        CleansiaDialog(
            title: L10n.Offers.declineTitle,
            confirmLabel: L10n.Offers.declineCta,
            onConfirm: onConfirm,
            onDismiss: onDismiss,
            message: L10n.Offers.declineBody,
            dismissLabel: L10n.cancel,
            destructive: true
        )
    }
}

#if DEBUG
    struct PendingOfferComponents_Previews: PreviewProvider {
        static var previews: some View {
            VStack(alignment: .leading, spacing: Spacing.m) {
                ReservedForYouRow(
                    respondByUtc: Date(timeIntervalSinceNow: 3600),
                    now: Date()
                )
                ReservedForYouRow(
                    respondByUtc: Date(timeIntervalSinceNow: -3600),
                    now: Date()
                )
            }
            .padding()
            .background(CleansiaColors.surface)
            .previewLayout(.sizeThatFits)
        }
    }
#endif
