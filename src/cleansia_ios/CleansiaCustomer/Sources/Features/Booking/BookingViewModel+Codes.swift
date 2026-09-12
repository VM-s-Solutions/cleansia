import CleansiaCore
import Foundation

/// The promo and referral code entry, split out of BookingViewModel.swift so the type body stays
/// under the lint ceiling; both read the quote and write the draft through the same `update`.
extension BookingViewModel {
    @discardableResult
    func validatePromoCode(_ rawCode: String) async -> PromoCodeState {
        let normalized = rawCode.trimmingCharacters(in: .whitespacesAndNewlines).uppercased()
        if normalized.isEmpty {
            promoState = .idle
            return .idle
        }
        promoState = .validating
        let quote = quoteState.quote
        let subtotal = quote?.preSurchargeSubtotal ?? 0
        let currencyId = quote.flatMap { $0.currencyId.isBlank ? nil : $0.currencyId }
        let resolved: PromoCodeState = switch await promoClient.validate(
            code: normalized,
            orderSubtotal: subtotal,
            currencyId: currencyId
        ) {
        case let .success(validation):
            if validation.isValid, let discount = validation.discountAmount {
                .valid(discountAmount: quote?.discountAsCharged(discount) ?? discount)
            } else {
                .invalid(PromoCodeError.from(validation.errorCode))
            }
        case .failure:
            .invalid(nil)
        }
        promoState = resolved
        if case .valid = resolved {
            update { current in
                var next = current
                next.promoCode = normalized
                return next
            }
        }
        return resolved
    }

    @discardableResult
    func validateReferralCode(_ rawCode: String) async -> ReferralCodeState {
        let normalized = rawCode.trimmingCharacters(in: .whitespacesAndNewlines).uppercased()
        if normalized.isEmpty {
            referralState = .idle
            return .idle
        }
        referralState = .validating
        let resolved: ReferralCodeState = switch await referralClient.validate(code: normalized) {
        case let .success(validation):
            if validation.isValid {
                .valid(referrerFirstName: validation.referrerFirstName)
            } else {
                .invalid(ReferralValidationError.from(validation.errorCode))
            }
        case .failure:
            .invalid(nil)
        }
        referralState = resolved
        if case .valid = resolved {
            update { current in
                var next = current
                next.referralCode = normalized
                return next
            }
        }
        return resolved
    }

    func clearPromoCode() {
        promoState = .idle
        update { current in
            var next = current
            next.promoCode = ""
            return next
        }
    }

    func clearReferralCode() {
        referralState = .idle
        update { current in
            var next = current
            next.referralCode = ""
            return next
        }
    }
}
