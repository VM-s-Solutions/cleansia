package cz.cleansia.customer.core.network

import cz.cleansia.core.network.IntValueEnumSerializer
import cz.cleansia.customer.api.model.AppliedDiscountSource
import cz.cleansia.customer.api.model.CancellationFeeTier
import cz.cleansia.customer.api.model.ConsentType
import cz.cleansia.customer.api.model.ContractStatus
import cz.cleansia.customer.api.model.DisputeReason
import cz.cleansia.customer.api.model.EmployeeEntityType
import cz.cleansia.customer.api.model.EmployeeInvoiceStatus
import cz.cleansia.customer.api.model.LoyaltyEarnSource
import cz.cleansia.customer.api.model.LoyaltyTier
import cz.cleansia.customer.api.model.LoyaltyTransactionType
import cz.cleansia.customer.api.model.MembershipStatus
import cz.cleansia.customer.api.model.OrderStatus
import cz.cleansia.customer.api.model.PaymentStatus
import cz.cleansia.customer.api.model.PaymentType
import cz.cleansia.customer.api.model.PhotoType
import cz.cleansia.customer.api.model.ReferralStatus
import cz.cleansia.customer.api.model.ReviewTag
import cz.cleansia.customer.api.model.SortDirection
import kotlinx.serialization.modules.SerializersModule

/**
 * The customer app's int-enum bindings; the reusable codec lives in `:core`.
 *
 * **Add an entry whenever a new int-enum lands** — the generated client emits these as quoted integers,
 * and an unregistered one fails to deserialize at runtime rather than at build.
 * -> /architecture/backend#tolerant-json
 */
val IntEnumSerializersModule = SerializersModule {
    contextual(AppliedDiscountSource::class, IntValueEnumSerializer(
        "AppliedDiscountSource",
        { it.value },
        { raw -> AppliedDiscountSource.entries.firstOrNull { it.value == raw } },
    ))
    contextual(CancellationFeeTier::class, IntValueEnumSerializer(
        "CancellationFeeTier",
        { it.value },
        { raw -> CancellationFeeTier.entries.firstOrNull { it.value == raw } },
    ))
    contextual(ConsentType::class, IntValueEnumSerializer(
        "ConsentType",
        { it.value },
        { raw -> ConsentType.entries.firstOrNull { it.value == raw } },
    ))
    contextual(ContractStatus::class, IntValueEnumSerializer(
        "ContractStatus",
        { it.value },
        { raw -> ContractStatus.entries.firstOrNull { it.value == raw } },
    ))
    contextual(DisputeReason::class, IntValueEnumSerializer(
        "DisputeReason",
        { it.value },
        { raw -> DisputeReason.entries.firstOrNull { it.value == raw } },
    ))
    contextual(EmployeeEntityType::class, IntValueEnumSerializer(
        "EmployeeEntityType",
        { it.value },
        { raw -> EmployeeEntityType.entries.firstOrNull { it.value == raw } },
    ))
    contextual(EmployeeInvoiceStatus::class, IntValueEnumSerializer(
        "EmployeeInvoiceStatus",
        { it.value },
        { raw -> EmployeeInvoiceStatus.entries.firstOrNull { it.value == raw } },
    ))
    contextual(LoyaltyEarnSource::class, IntValueEnumSerializer(
        "LoyaltyEarnSource",
        { it.value },
        { raw -> LoyaltyEarnSource.entries.firstOrNull { it.value == raw } },
    ))
    contextual(LoyaltyTier::class, IntValueEnumSerializer(
        "LoyaltyTier",
        { it.value },
        { raw -> LoyaltyTier.entries.firstOrNull { it.value == raw } },
    ))
    contextual(LoyaltyTransactionType::class, IntValueEnumSerializer(
        "LoyaltyTransactionType",
        { it.value },
        { raw -> LoyaltyTransactionType.entries.firstOrNull { it.value == raw } },
    ))
    contextual(MembershipStatus::class, IntValueEnumSerializer(
        "MembershipStatus",
        { it.value },
        { raw -> MembershipStatus.entries.firstOrNull { it.value == raw } },
    ))
    contextual(OrderStatus::class, IntValueEnumSerializer(
        "OrderStatus",
        { it.value },
        { raw -> OrderStatus.entries.firstOrNull { it.value == raw } },
    ))
    contextual(PaymentStatus::class, IntValueEnumSerializer(
        "PaymentStatus",
        { it.value },
        { raw -> PaymentStatus.entries.firstOrNull { it.value == raw } },
    ))
    contextual(PaymentType::class, IntValueEnumSerializer(
        "PaymentType",
        { it.value },
        { raw -> PaymentType.entries.firstOrNull { it.value == raw } },
    ))
    contextual(PhotoType::class, IntValueEnumSerializer(
        "PhotoType",
        { it.value },
        { raw -> PhotoType.entries.firstOrNull { it.value == raw } },
    ))
    contextual(ReferralStatus::class, IntValueEnumSerializer(
        "ReferralStatus",
        { it.value },
        { raw -> ReferralStatus.entries.firstOrNull { it.value == raw } },
    ))
    contextual(ReviewTag::class, IntValueEnumSerializer(
        "ReviewTag",
        { it.value },
        { raw -> ReviewTag.entries.firstOrNull { it.value == raw } },
    ))
    contextual(SortDirection::class, IntValueEnumSerializer(
        "SortDirection",
        { it.value },
        { raw -> SortDirection.entries.firstOrNull { it.value == raw } },
    ))
}
