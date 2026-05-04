plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.kotlin.serialization)
    alias(libs.plugins.ksp)
    alias(libs.plugins.hilt)
    alias(libs.plugins.spotless)
    alias(libs.plugins.openapi.generator)
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

        // Backend API base URL — same pattern as Mapbox. Default (emulator) points
        // at the Windows host's localhost:5003. Override in ~/.gradle/gradle.properties
        // with `API_BASE_URL=http://192.168.1.x:5003/` for real-device testing.
        val apiBaseUrl = providers.gradleProperty("API_BASE_URL").orNull
            ?: System.getenv("API_BASE_URL")
                    ?: "http://10.0.2.2:5003/"
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

// ── OpenAPI Generator ──
// Generates a Kotlin + Retrofit + kotlinx.serialization client from the dumped
// Cleansia.Web.Customer OpenAPI spec. Run `./gradlew :app:openApiGenerate` after
// updating the spec (re-dump via `curl http://localhost:5003/swagger/v1/swagger.json`).
// Output lands under `app/build/generated/openapi/` and is picked up by the source set.
openApiGenerate {
    generatorName.set("kotlin")
    inputSpec.set("$projectDir/../openapi/customer-api.json")
    outputDir.set("${layout.buildDirectory.get()}/generated/openapi")
    apiPackage.set("cz.cleansia.customer.generated.api")
    modelPackage.set("cz.cleansia.customer.generated.model")
    invokerPackage.set("cz.cleansia.customer.generated.infrastructure")
    configOptions.set(
        mapOf(
            "library" to "jvm-retrofit2",
            "useCoroutines" to "true",
            "serializationLibrary" to "kotlinx_serialization",
            "dateLibrary" to "kotlinx-datetime",
            "enumPropertyNaming" to "UPPERCASE",
            "sourceFolder" to "src/main/kotlin",
            "omitGradleWrapper" to "true",
        ),
    )
    // Skip generation of gradle/readme/other non-Kotlin artifacts.
    globalProperties.set(
        mapOf(
            "models" to "",
            "apis" to "",
            "supportingFiles" to "false",
        ),
    )
}

// Auth endpoints are hand-written (see core/auth/AuthApi.kt) since the generator's
// output for ASP.NET's verbose operationIds is too noisy for something auth-critical.
// Broader endpoints (orders, services, etc.) will use the generator in Phase 6 —
// run `./gradlew :app:openApiGenerate` then uncomment the hook below.
// android {
//     sourceSets.getByName("main") {
//         kotlin.srcDir("${layout.buildDirectory.get()}/generated/openapi/src/main/kotlin")
//     }
// }
// tasks.named("preBuild").configure { dependsOn("openApiGenerate") }

dependencies {
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

    testImplementation(libs.junit)
}
