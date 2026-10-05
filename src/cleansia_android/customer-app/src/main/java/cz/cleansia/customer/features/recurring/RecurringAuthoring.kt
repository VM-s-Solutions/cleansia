package cz.cleansia.customer.features.recurring

import androidx.annotation.StringRes
import cz.cleansia.customer.R
import cz.cleansia.customer.core.memberships.GetMyMembershipResponse
import cz.cleansia.customer.core.memberships.benefitsPaused
import cz.cleansia.customer.core.recurring.RecurringBookingTemplateDto

/**
 * Mirrors the server's split. Authoring a schedule — `CreateRecurringBooking`,
 * `UpdateRecurringBooking` — is the paid Cleansia Plus capability. Listing,
 * pausing, resuming and deleting one that already exists is deliberately
 * ungated so a lapsed subscriber can always stop what is still generating
 * billable cleanings.
 *
 * [Paused] is a live enrolment whose benefits are paused: the server refuses
 * authoring and books none of its schedules, yet refuses a second subscription
 * too, so it gets neither the create affordances nor the subscribe upsell.
 */
enum class RecurringAuthoringGate {
    Allowed,
    Upsell,
    Paused,
    ;

    companion object {
        /**
         * A null [membership] is the answer not having landed. It resolves
         * permissively: the server refuses an unentitled create on its own, so
         * failing open costs a member nothing, while failing closed shows a
         * paid-up member the upsell every time the fetch is slow or fails.
         */
        fun resolve(membership: GetMyMembershipResponse?): RecurringAuthoringGate = when {
            membership == null -> Allowed
            !membership.hasMembership -> Upsell
            membership.benefitsPaused -> Paused
            else -> Allowed
        }
    }
}

data class RecurringListAffordances(
    val showCreateAction: Boolean,
    val showPlusUpsell: Boolean,
    val showLapsedNotice: Boolean,
    val showPausedNotice: Boolean,
    val showEdit: Boolean,
) {
    companion object {
        fun of(gate: RecurringAuthoringGate, hasTemplates: Boolean) = RecurringListAffordances(
            showCreateAction = gate == RecurringAuthoringGate.Allowed && hasTemplates,
            showPlusUpsell = gate == RecurringAuthoringGate.Upsell && !hasTemplates,
            showLapsedNotice = gate == RecurringAuthoringGate.Upsell && hasTemplates,
            showPausedNotice = gate == RecurringAuthoringGate.Paused,
            showEdit = gate == RecurringAuthoringGate.Allowed,
        )
    }
}

/**
 * The card line for a schedule that still holds a service or package its market no longer offers. It says
 * "edit to update" only where the card offers Edit; a lapsed or paused member has no Edit, so their line
 * only says what the schedule holds.
 */
@StringRes
fun retiredEntryLine(showEdit: Boolean): Int =
    if (showEdit) R.string.recurring_card_item_no_longer_offered else R.string.recurring_card_item_no_longer_offered_no_edit

/** Whether a schedule books cleanings, and if not, why. */
enum class ScheduleStatus {
    Active,
    Paused,
    NeedsPaymentChange,
    ;

    companion object {
        /** A paused schedule books nothing, and neither does a cash one the server skips until it is changed. */
        fun of(template: RecurringBookingTemplateDto): ScheduleStatus = when {
            !template.isActive -> Paused
            template.requiresPaymentMethodChange -> NeedsPaymentChange
            else -> Active
        }
    }
}
