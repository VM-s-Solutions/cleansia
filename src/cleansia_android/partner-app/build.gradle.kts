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

        // Sentry DSN — read from ~/.gradle/gradle.properties (SENTRY_DSN) or CI env, same seam the
        // customer app uses. Empty string = Sentry stays dormant, so a clone without a DSN still
        // runs; the init in CleansiaPartnerApp guards on it.
        val sentryDsn = providers.gradleProperty("SENTRY_DSN").orNull
            ?: System.getenv("SENTRY_DSN")
                    ?: ""
        buildConfigField("String", "SENTRY_DSN", "\"$sentryDsn\"")
    }

    // Backend API base URL override, same seam the customer app exposes. Previously this app
    // hardcoded a URL per build type with no way to redirect it, so pointing it at a different
    // backend meant editing this file — which is exactly the kind of asymmetry that makes the two
    // apps drift. A non-null value here wins for EVERY build type; otherwise each type keeps its
    // own default below.
    //     ./gradlew :partner-app:installDebug -PAPI_BASE_URL=http://10.0.2.2:5002/
    val apiBaseUrlOverride: String? = providers.gradleProperty("API_BASE_URL").orNull
        ?: System.getenv("API_BASE_URL")

    // Same shape as customer-app so the two apps sign identically. The keystore is owner-supplied
    // and gitignored (`*.jks`); the three secrets come from the environment, never a tracked file.
    // The `exists()` guard keeps debug builds and IDE sync working on a machine without it — the
    // release-only assertion below is what stops that leniency reaching an upload.
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
            isMinifyEnabled = false
            applicationIdSuffix = ".debug"
            versionNameSuffix = "-debug"
            // Defaults to the Azure DEV host, matching what iOS ships in CleansiaPartner/project.yml
            // so both platforms hit the same backend out of the box. Trailing slash, no `/api`
            // suffix — the generated OpenAPI client's method paths already start with
            // `api/Auth/Login`. NetworkModule normalises a missing slash anyway.
            val url = apiBaseUrlOverride
                ?: "https://api-cleansia-partner-mobile-weu-dev.azurewebsites.net/"
            buildConfigField("String", "API_BASE_URL", "\"$url\"")
        }
        create("staging") {
            isMinifyEnabled = true
            applicationIdSuffix = ".staging"
            versionNameSuffix = "-staging"
            // `staging-api.cleansia.cz` never existed — no DNS, no Azure resource, no bicep binding.
            // Until a real staging host is provisioned this points where debug points, so a staging
            // build is a minified build of the same backend rather than a build of nothing.
            val url = apiBaseUrlOverride
                ?: "https://api-cleansia-partner-mobile-weu-dev.azurewebsites.net/"
            buildConfigField("String", "API_BASE_URL", "\"$url\"")
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
            signingConfig = signingConfigs.getByName("debug")
        }
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            // Was `https://api.cleansia.cz/`, which has never resolved — there is no prod resource
            // group, no binding, no certificate, and the only other mentions in the tree are a
            // commented-out line in a bicepparam whose own header says "AUTHORED, NOT DEPLOYED".
            // A release build shipped against it failed every request at DNS. The Azure DEV host is
            // what debug uses and what iOS TestFlight already ships; `-PAPI_BASE_URL` redirects it
            // to a real prod host later without editing this file.
            val url = apiBaseUrlOverride
                ?: "https://api-cleansia-partner-mobile-weu-dev.azurewebsites.net/"
            buildConfigField("String", "API_BASE_URL", "\"$url\"")
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
            signingConfig = signingConfigs.getByName("release")
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

    testOptions {
        // Matches :core and :customer-app. Pure-JVM unit tests cannot link against android.jar, so
        // a fire-and-forget android.util.Log call inside a repository under test blew up with
        // "Method w in android.util.Log not mocked" — on the error branch, which is the branch most
        // worth testing.
        unitTests.isReturnDefaultValues = true
    }
}

