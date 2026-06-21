package cz.cleansia.partner.features.profile

/**
 * SavedStateHandle key used to pass the picker's chosen
 * `GeocodedAddress` (JSON-encoded) back to the Address section.
 * Single source of truth so the producer and consumer can't drift.
 */
const val ADDRESS_PICKER_RESULT_KEY = "address_picker_result"
