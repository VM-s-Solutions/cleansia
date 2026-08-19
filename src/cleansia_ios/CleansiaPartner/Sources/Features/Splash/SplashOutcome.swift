enum SplashOutcome: Equatable {
    case authenticated
    case unauthenticated
    case needsOnboarding
    case needsRegistrationLock

    /// The backend did not answer. NOT a variant of `needsRegistrationLock` and it must never
    /// collapse back into one: the lock screen says "you are not approved yet", which an
    /// unanswered call says nothing about. The two were indistinguishable, so a cleaner on bad
    /// signal — or an app reviewer on hotel wifi — was told to upload documents they had already
    /// uploaded, with signing out as the only exit.
    ///
    /// The fail-closed reading is unchanged: this still admits nobody to the dashboard. It is the
    /// only outcome that does not navigate anywhere — `SplashGateView` holds and offers a retry.
    case unreachable
}
