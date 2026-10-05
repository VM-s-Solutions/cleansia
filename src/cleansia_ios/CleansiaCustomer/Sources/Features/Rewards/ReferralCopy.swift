import CleansiaCore
import Foundation

/// The referral reward is the chosen market's `ReferralCredit`, formatted on the device in that market's
/// currency. With none known — the directory has not resolved, or the market pays none — no line promises a
/// reward. A locale string never carries the amount or a currency of its own.
enum ReferralCopy {
    static func dialogHelper(_ credit: MarketMoney?) -> String {
        credit.map { L10n.Booking.referralDialogHelper(amount($0)) } ?? L10n.Booking.referralDialogHelperNoFigure
    }

    static func dialogSuccess(referrer: String?, credit: MarketMoney?) -> String {
        if let referrer, !referrer.isBlank {
            return credit.map { L10n.Booking.referralDialogSuccessNamed(referrer, amount($0)) }
                ?? L10n.Booking.referralDialogSuccessNamedNoFigure(referrer)
        }
        return credit.map { L10n.Booking.referralDialogSuccess(amount($0)) }
            ?? L10n.Booking.referralDialogSuccessNoFigure
    }

    static func rewardsSubtitle(_ credit: MarketMoney?) -> String {
        credit.map { L10n.Rewards.referralSubtitle(amount($0)) } ?? L10n.Rewards.referralSubtitleNoFigure
    }

    static func shareMessage(code: String) -> String {
        L10n.Rewards.referralShareTextNoFigure(code, CleansiaWeb.referralLink(code: code))
    }

    static func upsellDescription(_ credit: MarketMoney?) -> String {
        credit.map { L10n.Home.upsellReferralDesc(amount($0)) } ?? L10n.Home.upsellReferralDescGeneric
    }

    static func upsellChip(_ credit: MarketMoney?) -> String? {
        credit.map { L10n.Home.upsellChipCredit(amount($0)) }
    }

    static func amount(_ credit: MarketMoney) -> String {
        OrdersFormat.price(credit.amount, currencyCode: credit.currencyCode)
    }
}
