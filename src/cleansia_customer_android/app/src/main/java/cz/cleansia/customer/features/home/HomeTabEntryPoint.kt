package cz.cleansia.customer.features.home

import cz.cleansia.customer.core.catalog.CatalogRepository
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.loyalty.LoyaltyRepository
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.recurring.RecurringBookingRepository
import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent

@EntryPoint
@InstallIn(SingletonComponent::class)
interface HomeTabEntryPoint {
    fun addressRepository(): AddressRepository
    fun orderRepository(): OrderRepository
    fun loyaltyRepository(): LoyaltyRepository
    fun membershipRepository(): MembershipRepository
    fun catalogRepository(): CatalogRepository
    fun recurringBookingRepository(): RecurringBookingRepository
}
