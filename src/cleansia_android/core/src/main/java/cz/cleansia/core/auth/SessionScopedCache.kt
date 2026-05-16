package cz.cleansia.core.auth

/**
 * Marker for any repository that holds user-scoped state and must be flushed
 * on sign-out (forced or voluntary). The auth layer iterates the multibinding
 * `Set<SessionScopedCache>` and calls [clear] on every implementor — drop-in:
 * just `@Binds @IntoSet` your repo into the `SessionScopedModule` and
 * implement this interface.
 *
 * Why a multibinding over individual constructor params:
 *   - Adding a new cache is a one-line change (binding + interface), not a
 *     six-file cascade through [AuthRepository] + [AuthAuthenticator] + their
 *     `@Provides` methods.
 *   - The "INVARIANT: keep AuthAuthenticator and AuthRepository in sync" comment
 *     becomes structural rather than aspirational — both clear-paths iterate
 *     the same Set, so they CAN'T drift.
 *
 * The method is `suspend` because most existing impls are `suspend` (DataStore-backed
 * caches need to write atomically). Pure in-memory caches can implement it as a
 * trivial non-suspending body — `suspend` doesn't require the body to actually
 * suspend, only allows it.
 */
interface SessionScopedCache {
    suspend fun clear()
}
