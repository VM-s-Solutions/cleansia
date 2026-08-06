package cz.cleansia.customer.features.recurring

/**
 * Mirrors the server's split. Authoring a schedule — `CreateRecurringBooking`,
 * `UpdateRecurringBooking` — is the paid Cleansia Plus capability. Listing,
 * pausing, resuming and deleting one that already exists is deliberately
 * ungated so a lapsed subscriber can always stop what is still generating
 * billable cleanings.
 */
enum class RecurringAuthoringGate {
    Allowed,
    Upsell,
    ;

    companion object {
        /**
         * A null [hasMembership] is the answer not having landed. It resolves
         * permissively: the server refuses an unentitled create on its own, so
         * failing open costs a member nothing, while failing closed shows a
         * paid-up member the upsell every time the fetch is slow or fails.
         */
        fun resolve(hasMembership: Boolean?): RecurringAuthoringGate =
            if (hasMembership == false) Upsell else Allowed
    }
}

data class RecurringListAffordances(
    val showCreateAction: Boolean,
    val showPlusUpsell: Boolean,
    val showLapsedNotice: Boolean,
    val showEdit: Boolean,
) {
    companion object {
        fun of(gate: RecurringAuthoringGate, hasTemplates: Boolean) = RecurringListAffordances(
            showCreateAction = gate == RecurringAuthoringGate.Allowed && hasTemplates,
            showPlusUpsell = gate == RecurringAuthoringGate.Upsell && !hasTemplates,
            showLapsedNotice = gate == RecurringAuthoringGate.Upsell && hasTemplates,
            showEdit = gate == RecurringAuthoringGate.Allowed,
        )
    }
}
