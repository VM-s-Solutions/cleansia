package cz.cleansia.customer.core.booking

/**
 * Cash needs a signed-in customer, one quoted cleaner and a verified, usable saved card in the quoted currency.
 * Null inputs mean the current selection or saved cards have not been checked; the server rechecks on create.
 */
sealed interface CashEligibility {
    data object Available : CashEligibility

    data object NeedsAccount : CashEligibility

    data class NeedsCard(val requiredCleaners: Int) : CashEligibility

    data object NeedsSavedCard : CashEligibility

    data object Pending : CashEligibility

    /** Pending checks keep the choice while confirmation stays disabled. */
    val isRefused: Boolean get() = this is NeedsAccount || this is NeedsCard || this is NeedsSavedCard

    companion object {
        private val ServerRefusals = setOf(
            "order.cash_requires_saved_card",
            "order.cash_not_available",
            "order.cash_open_bookings_limit_reached",
        )

        fun refusedBy(keys: Iterable<String>): Boolean = keys.any { value ->
            value.split(';').any { it.trim() in ServerRefusals }
        }

        fun resolve(signedIn: Boolean, requiredEmployees: Int?, hasUsableSavedCard: Boolean?): CashEligibility = when {
            requiredEmployees != null && requiredEmployees > 1 -> NeedsCard(requiredEmployees)
            !signedIn -> NeedsAccount
            requiredEmployees != 1 || hasUsableSavedCard == null -> Pending
            !hasUsableSavedCard -> NeedsSavedCard
            requiredEmployees == 1 -> Available
            else -> Pending
        }
    }
}
