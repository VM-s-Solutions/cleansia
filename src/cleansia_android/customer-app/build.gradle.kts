import java.net.URI

plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.kotlin.serialization)
    alias(libs.plugins.ksp)
    alias(libs.plugins.hilt)
    alias(libs.plugins.spotless)
    alias(libs.plugins.google.services)
    // Generates a typed Retrofit + kotlinx.serialization client from the
    // backend's OpenAPI spec at `openapi/customer-mobile-api.json`. The
    // alternative we tried (hand-written DTOs) drifted silently from the
    // backend response shape and produced "blank screen" bugs whenever a
    // field was renamed or made required. Generated client makes those
    // mismatches compile-time errors.
    //
    // Refresh the spec with `./gradlew :app:dumpOpenApiSpec` (defined below;
    // requires the customer-mobile-api host running on 5004). The generator
    // task then runs automatically on every Kotlin compile and emits into
    // `build/generated/openapi/`.
    alias(libs.plugins.openapi.generator)
}

// FCM Phase A — owner provisions a real google-services.json from the
// Firebase console and drops it into app/ (gitignored). For first-time
// developers without Firebase access, fall back to the placeholder sample
// so `./gradlew assembleDebug` still produces a working APK — push will
// silently fail at runtime (FcmPushDispatcher logs a warning and skips
// dispatch; messaging service registers but never receives real messages).
val googleServicesFile = file("google-services.json")
if (!googleServicesFile.exists()) {
    val sample = file("google-services.sample.json")
    if (sample.exists()) {
        sample.copyTo(googleServicesFile, overwrite = false)
        logger.lifecycle(
            "google-services.json missing — copied placeholder from " +
                "google-services.sample.json. Replace with real config from " +
                "the Firebase console before shipping a release build."
        )
    }
}

android {
    namespace = "cz.cleansia.customer"
    compileSdk = 35

    defaultConfig {
        applicationId = "cz.cleansia.customer"
        minSdk = 26
        targetSdk = 35
        versionCode = 1
        versionName = "0.1.0"

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
        @Suppress("DEPRECATION")
        resourceConfigurations += listOf("en", "cs", "sk", "uk", "ru")
        vectorDrawables.useSupportLibrary = true

        // Mapbox public access token — read from ~/.gradle/gradle.properties (MAPBOX_ACCESS_TOKEN)
        // or CI env var. Empty string fallback keeps builds working without crashing; map will
        // fail to load at runtime with a clear error.
        val mapboxAccessToken = providers.gradleProperty("MAPBOX_ACCESS_TOKEN").orNull
            ?: System.getenv("MAPBOX_ACCESS_TOKEN")
                    ?: ""
        buildConfigField("String", "MAPBOX_ACCESS_TOKEN", "\"$mapboxAccessToken\"")

        // Backend API base URL — points at the dedicated Customer Mobile API
        // host (Cleansia.Web.Mobile.Customer on :5004). The Customer Web host
        // (:5003) is for browser clients and blanks body tokens per the
        // HttpOnly cookie migration; native clients need the token in the
        // JSON response (EncryptedSharedPreferences storage), which the
        // Mobile.Customer host preserves. Override in
        // `~/.gradle/gradle.properties` with `API_BASE_URL=http://192.168.1.x:5004/`
        // for real-device testing.
        val apiBaseUrl = providers.gradleProperty("API_BASE_URL").orNull
            ?: System.getenv("API_BASE_URL")
                    ?: "http://10.0.2.2:5004/"
        buildConfigField("String", "API_BASE_URL", "\"$apiBaseUrl\"")

        // Sentry DSN — read from ~/.gradle/gradle.properties (SENTRY_DSN) or CI env.
        // Empty string = Sentry stays dormant (no-op init), so dev builds without a DSN
        // still run normally. Real DSN goes into the Play-store release pipeline.
        val sentryDsn = providers.gradleProperty("SENTRY_DSN").orNull
            ?: System.getenv("SENTRY_DSN")
                    ?: ""
        buildConfigField("String", "SENTRY_DSN", "\"$sentryDsn\"")

        // Stripe publishable key (pk_test_... in dev, pk_live_... in prod). Read
        // from ~/.gradle/gradle.properties (STRIPE_PUBLISHABLE_KEY) or CI env.
        // Empty fallback so unconfigured builds don't crash at startup —
        // PaymentSheet will fail at runtime with a clear error if the key is missing.
        val stripePublishableKey = providers.gradleProperty("STRIPE_PUBLISHABLE_KEY").orNull
            ?: System.getenv("STRIPE_PUBLISHABLE_KEY")
                    ?: ""
        buildConfigField("String", "STRIPE_PUBLISHABLE_KEY", "\"$stripePublishableKey\"")

        // Google Sign-In OAuth 2.0 web client ID — the Cloud-Console "Web client"
        // entry (NOT the Android client). Credential Manager exchanges this for
        // an ID token that the backend's GoogleAuth handler verifies.
        // Empty string keeps debug builds working; the sign-in button just fails
        // at runtime with a clear message until owner provisions the value via
        // ~/.gradle/gradle.properties (GOOGLE_WEB_CLIENT_ID) or CI env.
        val googleWebClientId = providers.gradleProperty("GOOGLE_WEB_CLIENT_ID").orNull
            ?: System.getenv("GOOGLE_WEB_CLIENT_ID")
                    ?: ""
        buildConfigField("String", "GOOGLE_WEB_CLIENT_ID", "\"$googleWebClientId\"")
    }

    signingConfigs {
        create("release") {
            val keystoreFile = rootProject.file("keystore/release.jks")
            if (keystoreFile.exists()) {
                storeFile = keystoreFile
                storePassword = System.getenv("RELEASE_KEYSTORE_PASSWORD")
                keyAlias = System.getenv("RELEASE_KEY_ALIAS")
                keyPassword = System.getenv("RELEASE_KEY_PASSWORD")
            }
        }
    }

    buildTypes {
        debug {
            applicationIdSuffix = ".debug"
            versionNameSuffix = "-debug"
            isMinifyEnabled = false
        }
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro",
            )
            signingConfig = signingConfigs.getByName("release")
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
        buildConfig = true
    }

    packaging {
        resources {
            excludes += setOf(
                "/META-INF/{AL2.0,LGPL2.1}",
                "META-INF/LICENSE*",
                "META-INF/NOTICE*",
            )
        }
    }

    testOptions {
        // Pure-JVM unit tests can't link against android.jar — calls into
        // android.util.Log etc. blow up with "Method X not mocked." This flag
        // makes those return zero/null/false instead, which is what we want
        // for fire-and-forget logging calls inside repos under test.
        unitTests.isReturnDefaultValues = true
    }
}

