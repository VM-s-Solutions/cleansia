import CleansiaCore
import Foundation

extension CreateRecurringViewModel {
    /// A schedule is always an account's. Its current quote decides the crew and currency; only a
    /// verified, unexpired saved card in that currency permits cash.
    var cashEligibility: CashEligibility {
        let crew = formCrew.flatMap { $0.selection == pricedSelection ? $0.requiredEmployees : nil }
        let hasCard = cashCards.flatMap { cards in
            crewCurrencyCode.map { SavedCard.usable(in: cards, currencyCode: $0) != nil }
        }
        let eligibility = CashEligibility.resolve(signedIn: true, requiredEmployees: crew, hasUsableSavedCard: hasCard)
        return eligibility == .pending && cashCardReadFailed ? .cardCheckFailed : eligibility
    }

    func refreshCashCard() async {
        cashCardReadSequence += 1
        let sequence = cashCardReadSequence
        cashCards = nil
        cashCardReadFailed = false
        let result = await savedCardClient.myCards()
        guard sequence == cashCardReadSequence else { return }
        switch result {
        case let .success(cards): cashCards = cards
        case .failure: cashCardReadFailed = true
        }
    }

    /// A POST refusal invalidates the earlier card verdict immediately and asks once for current cards.
    /// Keep the payment empty while that answer is pending; no recovery path selects another method.
    func takeCashAwayIfServerRefused(_ error: ApiError) {
        guard error.code == CashEligibility.refusalCode || error.code == "order.cash_requires_saved_card"
        else { return }
        dropCash(announce: false)
        cashCardReadSequence += 1
        cashCards = nil
        cashCardReadFailed = false
        Task { [weak self] in await self?.refreshCashCard() }
    }

    /// Cash goes out only on a fresh quote for the form that says one cleaner does it. The verdict is
    /// this quote's own answer: a debounced requote that starts while it is in flight may take over
    /// the form's crew, but it must not turn a confirmed save into "could not confirm".
    func cashConfirmedForForm() async -> Bool {
        let selection = pricedSelection
        let sequence = nextQuoteSequence()
        let result = await quoteClient.quote(selection.quoteRequest)
        await refreshCashCard()
        var requiredEmployees: Int?
        var currency: String?
        if case let .success(quoted) = result {
            requiredEmployees = quoted.requiredEmployees
            currency = quoted.currencyCode
        }
        if sequence == quoteSequence {
            formCrew = requiredEmployees.map { RecurringCrew(selection: selection, requiredEmployees: $0) }
            crewCurrencyCode = currency
        }
        let hasCard = cashCards.flatMap { cards in
            currency.map { SavedCard.usable(in: cards, currencyCode: $0) != nil }
        }
        let eligibility = CashEligibility.resolve(
            signedIn: true, requiredEmployees: requiredEmployees, hasUsableSavedCard: hasCard
        )
        if eligibility == .available { return true }
        if eligibility.refusesCash {
            dropCash(announce: true)
        } else {
            snackbar.showError(L10n.Recurring.cashUnchecked)
        }
        return false
    }
}
