package cz.cleansia.core.notifications

import javax.inject.Qualifier

/**
 * Qualifies the preferences `DataStore` that backs [PushTokenRepository]. Each
 * app provides one with its own store name (the customer and partner apps must
 * not share a file) so the DataStore name is parameterized per-app rather than
 * hardcoded in `:core`.
 */
@Qualifier
@Retention(AnnotationRetention.BINARY)
annotation class PushTokenDataStore