spotless {
    kotlin {
        target("src/**/*.kt")
        targetExclude("**/generated/**", "**/build/**")
        ktlint(libs.versions.ktlint.get())
    }
    kotlinGradle {
        target("*.gradle.kts")
        ktlint(libs.versions.ktlint.get())
    }
}

// ─── OpenAPI generated client ───────────────────────────────────────
// Reads the spec dumped from the running customer-mobile-api host (5004)
// and produces typed Retrofit interfaces + kotlinx.serialization data
// classes under cz.cleansia.customer.api.* — see the plugin alias above
// for the regen command. Generated code is treated as compile input
// only; nothing is committed under build/.

openApiGenerate {
    generatorName.set("kotlin")
    inputSpec.set("${rootDir}/openapi/customer-mobile-api.json")
    outputDir.set("${layout.buildDirectory.get()}/generated/openapi")
    apiPackage.set("cz.cleansia.customer.api.client")
    modelPackage.set("cz.cleansia.customer.api.model")
    invokerPackage.set("cz.cleansia.customer.api.infrastructure")
    // jvm-retrofit2 wires our existing OkHttp client; multiplatform-* and
    // jvm-ktor wouldn't fit the existing infra. kotlinx-serialization for
    // DTO codecs (matches the rest of the app).
    library.set("jvm-retrofit2")
    configOptions.set(mapOf(
        "serializationLibrary" to "kotlinx_serialization",
        "useCoroutines" to "true",
        "dateLibrary" to "kotlinx-datetime",
        // The default `MarshallableExtensionFunctions.kt` adds extension
        // functions on java.lang types that conflict with kotlinx — disable.
        "generateExtensions" to "false",
        "enumPropertyNaming" to "UPPERCASE",
        // Don't emit the .gitignore / build files; we don't commit generated.
        "omitGradleWrapper" to "true",
    ))
    // Emit only the supporting files we actually need at compile time —
    // `CollectionFormats.kt` is referenced by every generated Api interface
    // via `org.openapitools.client.infrastructure.CollectionFormats.*`.
    // Skip README, gradle wrapper, tests, etc. (those leak project metadata
    // about the OpenAPI Generator itself into build/).
    globalProperties.set(mapOf(
        "models" to "",
        "apis" to "",
        "supportingFiles" to "CollectionFormats.kt",
    ))
    skipOverwrite.set(false)
}

android {
    sourceSets.getByName("main") {
        kotlin.srcDir("${layout.buildDirectory.get()}/generated/openapi/src/main/kotlin")
    }
}

// Make every Kotlin compile depend on the generator so the generated
// sources are present before kotlinc resolves them. Using `preBuild`
// alone wasn't enough — KSP/Hilt tasks resolve sources earlier.
tasks.matching { it.name.startsWith("compile") && it.name.endsWith("Kotlin") }
    .configureEach { dependsOn("openApiGenerate") }
