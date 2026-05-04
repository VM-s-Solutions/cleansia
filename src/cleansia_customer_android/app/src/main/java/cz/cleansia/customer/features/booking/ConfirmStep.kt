package cz.cleansia.customer.features.booking

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowRight
import androidx.compose.material.icons.outlined.AccessTime
import androidx.compose.material.icons.outlined.CalendarToday
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material.icons.outlined.ConfirmationNumber
import androidx.compose.material.icons.outlined.CreditCard
import androidx.compose.material.icons.outlined.Home
import androidx.compose.material.icons.outlined.LocationOn
import androidx.compose.material.icons.outlined.Payments
import androidx.compose.material.icons.outlined.Schedule
import androidx.compose.material.icons.outlined.Shield
import androidx.compose.material.icons.outlined.VerifiedUser
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.customer.core.catalog.CatalogRepositoryEntryPoint
import cz.cleansia.customer.ui.components.CleansiaTextField
import cz.cleansia.customer.ui.theme.selectionTint
import cz.cleansia.customer.ui.theme.SuccessText
import dagger.hilt.android.EntryPointAccessors

@Composable
fun ConfirmStep(state: BookingState, onUpdate: (BookingState) -> Unit) {
    val context = LocalContext.current
    val catalogRepo = remember {
        EntryPointAccessors
            .fromApplication(context, CatalogRepositoryEntryPoint::class.java)
            .catalogRepository()
    }
    val services by catalogRepo.services.collectAsState()
    val packages by catalogRepo.packages.collectAsState()

    val selectedServices = remember(services, state.selectedServiceIds) {
        services.filter { it.id in state.selectedServiceIds }
    }
    val selectedPackages = remember(packages, state.selectedPackageIds) {
        packages.filter { it.id in state.selectedPackageIds }
    }

    // Live quote via parent VM — drives the authoritative base price; fall back
    // to a rough catalog sum until the first quote lands so the card isn't blank.
    val bookingVm: BookingViewModel = androidx.hilt.navigation.compose.hiltViewModel()
    val quote by bookingVm.quote.collectAsState()
    val promoState by bookingVm.promoCodeState.collectAsState()
    val basePrice = quote?.totalPrice
        ?: (selectedServices.sumOf { it.basePrice + it.perRoomPrice * (state.rooms + state.bathrooms) } +
            selectedPackages.sumOf { it.price })
    val isExpress = BookingPricing.requiresExpressSurcharge(state.selectedInstant)
    val surcharge = BookingPricing.expressSurchargeAmount(basePrice, state.selectedInstant)
    // Tier discount integration is Phase A — until that's wired client-side we
    // treat it as zero and let promo carry the entire discount calc. When tier
    // lands, source it from a LoyaltyRepository observation here.
    val tierDiscount = 0.0
    val promoDiscount = (promoState as? PromoCodeUiState.Valid)?.discountAmount ?: 0.0
    val showPromoLine = promoDiscount > tierDiscount && promoDiscount > 0.0
    // showTierLine = tierDiscount > 0.0 && !showPromoLine — wired in once Phase A
    // tier discount lands client-side. Until then it's identically false.
    val finalTotal = BookingPricing.finalTotal(basePrice, state.selectedInstant, tierDiscount, promoDiscount)

    // Local sheet-open flags. The applied codes themselves live on BookingState
    // (via the VM), so reopening the sheets re-seeds with the canonical value.
    var promoSheetOpen by remember { mutableStateOf(false) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(20.dp),
    ) {
        // ── Order summary ──
        SummaryCard {
            // Items the customer picked, with per-row prices. Header gives the
            // section a clear visual anchor instead of bare rows.
            Text(
                stringResource(R.string.booking_summary_items_label),
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(6.dp))
            selectedServices.forEach { svc ->
                Row(Modifier.fillMaxWidth().padding(vertical = 4.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                    Text(svc.name, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurface, modifier = Modifier.weight(1f))
                    Text(
                        "${(svc.basePrice + svc.perRoomPrice * (state.rooms + state.bathrooms)).toInt()} CZK",
                        style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                }
            }
            selectedPackages.forEach { pkg ->
                Row(Modifier.fillMaxWidth().padding(vertical = 4.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                    Text(pkg.name, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurface, modifier = Modifier.weight(1f))
                    Text(
                        "${pkg.price.toInt()} CZK",
                        style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                }
            }
            HorizontalDivider(Modifier.padding(vertical = 10.dp))

            // Booking details with explicit labels — easier to scan than bare icons.
            Text(
                stringResource(R.string.booking_summary_details_label),
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(4.dp))
            LabeledInfoRow(Icons.Outlined.LocationOn, stringResource(R.string.booking_summary_address), state.street.ifBlank { "—" })
            LabeledInfoRow(Icons.Outlined.Home, stringResource(R.string.booking_summary_property), "${state.rooms} rooms · ${state.bathrooms} bath")
            LabeledInfoRow(Icons.Outlined.CalendarToday, stringResource(R.string.booking_summary_date), state.selectedDate.ifBlank { "—" })
            LabeledInfoRow(Icons.Outlined.AccessTime, stringResource(R.string.booking_summary_time), state.selectedTime.ifBlank { "—" })

            HorizontalDivider(Modifier.padding(vertical = 10.dp))

            // Total breakdown — base + (optional) express surcharge + grand total.
            // Server recomputes this so the displayed number IS what the user pays.
            Row(Modifier.fillMaxWidth().padding(vertical = 2.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                Text(
                    stringResource(R.string.booking_summary_subtotal),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Text(
                    "${basePrice.toInt()} CZK",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                )
            }
            if (isExpress) {
                Row(Modifier.fillMaxWidth().padding(vertical = 2.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                    Text(
                        stringResource(R.string.booking_summary_express_surcharge),
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    Text(
                        "+${surcharge.toInt()} CZK",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                }
            }
            if (showPromoLine) {
                // Best-discount-wins: only show the promo line when promo beats
                // tier (or tier is zero). Tier line would render here otherwise.
                Row(Modifier.fillMaxWidth().padding(vertical = 2.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                    Text(
                        stringResource(R.string.booking_summary_promo_discount, state.promoCode.trim().uppercase()),
                        style = MaterialTheme.typography.bodyMedium,
                        color = SuccessText,
                    )
                    Text(
                        "-${promoDiscount.toInt()} CZK",
                        style = MaterialTheme.typography.bodyMedium,
                        color = SuccessText,
                    )
                }
            }
            Spacer(Modifier.height(6.dp))
            Row(Modifier.fillMaxWidth().padding(vertical = 2.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                Text(
                    stringResource(R.string.booking_summary_total),
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
                Text(
                    "${finalTotal.toInt()} CZK",
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.primary,
                )
            }
        }
        Spacer(Modifier.height(16.dp))

        // ── Promo code (Loyalty Phase B) — Wolt-style row + dialog ──
        // Tap the row → modal sheet → user types + Apply → backend call → applied
        // code persists in BookingState, summary line above re-renders.
        PromoCodeRow(
            appliedCode = state.promoCode.takeIf { promoState is PromoCodeUiState.Valid }.orEmpty(),
            onClick = { promoSheetOpen = true },
            onClear = { bookingVm.clearPromoCode() },
        )
        Spacer(Modifier.height(12.dp))

        // (Referral codes are signup-only — removed from the booking flow on
        // purpose. One-per-invitee is enforced at registration; a separate
        // entry here just confused users who'd already redeemed at signup.)
        Spacer(Modifier.height(16.dp))

        // ── Payment method ──
        SectionLabel(stringResource(R.string.booking_payment_method))
        Spacer(Modifier.height(10.dp))

        PaymentOption(
            icon = Icons.Outlined.CreditCard,
            title = stringResource(R.string.booking_pay_card),
            subtitle = stringResource(R.string.booking_pay_card_desc),
            selected = state.paymentMethod == "card",
            onClick = { onUpdate(state.copy(paymentMethod = "card")) },
        )
        Spacer(Modifier.height(8.dp))
        PaymentOption(
            icon = Icons.Outlined.Payments,
            title = stringResource(R.string.booking_pay_cash),
            subtitle = stringResource(R.string.booking_pay_cash_desc),
            selected = state.paymentMethod == "cash",
            onClick = { onUpdate(state.copy(paymentMethod = "cash")) },
        )

        Spacer(Modifier.height(16.dp))

        // ── Special instructions ──
        CleansiaTextField(
            value = state.specialInstructions,
            onValueChange = { onUpdate(state.copy(specialInstructions = it)) },
            label = stringResource(R.string.booking_special_instructions_hint),
            singleLine = false,
        )

        Spacer(Modifier.height(16.dp))

        // ── Plus: pre-request a favorite cleaner ──
        // Renders nothing for non-Plus users or when the user has no eligible
        // cleaners — see PreferredCleanerPicker for the gating.
        PreferredCleanerPicker(
            selectedEmployeeId = state.preferredEmployeeId,
            onSelect = { id, _ -> onUpdate(state.copy(preferredEmployeeId = id)) },
        )

        Spacer(Modifier.height(20.dp))

        // ── Cancellation policy ──
        CancellationPolicyCard()
        Spacer(Modifier.height(16.dp))

        // ── Trust badges ──
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(14.dp))
                .background(MaterialTheme.colorScheme.surface)
                .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(14.dp))
                .padding(14.dp)
                .height(androidx.compose.foundation.layout.IntrinsicSize.Max),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            TrustBadge(
                Icons.Outlined.Shield,
                stringResource(R.string.booking_trust_insured),
                Modifier.weight(1f).fillMaxHeight(),
            )
            Box(Modifier.width(1.dp).fillMaxHeight().background(MaterialTheme.colorScheme.outlineVariant))
            TrustBadge(
                Icons.Outlined.VerifiedUser,
                stringResource(R.string.booking_trust_vetted),
                Modifier.weight(1f).fillMaxHeight(),
            )
        }

        Spacer(Modifier.height(32.dp))
    }

    // ── Bottom-sheet dialogs (rendered when their flags are flipped) ──
    if (promoSheetOpen) {
        PromoCodeBottomSheet(
            initialCode = state.promoCode,
            onDismiss = { promoSheetOpen = false },
            onValidate = { code -> bookingVm.validatePromoCodeNow(code) },
            // VM persisted code + state; the sheet only signals so we can close it.
            onApplied = { _, _ -> },
        )
    }
}

/* ── Cancellation policy card — 3-tier fee structure ── */

@Composable
private fun CancellationPolicyCard() {
    val context = LocalContext.current
    val membership = remember {
        EntryPointAccessors
            .fromApplication(context, cz.cleansia.customer.core.memberships.MembershipEntryPoint::class.java)
            .membershipRepository()
    }
    val membershipState by membership.current.collectAsState()
    // Backend BookingPolicy constants (mirror these exactly):
    //   StandardFreeWindowHours = 24  (free cancel ≥24h ahead)
    //   PenaltyWindowHours      = 4   (50% charge in 4–24h band; 100% under 4h)
    val standardFreeHours = 24
    val penaltyHours = 4
    // Plus may extend the free window. Only counts as a real perk when it's
    // strictly larger than the standard window — otherwise the badge would
    // be misleading.
    val rawPlusHours = membershipState
        ?.takeIf { it.hasMembership }
        ?.freeCancellationWindowHours
        ?.takeIf { it > 0 }
    val plusFreeHours = rawPlusHours?.takeIf { it > standardFreeHours }
    val freeHours = plusFreeHours ?: standardFreeHours
    // Mid-tier (50% charge) only renders when there's room between the free
    // window and the no-refund threshold. Plus members with a free window
    // wider than [penaltyHours] still see the mid-tier; if a future config
    // ever extends free below 4h the mid-tier vanishes (one-tier collapse).
    val showMidTier = freeHours > penaltyHours

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(14.dp))
            .padding(14.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Icon(
                Icons.Outlined.Schedule,
                null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(18.dp),
            )
            Spacer(Modifier.width(8.dp))
            Text(
                stringResource(R.string.booking_cancel_title),
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onBackground,
            )
            if (plusFreeHours != null) {
                Spacer(Modifier.weight(1f))
                Text(
                    stringResource(R.string.booking_cancel_plus_badge),
                    style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.primary,
                    modifier = Modifier
                        .clip(RoundedCornerShape(8.dp))
                        .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.12f))
                        .padding(horizontal = 8.dp, vertical = 3.dp),
                )
            }
        }
        if (plusFreeHours != null) {
            Spacer(Modifier.height(2.dp))
            Text(
                text = stringResource(R.string.booking_cancel_plus_subtitle, plusFreeHours),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.primary,
            )
        }
        Spacer(Modifier.height(8.dp))
        PolicyTier(
            label = stringResource(R.string.booking_cancel_tier1_when_plus, freeHours),
            value = stringResource(R.string.booking_cancel_tier1_value),
            valueColor = SuccessText,
        )
        if (showMidTier) {
            PolicyTier(
                label = stringResource(
                    R.string.booking_cancel_tier2_when_range,
                    penaltyHours,
                    freeHours,
                ),
                value = stringResource(R.string.booking_cancel_tier2_value),
            )
        }
        PolicyTier(
            label = stringResource(R.string.booking_cancel_tier3_when_under, penaltyHours),
            value = stringResource(R.string.booking_cancel_tier3_value),
            valueColor = MaterialTheme.colorScheme.error,
        )
    }
}

@Composable
private fun PolicyTier(label: String, value: String, valueColor: androidx.compose.ui.graphics.Color = androidx.compose.ui.graphics.Color.Unspecified) {
    Row(
        Modifier.fillMaxWidth().padding(vertical = 3.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text(
            label,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.weight(1f),
        )
        Text(
            value,
            style = MaterialTheme.typography.bodySmall.copy(fontWeight = FontWeight.SemiBold),
            color = if (valueColor == androidx.compose.ui.graphics.Color.Unspecified) MaterialTheme.colorScheme.onSurface else valueColor,
        )
    }
}

@Composable
private fun SummaryCard(content: @Composable () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(16.dp))
            .padding(14.dp),
    ) { content() }
}

/**
 * Two-column row: small label on the left ("Address"), value on the right
 * ("Zenklova 545/6"). Easier to scan than a bare icon + text and matches the
 * receipt-style summary the customer expects on the confirm step.
 */
@Composable
private fun LabeledInfoRow(icon: ImageVector, label: String, value: String) {
    Row(Modifier.padding(vertical = 4.dp).fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        Icon(icon, null, Modifier.size(16.dp), tint = MaterialTheme.colorScheme.onSurfaceVariant)
        Spacer(Modifier.width(8.dp))
        Text(
            label,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.width(72.dp),
        )
        Text(
            value,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface,
            maxLines = 2,
            overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis,
        )
    }
}

@Composable
private fun PaymentOption(icon: ImageVector, title: String, subtitle: String, selected: Boolean, onClick: () -> Unit) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(if (selected) selectionTint() else MaterialTheme.colorScheme.surface)
            .clickable(onClick = onClick),
    ) {
        if (selected) {
            Box(
                modifier = Modifier
                    .fillMaxHeight()
                    .width(3.dp)
                    .background(MaterialTheme.colorScheme.primary),
            )
        }
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .border(
                    if (selected) 0.dp else 1.dp,
                    if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outlineVariant,
                    RoundedCornerShape(14.dp),
                )
                .padding(start = if (selected) 17.dp else 14.dp, end = 14.dp, top = 14.dp, bottom = 14.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Box(
                Modifier.size(40.dp).background(MaterialTheme.colorScheme.primary.copy(alpha = 0.15f), CircleShape),
                contentAlignment = Alignment.Center,
            ) { Icon(icon, null, tint = MaterialTheme.colorScheme.primary, modifier = Modifier.size(20.dp)) }
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) {
                Text(title, style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold), color = MaterialTheme.colorScheme.onSurface)
                Text(subtitle, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
    }
}

@Composable
private fun TrustBadge(icon: ImageVector, text: String, modifier: Modifier) {
    Row(modifier = modifier, verticalAlignment = Alignment.CenterVertically) {
        Icon(icon, null, tint = SuccessText, modifier = Modifier.size(20.dp))
        Spacer(Modifier.width(8.dp))
        Text(
            text,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurface,
            maxLines = 2,
            overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis,
        )
    }
}

@Composable
private fun SectionLabel(text: String) {
    Text(text, style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold), color = MaterialTheme.colorScheme.onBackground)
}

/**
 * Wolt-style entry row for the promo-code dialog. Tappable list row with a
 * leading ticket icon, title, and a trailing chevron when nothing's applied or
 * a "code · clear" pair when a validated code is in [appliedCode]. Tapping
 * the row anywhere fires [onClick].
 */
@Composable
private fun PromoCodeRow(
    appliedCode: String,
    onClick: () -> Unit,
    onClear: () -> Unit,
) {
    CodeEntryRow(
        icon = Icons.Outlined.ConfirmationNumber,
        title = stringResource(R.string.booking_promo_code_row_title),
        appliedCode = appliedCode,
        appliedSuffixRes = R.string.booking_promo_code_row_applied,
        clearContentDescriptionRes = R.string.booking_promo_code_row_clear,
        onClick = onClick,
        onClear = onClear,
    )
}


/**
 * Shared layout for the two row variants — leading icon, title (+ applied
 * subtitle), trailing chevron when empty / clear button when applied. Single
 * surface card matches other selectable rows on the screen (e.g. PaymentOption).
 */
@Composable
private fun CodeEntryRow(
    icon: ImageVector,
    title: String,
    appliedCode: String,
    appliedSuffixRes: Int,
    clearContentDescriptionRes: Int,
    onClick: () -> Unit,
    onClear: () -> Unit,
) {
    val hasApplied = appliedCode.isNotBlank()
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(14.dp))
            .clickable(onClick = onClick)
            .padding(horizontal = 14.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            Modifier
                .size(36.dp)
                .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.15f), CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                icon,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(20.dp),
            )
        }
        Spacer(Modifier.width(12.dp))
        Column(Modifier.weight(1f)) {
            Text(
                title,
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            if (hasApplied) {
                Text(
                    stringResource(appliedSuffixRes, appliedCode.trim().uppercase()),
                    style = MaterialTheme.typography.bodySmall,
                    color = SuccessText,
                )
            }
        }
        if (hasApplied) {
            IconButton(onClick = onClear) {
                Icon(
                    imageVector = Icons.Outlined.Close,
                    contentDescription = stringResource(clearContentDescriptionRes),
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.size(20.dp),
                )
            }
        } else {
            Icon(
                imageVector = Icons.AutoMirrored.Outlined.KeyboardArrowRight,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(20.dp),
            )
        }
    }
}
