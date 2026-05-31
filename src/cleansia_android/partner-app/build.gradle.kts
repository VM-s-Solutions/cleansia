import java.net.URI

plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.hilt.android)
    alias(libs.plugins.ksp)
    alias(libs.plugins.kotlin.serialization)
    alias(libs.plugins.openapi.generator)
    alias(libs.plugins.google.services)
}

// FCM — owner provisions a real google-services.json from the Firebase
// console and drops it into partner-app/ (gitignored). The google-services
// plugin fails the build if the file is missing, so for first-time devs
// without Firebase access we fall back to the committed placeholder sample.
// Push silently no-ops at runtime against the placeholder (the messaging
// service registers but never receives real messages); compileDebugKotlin
// and assembleDebug both still succeed. Replace with the real config before
// shipping a release build.
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
    namespace = "cz.cleansia.partner"
    compileSdk = 35

    defaultConfig {
        applicationId = "cz.cleansia.partner"
        minSdk = 26
        targetSdk = 35
        versionCode = 1
        versionName = "1.0.0"

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"

        // Mapbox public access token — same pattern as customer-app. Reads from
        // ~/.gradle/gradle.properties (MAPBOX_ACCESS_TOKEN) or CI env. Empty
        // fallback keeps builds working; map will fail to load with a clear
        // error at runtime.
        val mapboxAccessToken = providers.gradleProperty("MAPBOX_ACCESS_TOKEN").orNull
            ?: System.getenv("MAPBOX_ACCESS_TOKEN")
                    ?: ""
        buildConfigField("String", "MAPBOX_ACCESS_TOKEN", "\"$mapboxAccessToken\"")
    }

    buildTypes {
        debug {
            isMinifyEnabled = false
            applicationIdSuffix = ".debug"
            versionNameSuffix = "-debug"
            // Trailing slash, no `/api` suffix — the generated OpenAPI client's
            // method paths already start with `api/Auth/Login` etc. NetworkModule
            // adds the trailing slash if missing, but keep it explicit here for clarity.
            buildConfigField("String", "API_BASE_URL", "\"http://10.0.2.2:5002/\"")
        }
        create("staging") {
            isMinifyEnabled = true
            applicationIdSuffix = ".staging"
            versionNameSuffix = "-staging"
            buildConfigField("String", "API_BASE_URL", "\"https://staging-api.cleansia.cz/\"")
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
            signingConfig = signingConfigs.getByName("debug")
        }
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            buildConfigField("String", "API_BASE_URL", "\"https://api.cleansia.cz/\"")
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
        }
    }

    compileOptions {
        // ARCH-001 Phase 1: bumped to Java 21 to match customer app and enable a future
        // shared :core module that has to pick a single Java target.
        sourceCompatibility = JavaVersion.VERSION_21
        targetCompatibility = JavaVersion.VERSION_21
        isCoreLibraryDesugaringEnabled = true
    }

    kotlinOptions {
        jvmTarget = "21"
    }

    buildFeatures {
        compose = true
        buildConfig = true
    }

    // Add generated sources to the source sets
    sourceSets {
        getByName("main") {
            kotlin.srcDir("${layout.buildDirectory.get()}/generated/openapi/src/main/kotlin")
        }
    }
}

// ─── OpenAPI generated client ───────────────────────────────────────
// Reads the spec dumped from the running partner-mobile-api host (5002)
// and produces typed Retrofit interfaces + kotlinx.serialization data
// classes under cz.cleansia.partner.api.* — see the plugin alias above
// for the regen command. Generated code is treated as compile input
// only; nothing is committed under build/.
//
// Spec lives at ${rootDir}/openapi/partner-mobile-api.json so both
// customer + partner specs sit side-by-side under the monorepo root.
// Refresh with `./gradlew :partner-app:dumpOpenApiSpec`.

openApiGenerate {
    generatorName.set("kotlin")
    inputSpec.set("${rootDir}/openapi/partner-mobile-api.json")
    outputDir.set("${layout.buildDirectory.get()}/generated/openapi")
    apiPackage.set("cz.cleansia.partner.api.client")
    modelPackage.set("cz.cleansia.partner.api.model")
    invokerPackage.set("cz.cleansia.partner.api.infrastructure")
    // jvm-retrofit2 wires our existing OkHttp client; multiplatform-* and
    // jvm-ktor wouldn't fit the existing infra. kotlinx-serialization for
    // DTO codecs (matches the rest of the app).
    library.set("jvm-retrofit2")
    configOptions.set(mapOf(
        "serializationLibrary" to "kotlinx_serialization",
        "useCoroutines" to "true",
        "dateLibrary" to "string",
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
    // Skip README, gradle wrapper, tests, etc.
    globalProperties.set(mapOf(
        "models" to "",
        "apis" to "",
        "supportingFiles" to "CollectionFormats.kt",
    ))
    skipOverwrite.set(false)
}

// Make every Kotlin compile depend on the generator so the generated
// sources are present before kotlinc resolves them. Using `preBuild`
// alone wasn't enough — KSP/Hilt tasks resolve sources earlier.
tasks.matching { it.name.startsWith("compile") && it.name.endsWith("Kotlin") }
    .configureEach { dependsOn("openApiGenerate") }
tasks.matching { it.name.startsWith("ksp") && it.name.endsWith("Kotlin") }
    .configureEach { dependsOn("openApiGenerate") }

// Fetch the latest OpenAPI spec from the running partner-mobile-api host
// (port 5002) and overwrite the on-disk copy that openApiGenerate reads.
// Override the URL with `-PopenApiUrl=http://192.168.1.x:5002/swagger/v1/swagger.json`
// for real-device testing. Failure (e.g. host not running) prints a clear
// hint instead of a cryptic IOException.
val openApiUrl = (project.findProperty("openApiUrl") as String?)
    ?: "http://localhost:5002/swagger/v1/swagger.json"
val openApiSpecFile = rootProject.file("openapi/partner-mobile-api.json")
val openApiRepoRoot = rootDir
tasks.register("dumpOpenApiSpec") {
    group = "openapi"
    description = "Download the latest OpenAPI spec from the running partner-mobile-api host."
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
                    "Is the partner-mobile-api host running on port 5002? " +
                    "Original error: ${e.message}",
                e,
            )
        }
    }
}

