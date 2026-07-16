plugins {
    alias(libs.plugins.android.library)
    alias(libs.plugins.kotlin.android)
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.kotlin.serialization)
    alias(libs.plugins.ksp)
    alias(libs.plugins.hilt)
}

android {
    namespace = "cz.cleansia.core"
    compileSdk = 35

    defaultConfig {
        minSdk = 26
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
        consumerProguardFiles("consumer-rules.pro")
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro",
            )
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_21
        targetCompatibility = JavaVersion.VERSION_21
        isCoreLibraryDesugaringEnabled = true
    }

    kotlin {
        jvmToolchain(21)
    }

    buildFeatures {
        compose = true
        buildConfig = false
    }

    testOptions {
        // Pure-JVM unit tests can't link against android.jar — calls into
        // android.util.Log etc. blow up with "Method X not mocked." This flag
        // makes those return zero/null/false instead, which is what we want
        // for fire-and-forget logging calls inside classes under test.
        unitTests.isReturnDefaultValues = true
    }
}

dependencies {
    // Java 21 core library desugaring — both apps already enable it; the library
    // module needs the same so its public surface compiles against the same
    // bytecode shape.
    coreLibraryDesugaring(libs.desugar.jdk.libs)

    // AndroidX core / Compose — :core provides shared UI primitives + theme
    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.appcompat)
    implementation(libs.androidx.lifecycle.runtime.ktx)
    implementation(platform(libs.compose.bom))
    implementation(libs.compose.ui)
    implementation(libs.compose.ui.graphics)
    implementation(libs.compose.ui.tooling.preview)
    implementation(libs.compose.material3)
    implementation(libs.compose.material.icons)
    implementation(libs.compose.foundation)
    // Google Fonts provider — Type.kt references it for Poppins + Nunito.
    implementation(libs.compose.ui.text.google.fonts)

    // Hilt — :core declares @Module entries (e.g. TokenStore provider) that
    // both apps' SingletonComponent picks up.
    implementation(libs.hilt.android)
    ksp(libs.hilt.compiler)

    // Networking — TokenStore / AuthInterceptor / AuthAuthenticator + NetworkCall
    // helper live here.
    implementation(libs.retrofit)
    implementation(libs.retrofit.kotlinx.serialization)
    implementation(libs.okhttp)
    implementation(libs.okhttp.logging)

    // Serialization — IntEnumSerializers + JSON config
    implementation(libs.kotlinx.serialization.json)
    implementation(libs.kotlinx.coroutines.android)
    // OrderFormatters uses kotlinx-datetime for Instant/LocalDateTime.
    implementation(libs.kotlinx.datetime)

    // Firebase Cloud Messaging — PushTokenRepository drives the FCM token
    // lifecycle (fetch / rotate / delete) shared by both apps. The BOM aligns
    // transitive Firebase versions; only the messaging artifact is needed.
    implementation(platform(libs.firebase.bom))
    implementation(libs.firebase.messaging.ktx)

    // Persistence — TokenStore uses EncryptedSharedPreferences; preferences DataStore
    // is the future-proof option some shared utilities may consume.
    implementation(libs.androidx.datastore.preferences)
    implementation(libs.androidx.security.crypto)

    // Sentry — OkHttp event listener + SentryUserTracker. Customer wires both
    // up; partner doesn't run Sentry yet but the deps are harmless (no-op
    // when the SDK isn't initialized).
    implementation(libs.sentry.android)
    implementation(libs.sentry.okhttp)

    // Location + Mapbox stack — :core hosts LocationService (FusedLocation
    // wrapper), ReverseGeocodingService (Mapbox Geocoding v5 forward +
    // reverse), MapStyles. Mapbox deps are `api` because the picker
    // composables that consumer apps build invoke MapboxMap directly;
    // making them `api` lets each app import the symbols without
    // re-declaring the dep. FusedLocation is `api` for the same reason —
    // consumers (e.g. partner Orders) directly reference UserLocation.
    api(libs.play.services.location)
    api(libs.mapbox.maps)
    api(libs.mapbox.compose)

    // libphonenumber — used by CleansiaPhoneInput for region-aware
    // format-as-you-type + validation. ~700kB APK cost, replaces any
    // hand-rolled phone mask.
    implementation(libs.libphonenumber)

    testImplementation(libs.junit)
    testImplementation(libs.okhttp.mockwebserver)
    testImplementation(libs.mockk)
    testImplementation(libs.kotlinx.coroutines.test)
}
