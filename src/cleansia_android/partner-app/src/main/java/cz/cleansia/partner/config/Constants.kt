package cz.cleansia.partner.config

/**
 * Application-wide constants
 */
object Constants {

    /**
     * Network timeouts in seconds
     */
    object Network {
        const val CONNECT_TIMEOUT_SECONDS = 30L
        const val READ_TIMEOUT_SECONDS = 30L
        const val WRITE_TIMEOUT_SECONDS = 30L
    }

    /**
     * Pagination defaults
     */
    object Pagination {
        const val DEFAULT_PAGE_SIZE = 20
        const val INITIAL_PAGE = 1
    }

    /**
     * DataStore keys
     */
    object DataStore {
        const val PREFERENCES_NAME = "cleansia_preferences"
        const val KEY_AUTH_TOKEN = "auth_token"
        const val KEY_USER_ID = "user_id"
        const val KEY_USER_EMAIL = "user_email"
        const val KEY_REMEMBER_ME = "remember_me"
        const val KEY_LANGUAGE = "language"
        const val KEY_THEME = "theme"
    }

    /**
     * Deep link constants
     */
    object DeepLink {
        const val SCHEME = "cleansia"
        const val HOST = "partner"
        const val HTTPS_HOST = "partner.cleansia.cz"
        const val PATH_ORDERS = "orders"
        const val PATH_INVOICES = "invoices"
    }

    /**
     * Date and time formats
     */
    object DateFormat {
        const val API_DATE_FORMAT = "yyyy-MM-dd"
        const val API_DATETIME_FORMAT = "yyyy-MM-dd'T'HH:mm:ss"
        const val API_DATETIME_FORMAT_WITH_ZONE = "yyyy-MM-dd'T'HH:mm:ss.SSSXXX"
        const val DISPLAY_DATE_FORMAT = "dd.MM.yyyy"
        const val DISPLAY_DATETIME_FORMAT = "dd.MM.yyyy HH:mm"
        const val DISPLAY_TIME_FORMAT = "HH:mm"
    }

    /**
     * File upload constants
     */
    object Upload {
        const val MAX_IMAGE_SIZE_MB = 10
        const val MAX_DOCUMENT_SIZE_MB = 25
        const val IMAGE_COMPRESSION_QUALITY = 85
        val ALLOWED_IMAGE_TYPES = listOf("image/jpeg", "image/png", "image/webp")
        val ALLOWED_DOCUMENT_TYPES = listOf("application/pdf", "image/jpeg", "image/png")
    }

    /**
     * Animation durations in milliseconds
     */
    object Animation {
        const val SHORT_DURATION = 150
        const val MEDIUM_DURATION = 300
        const val LONG_DURATION = 500
    }
}