dependencies {
    // ARCH-001 Phase 3: shared :core module. Partner currently consumes only the
    // UI primitives that land in Phase 4 (theme, components). Its auth/network
    // plumbing stays app-specific until "Phase 3b" — see arch-001 plan.
    implementation(project(":core"))

    // Java 21 core library desugaring — required because compileOptions moved to JavaVersion.VERSION_21
    coreLibraryDesugaring(libs.desugar.jdk.libs)

    // Core Android
    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.lifecycle.runtime.ktx)
    implementation(libs.androidx.lifecycle.runtime.compose)
    implementation(libs.androidx.lifecycle.viewmodel.compose)
    implementation(libs.androidx.activity.compose)

    // Compose
    implementation(platform(libs.androidx.compose.bom))
    implementation(libs.androidx.ui)
    implementation(libs.androidx.ui.graphics)
    implementation(libs.androidx.ui.tooling.preview)
    implementation(libs.androidx.material3)
    implementation(libs.androidx.material.icons.extended)

    // Navigation
    implementation(libs.androidx.navigation.compose)

    // Hilt
    implementation(libs.hilt.android)
    ksp(libs.hilt.android.compiler)
    implementation(libs.hilt.navigation.compose)

    // Networking
    implementation(libs.retrofit)
    implementation(libs.retrofit.kotlinx.serialization)
    implementation(libs.retrofit.converter.scalars)
    implementation(libs.okhttp)
    implementation(libs.okhttp.logging)

    // Serialization
    implementation(libs.kotlinx.serialization.json)

    // Coroutines
    implementation(libs.kotlinx.coroutines.core)
    implementation(libs.kotlinx.coroutines.android)

    // Image Loading (Coil 3 — needs the network-okhttp module too, was bundled in Coil 2)
    implementation(libs.coil.compose)
    implementation(libs.coil.network.okhttp)
    // Animated-image decoder (GIF + animated WebP + animated HEIF on
    // Android 28+). Required for the InProgress mascot which ships
    // as an animated WebP from the shared web mascot set.
    implementation(libs.coil.gif)

    // DataStore
    implementation(libs.androidx.datastore.preferences)

    // Location — distance-to-job calc on Orders feed
    implementation(libs.play.services.location)

    // Mapbox — map preview thumbnail on each offer card
    implementation(libs.mapbox.maps)
    implementation(libs.mapbox.compose)

    // AppCompat (for locale changes)
    implementation(libs.androidx.appcompat)

    // Splash Screen
    implementation(libs.androidx.splashscreen)

    // EncryptedSharedPreferences — needed at runtime because :core TokenStore
    // references it from its compiled class (the dep is `implementation` in
    // :core so it's not exposed transitively). Deprecated by Google in 2024
    // but still the canonical secure-prefs solution; see :core TokenStore for
    // the migration plan.
    implementation(libs.androidx.security.crypto)

    // Firebase Cloud Messaging — push notifications. The BOM aligns
    // transitive Firebase versions; we only need the messaging artifact.
    implementation(platform(libs.firebase.bom))
    implementation(libs.firebase.messaging.ktx)

    // Room — local notifications-feed store. The FCM service writes a record
    // on every received push; the NotificationsScreen reads them back as a
    // Flow. Room (not DataStore) because the feed is a growing list with
    // per-row read-state, queried newest-first.
    implementation(libs.androidx.room.runtime)
    implementation(libs.androidx.room.ktx)
    ksp(libs.androidx.room.compiler)

    // Testing
    testImplementation(libs.junit)
    testImplementation(libs.kotlinx.coroutines.test)
    testImplementation(libs.mockk)
    testImplementation(libs.turbine)
    androidTestImplementation(libs.androidx.junit)
    androidTestImplementation(libs.androidx.espresso.core)
    androidTestImplementation(platform(libs.androidx.compose.bom))
    androidTestImplementation(libs.androidx.ui.test.junit4)
    debugImplementation(libs.androidx.ui.tooling)
    debugImplementation(libs.androidx.ui.test.manifest)
}
