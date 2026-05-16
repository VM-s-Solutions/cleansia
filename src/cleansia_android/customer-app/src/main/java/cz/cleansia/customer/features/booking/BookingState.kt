package cz.cleansia.customer.features.booking

data class BookingState(
    // Step 1 — What
    val selectedServiceIds: Set<String> = emptySet(),
    val selectedPackageIds: Set<String> = emptySet(),
    /**
     * Slugs (e.g. "inside-oven") of catalog extras the user has toggled on.
     * Slug-keyed (not id-keyed) so the wire payload matches what the
     * backend pricing calculator expects and the ConfirmStep can render
     * translations by looking them up in [CatalogRepository.extras].
     */
    val selectedExtraSlugs: Set<String> = emptySet(),
    val rooms: Int = 1,
    val bathrooms: Int = 1,

    // Step 2 — When & Where
    val street: String = "",
    val city: String = "",
    val zipCode: String = "",
    // Non-null when the address was picked from the user's saved list; the
    // street/city/zipCode fields still mirror it for display. Null means the
    // address is one-off/inline and the three string fields are authoritative —
    // submit code uses this to pick between savedAddressId vs inline payload
    // (backend expects XOR).
    val savedAddressId: String? = null,
    val selectedDate: String = "",
    val selectedTime: String = "",
    // Server-bound moment. Computed from (selectedDate, selectedTime) when the
    // user picks a slot — the two string fields stay for UI display but this is
    // what CreateOrder submits. Null until a slot is chosen.
    val selectedInstant: kotlinx.datetime.Instant? = null,

    // Step 3 — Confirm
    val paymentMethod: String = "", // "card" or "cash"
    val specialInstructions: String = "",
    // Loyalty Phase B — raw user input. Trimmed + uppercased before validation.
    // Empty string = no promo intent. Validity tracked separately on the VM as
    // PromoCodeUiState; the wire payload only sends this when state is Valid.
    val promoCode: String = "",
    // Loyalty Phase C — late-acceptance referral code. Same shape as promoCode:
    // raw user input, trimmed/uppercased before validation. Backend treats
    // unknown/already-referred codes as silent no-ops, so the wire payload
    // sends whatever the user typed (even when client-side validation says
    // invalid). Empty string means "don't send the field".
    val referralCode: String = "",

    // Plus members may pre-request a cleaner they've worked with before. Backend
    // validates eligibility (must have a Completed order with this employee) and
    // boosts the matching score. Null = no preference / not Plus.
    val preferredEmployeeId: String? = null,
)