tasks.matching { it.name.startsWith("ksp") && it.name.endsWith("Kotlin") }
    .configureEach { dependsOn("openApiGenerate") }

// Fetch the latest OpenAPI spec from the running customer-mobile-api host
// (port 5004) and overwrite the on-disk copy that openApiGenerate reads.
// Override the URL with `-PopenApiUrl=http://192.168.1.x:5004/swagger/v1/swagger.json`
// for real-device testing. Failure (e.g. host not running) prints a clear
// hint instead of a cryptic IOException.
//
// Capture every Project-derived value at configuration time (specUrl,
// specFile, repoRoot) so the doLast closure only references local vals —
// configuration-cache disallows live Project references inside task actions.
val openApiUrl = (project.findProperty("openApiUrl") as String?)
    ?: "http://localhost:5004/swagger/v1/swagger.json"
val openApiSpecFile = rootProject.file("openapi/customer-mobile-api.json")
val openApiRepoRoot = rootDir
tasks.register("dumpOpenApiSpec") {
    group = "openapi"
    description = "Download the latest OpenAPI spec from the running customer-mobile-api host."
    // This is an interactive dev task (refresh-spec-from-running-host); it
    // doesn't fit Gradle's pure/cacheable model. Opting out of the
    // configuration cache for this one task lets us keep things tidy —
    // every other task in the build still benefits from cache.
    notCompatibleWithConfigurationCache(
        "dumpOpenApiSpec performs a network fetch and is invoked manually; " +
            "config-cache compatibility isn't worth the contortions.",
    )
    doLast {
        println("Fetching OpenAPI spec from $openApiUrl ...")
        try {
            openApiSpecFile.parentFile.mkdirs()
            val url = URI.create(openApiUrl).toURL()
            url.openStream().use { input: java.io.InputStream ->
                openApiSpecFile.outputStream().use { output: java.io.OutputStream ->
                    input.copyTo(output)
                }
            }
            println(
                "Wrote ${openApiSpecFile.length()} bytes to " +
                    "${openApiSpecFile.relativeTo(openApiRepoRoot)}.",
            )
        } catch (e: Exception) {
            throw GradleException(
                "Could not fetch OpenAPI spec from $openApiUrl. " +
                    "Is the customer-mobile-api host running on port 5004? " +
                    "Original error: ${e.message}",
                e,
            )
        }
    }
}

dependencies {
    implementation(project(":core"))

    coreLibraryDesugaring("com.android.tools:desugar_jdk_libs:2.1.4")

    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.appcompat)
    implementation(libs.androidx.splashscreen)
    implementation(libs.androidx.lifecycle.runtime.ktx)
    implementation(libs.androidx.lifecycle.viewmodel.compose)
    implementation(libs.androidx.lifecycle.runtime.compose)

    implementation(libs.androidx.activity.compose)
    implementation(platform(libs.compose.bom))
    implementation(libs.compose.ui)
    implementation(libs.compose.ui.graphics)
    implementation(libs.compose.ui.tooling.preview)
    implementation(libs.compose.ui.text.google.fonts)
    implementation(libs.compose.material3)
    implementation(libs.compose.material.icons)
    implementation(libs.compose.foundation)
    debugImplementation(libs.compose.ui.tooling)

    implementation(libs.androidx.navigation.compose)

    implementation(libs.hilt.android)
    ksp(libs.hilt.compiler)
    implementation(libs.hilt.navigation.compose)

    implementation(libs.kotlinx.coroutines.android)
    implementation(libs.kotlinx.serialization.json)
    implementation(libs.kotlinx.datetime)

    implementation(libs.retrofit)
    implementation(libs.retrofit.kotlinx.serialization)
    implementation(libs.okhttp)
    implementation(libs.okhttp.logging)

    implementation(libs.coil.compose)
    implementation(libs.coil.network.okhttp)
    implementation(libs.coil.gif)

    implementation(libs.androidx.datastore.preferences)
    implementation(libs.androidx.security.crypto)

    implementation(libs.mapbox.maps)
    implementation(libs.mapbox.compose)
    implementation(libs.play.services.location)

    implementation(libs.sentry.android)
    implementation(libs.sentry.okhttp)

    implementation(libs.stripe.android)

    // Firebase Cloud Messaging — push notifications. The BOM aligns
    // transitive Firebase versions; we only need the messaging artifact
    // (Sentry handles crashes; no analytics).
    implementation(platform(libs.firebase.bom))
    implementation(libs.firebase.messaging.ktx)

    // Credential Manager + Google ID for Sign in with Google.
    implementation(libs.androidx.credentials)
    implementation(libs.androidx.credentials.play.services.auth)
    implementation(libs.googleid)

    testImplementation(libs.junit)
    testImplementation(libs.kotlinx.coroutines.test)
    testImplementation(libs.mockk)
    testImplementation(libs.turbine)
}