// ─── Release signing assertion ──────────────────────────────────────
// AGP treats an incomplete signingConfig as "package it unsigned" and emits the artifact with
// no error at all, so the first signal that the keystore or a password was missing is Play
// rejecting the upload — after a 50 MB round trip. Assert instead.
//
// Only fires when an explicit release-packaging task was requested, so debug builds, unit tests
// and IDE sync are untouched, and so `./gradlew build` on a machine without the keystore still
// works. CI (`android-ci.yml`) runs only compileDebugKotlin/testDebugUnitTest, so it never trips.
//
// Reads `startParameter.taskNames` rather than `gradle.taskGraph.whenReady` because the
// configuration cache (`org.gradle.configuration-cache=true`) disallows the latter.
run {
    // `package*Release` belongs here as much as assemble/bundle: packageRelease and
    // packageReleaseBundle are public, directly-invokable tasks that produce the artifact.
    val releaseTask = Regex("(assemble|bundle|install|publish|package)\\w*Release", RegexOption.IGNORE_CASE)
    // `taskNames` is the whole invocation, not this project's share of it, so an unscoped check
    // makes `:partner-app:bundleRelease` fail out of customer-app's build file. Match an
    // unqualified name (which targets every project) or one qualified with this project's path.
    //
    // Known limitation: these are the literal strings typed on the command line, before Gradle's
    // camelCase abbreviation is expanded, so `./gradlew bR` or `:p-a:bundleRelease` slips past.
    // Not worth solving here — `jarsigner -verify` on the artifact is the real gate, and this
    // assertion exists to catch the ordinary invocation, not to be a proof.
    val wantsRelease = gradle.startParameter.taskNames.any { name ->
        releaseTask.containsMatchIn(name) &&
            (!name.startsWith(":") || name.startsWith("${project.path}:"))
    }
    // Android Studio's "Generate Signed Bundle / APK" wizard passes the keystore as
    // -Pandroid.injected.signing.*, which AGP honours over the DSL. Signing IS configured on that
    // path, and asserting would break a flow that worked.
    val injectedSigning =
        providers.gradleProperty("android.injected.signing.store.file").orNull != null
    if (wantsRelease && !injectedSigning) {
        val missing = buildList {
            if (!rootProject.file("keystore/release.jks").exists()) add("keystore/release.jks")
            if (providers.environmentVariable("RELEASE_KEYSTORE_PASSWORD").orNull.isNullOrBlank()) {
                add("RELEASE_KEYSTORE_PASSWORD")
            }
            if (providers.environmentVariable("RELEASE_KEY_ALIAS").orNull.isNullOrBlank()) {
                add("RELEASE_KEY_ALIAS")
            }
            if (providers.environmentVariable("RELEASE_KEY_PASSWORD").orNull.isNullOrBlank()) {
                add("RELEASE_KEY_PASSWORD")
            }
        }
        if (missing.isNotEmpty()) {
            throw GradleException(
                "Release signing is not configured, so this build would produce an UNSIGNED " +
                    "artifact that Google Play rejects. Missing: " + missing.joinToString(", ") +
                    ". The keystore is owner-supplied and gitignored; the three values are " +
                    "environment variables, never a tracked file."
            )
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

    // Crash reporting. Partner shipped without it, so a cleaner's crash produced nothing at all
    // — no stack trace, no report, no way to know it happened.
    implementation(libs.sentry.android)
    implementation(libs.sentry.okhttp)

    // Firebase Cloud Messaging — push notifications. The BOM aligns
    // transitive Firebase versions; we only need the messaging artifact.
    implementation(platform(libs.firebase.bom))
    implementation(libs.firebase.messaging.ktx)

    // Testing
    testImplementation(libs.junit)
    testImplementation(libs.kotlinx.coroutines.test)
    testImplementation(libs.mockk)
    testImplementation(libs.turbine)
    // Hand-written Retrofit interfaces carry the wire contract in their @Query
    // names; a mocked interface can only pin argument values, never the names
    // the server actually binds. MockWebServer lets those tests assert the URL.
    testImplementation(libs.okhttp.mockwebserver)
    androidTestImplementation(libs.androidx.junit)
    androidTestImplementation(libs.androidx.espresso.core)
    androidTestImplementation(platform(libs.androidx.compose.bom))
    androidTestImplementation(libs.androidx.ui.test.junit4)
    debugImplementation(libs.androidx.ui.tooling)
    debugImplementation(libs.androidx.ui.test.manifest)
}
