package cz.cleansia.partner.core.auth

import cz.cleansia.core.auth.TokenStore
import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent

/**
 * Exposes [TokenStore] to non-Hilt callers. Today that is exactly one: the `Application`, which
 * needs it to hand `SentryUserTracker` a session to follow and cannot be constructor-injected.
 *
 * Mirrors the customer app's entry point of the same name, minus `sessionManager()` — the customer
 * navigation host reaches for that from a composable, and nothing in the partner app does. A member
 * added here without a caller is a member every future reader has to rule out.
 *
 * Prefer `hiltViewModel()` or a constructor-injected dependency over this pattern in feature code.
 */
@EntryPoint
@InstallIn(SingletonComponent::class)
interface TokenStoreEntryPoint {
    fun tokenStore(): TokenStore
}
