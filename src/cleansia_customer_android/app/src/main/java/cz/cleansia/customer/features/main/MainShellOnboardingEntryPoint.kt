package cz.cleansia.customer.features.main

import cz.cleansia.customer.core.settings.AppSettingsRepository
import cz.cleansia.customer.core.user.UserRepository
import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent

// MainShell needs read-access to both repos to decide whether to nudge the user
// into onboarding. A single EntryPoint avoids resolving the app component twice.
@EntryPoint
@InstallIn(SingletonComponent::class)
interface MainShellOnboardingEntryPoint {
    fun userRepository(): UserRepository
    fun appSettings(): AppSettingsRepository
}
