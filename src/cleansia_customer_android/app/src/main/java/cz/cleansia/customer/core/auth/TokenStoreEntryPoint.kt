package cz.cleansia.customer.core.auth

import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent

/**
 * Exposes [TokenStore] to non-Hilt composables (e.g. the splash branch inside
 * the navigation host). Prefer `hiltViewModel()` or constructor-injected VMs
 * over this pattern in feature code.
 */
@EntryPoint
@InstallIn(SingletonComponent::class)
interface TokenStoreEntryPoint {
    fun tokenStore(): TokenStore
    fun sessionManager(): SessionManager
}
