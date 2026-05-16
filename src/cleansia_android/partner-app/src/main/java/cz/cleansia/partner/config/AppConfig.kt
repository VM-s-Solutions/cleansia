package cz.cleansia.partner.config

import cz.cleansia.partner.BuildConfig

/**
 * Application configuration that provides access to build-time constants
 * and environment-specific settings.
 */
object AppConfig {

    /**
     * Base URL for API calls, configured per build variant
     */
    val apiBaseUrl: String = BuildConfig.API_BASE_URL

    /**
     * Whether the app is running in debug mode
     */
    val isDebug: Boolean = BuildConfig.DEBUG

    /**
     * Application version name
     */
    val versionName: String = BuildConfig.VERSION_NAME

    /**
     * Application version code
     */
    val versionCode: Int = BuildConfig.VERSION_CODE

    /**
     * Application package name
     */
    val applicationId: String = BuildConfig.APPLICATION_ID
}
