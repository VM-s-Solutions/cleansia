package cz.cleansia.customer.features.orders

import androidx.compose.runtime.Composable
import androidx.compose.ui.res.pluralStringResource
import cz.cleansia.customer.R

/**
 * "2 rooms · 1 bath" — built from TWO plurals resources rather than one.
 *
 * Android's `<plurals>` selects on a single quantity, so a string carrying two independent counts
 * cannot be one resource: whichever count drove the selection, the other noun would be frozen. That is
 * exactly the bug this replaces — the old `order_detail_rooms_bathrooms` was a flat string, so Czech
 * read "1 pokojů · 1 koup." with the genitive plural that is correct only for 5 and above.
 *
 * The two halves are the same resources the booking wizard's chips use, so the wizard and the order
 * detail cannot drift apart in wording.
 */
@Composable
internal fun roomsAndBathrooms(rooms: Int, bathrooms: Int): String {
    val roomsText = pluralStringResource(R.plurals.booking_rooms_short, rooms, rooms)
    val bathText = pluralStringResource(R.plurals.booking_bath_short, bathrooms, bathrooms)
    return "$roomsText · $bathText"
}
