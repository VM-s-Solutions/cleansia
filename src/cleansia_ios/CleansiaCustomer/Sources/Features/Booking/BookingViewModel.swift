import CleansiaCore
import Combine
import Foundation

@MainActor
final class BookingViewModel: ViewModel {
    @Published private(set) var state = BookingState()
    @Published private(set) var submitState: ActionState = .idle
    @Published private(set) var quoteState: BookingQuoteState = .idle
    @Published private(set) var promoState: PromoCodeState = .idle
    @Published private(set) var referralState: ReferralCodeState = .idle

    @Published private(set) var currentStep = 1

    var isFirstStep: Bool {
        currentStep <= 1
    }

    var isLastStep: Bool {
        currentStep >= BookingStepGate.totalSteps
    }

    func update(_ transform: (BookingState) -> BookingState) {
        state = transform(state)
    }

    @discardableResult
    func advance() -> Bool {
        guard currentStep < BookingStepGate.totalSteps else { return false }
        currentStep += 1
        return true
    }

    @discardableResult
    func back() -> Bool {
        guard currentStep > 1 else { return false }
        currentStep -= 1
        return true
    }

    func reset() {
        state = BookingState()
        submitState = .idle
        quoteState = .idle
        promoState = .idle
        referralState = .idle
        currentStep = 1
    }
}
