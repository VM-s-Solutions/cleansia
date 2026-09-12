package cz.cleansia.customer.core.auth

import cz.cleansia.core.auth.SessionManager
import cz.cleansia.core.auth.TokenStore
import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent

/**
 * Exposes [TokenStore] outside the Hilt graph, to the `Application` that starts Sentry's user
 * tracking before any ViewModel exists. Feature code takes it through `hiltViewModel()` or a
 * constructor-injected ViewModel, never through this.
 */
@EntryPoint
@InstallIn(SingletonComponent::class)
interface TokenStoreEntryPoint {
    fun tokenStore(): TokenStore
    fun sessionManager(): SessionManager
}
