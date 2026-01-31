import org.openapitools.generator.gradle.plugin.tasks.GenerateTask
import java.net.HttpURLConnection
import java.net.URI

plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.hilt.android)
    alias(libs.plugins.ksp)
    alias(libs.plugins.kotlin.serialization)
    alias(libs.plugins.openapi.generator)
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
    }

    buildTypes {
        debug {
            isMinifyEnabled = false
            applicationIdSuffix = ".debug"
            versionNameSuffix = "-debug"
            buildConfigField("String", "API_BASE_URL", "\"http://10.0.2.2:5000/api\"")
            manifestPlaceholders["APP_NAME"] = "Cleansia Dev"
        }
        create("staging") {
            isMinifyEnabled = true
            applicationIdSuffix = ".staging"
            versionNameSuffix = "-staging"
            buildConfigField("String", "API_BASE_URL", "\"https://staging-api.cleansia.cz/api\"")
            manifestPlaceholders["APP_NAME"] = "Cleansia Staging"
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
            signingConfig = signingConfigs.getByName("debug")
        }
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            buildConfigField("String", "API_BASE_URL", "\"https://api.cleansia.cz/api\"")
            manifestPlaceholders["APP_NAME"] = "Cleansia Partner"
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
        }
    }

    flavorDimensions += "environment"
    productFlavors {
        create("prod") {
            dimension = "environment"
        }
        create("mock") {
            dimension = "environment"
            applicationIdSuffix = ".mock"
            manifestPlaceholders["APP_NAME"] = "Cleansia Mock"
        }
    }

    // Only allow mock flavor with debug build type
    androidComponents {
        beforeVariants { variantBuilder ->
            if (variantBuilder.productFlavors.any { it.second == "mock" } &&
                variantBuilder.buildType != "debug") {
                variantBuilder.enable = false
            }
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
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

// =============================================================================
// OpenAPI Generator Configuration
// =============================================================================

// Configuration for the API spec location
val openApiSpecUrl = "http://localhost:5000/swagger/v1/swagger.json"
val openApiSpecFilePath = "$projectDir/api-spec/swagger.json"

// Task to download the OpenAPI spec from the running backend
abstract class DownloadApiSpecTask : DefaultTask() {
    @get:Input
    abstract val specUrl: Property<String>

    @get:OutputFile
    abstract val outputFile: RegularFileProperty

    @TaskAction
    fun download() {
        val specDir = outputFile.get().asFile.parentFile
        if (!specDir.exists()) {
            specDir.mkdirs()
        }

        try {
            val url = URI(specUrl.get()).toURL()
            val connection = url.openConnection() as HttpURLConnection
            connection.requestMethod = "GET"
            connection.connectTimeout = 5000
            connection.readTimeout = 5000

            if (connection.responseCode == 200) {
                val content = connection.inputStream.bufferedReader().readText()
                outputFile.get().asFile.writeText(content)
                println("Successfully downloaded OpenAPI spec to: ${outputFile.get().asFile.absolutePath}")
            } else {
                throw GradleException("Failed to download OpenAPI spec. HTTP ${connection.responseCode}")
            }
        } catch (e: Exception) {
            throw GradleException("Failed to download OpenAPI spec from ${specUrl.get()}. Make sure the backend is running.\nError: ${e.message}")
        }
    }
}

tasks.register<DownloadApiSpecTask>("downloadApiSpec") {
    group = "openapi"
    description = "Downloads the OpenAPI specification from the backend server"
    specUrl.set(openApiSpecUrl)
    outputFile.set(file(openApiSpecFilePath))
}

// Main task to generate the API client
tasks.register<GenerateTask>("generateApiClient") {
    group = "openapi"
    description = "Generates Kotlin API client from OpenAPI specification"

    // Use local spec file (download first with downloadApiSpec task)
    inputSpec.set(openApiSpecFilePath)

    // Output configuration
    outputDir.set("${layout.buildDirectory.get()}/generated/openapi")

    // Generator configuration
    generatorName.set("kotlin")

    // Skip validation - .NET generic types create schema names with special characters
    validateSpec.set(false)

    // Package names
    apiPackage.set("cz.cleansia.partner.api.generated.api")
    modelPackage.set("cz.cleansia.partner.api.generated.models")
    packageName.set("cz.cleansia.partner.api.generated")

    // Additional properties for Retrofit + kotlinx.serialization
    configOptions.set(mapOf(
        "library" to "jvm-retrofit2",
        "serializationLibrary" to "kotlinx_serialization",
        "useCoroutines" to "true",
        "enumPropertyNaming" to "original",  // Use original enum names from backend
        "dateLibrary" to "string",
        "collectionType" to "list",
        "generateApiTests" to "false",
        "generateModelTests" to "false",
        "generateApiDocumentation" to "false",
        "generateModelDocumentation" to "false",
        "sortParamsByRequiredFlag" to "true",
        "sortModelPropertiesByRequiredFlag" to "true",
        "sourceFolder" to "src/main/kotlin"  // Standard source folder
    ))

    // Type mappings to ensure proper Kotlin types
    typeMappings.set(mapOf(
        "DateTime" to "String",
        "Date" to "String",
        "date" to "String",
        "date-time" to "String",
        "UUID" to "String",
        "integer" to "kotlin.Int",
        "number" to "kotlin.Double",
        "int" to "kotlin.Int",
        "long" to "kotlin.Long",
        "float" to "kotlin.Float",
        "double" to "kotlin.Double",
        "boolean" to "kotlin.Boolean",
        "string" to "kotlin.String"
    ))

    // Import mappings for proper type resolution
    importMappings.set(mapOf(
        "Int" to "kotlin.Int",
        "Long" to "kotlin.Long",
        "Float" to "kotlin.Float",
        "Double" to "kotlin.Double",
        "Boolean" to "kotlin.Boolean",
        "String" to "kotlin.String"
    ))

    // Skip generating unnecessary files
    generateApiTests.set(false)
    generateModelTests.set(false)
    generateApiDocumentation.set(false)
    generateModelDocumentation.set(false)
}

// Convenience task to download spec and generate client in one step
tasks.register("updateApiClient") {
    group = "openapi"
    description = "Downloads the latest OpenAPI spec and regenerates the API client"
    dependsOn("downloadApiSpec", "generateApiClient")
    tasks.findByName("generateApiClient")?.mustRunAfter("downloadApiSpec")
}

// NOTE: To automatically generate API client before build, uncomment the following:
// tasks.named("preBuild") {
//     dependsOn("generateApiClient")
// }
// Make sure to run `./gradlew downloadApiSpec` first to download the swagger.json

dependencies {
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

    // Image Loading
    implementation(libs.coil.compose)

    // DataStore
    implementation(libs.androidx.datastore.preferences)

    // AppCompat (for locale changes)
    implementation(libs.androidx.appcompat)

    // Security
    implementation(libs.androidx.security.crypto)
    implementation(libs.androidx.biometric)

    // Room Database
    implementation(libs.androidx.room.runtime)
    implementation(libs.androidx.room.ktx)
    ksp(libs.androidx.room.compiler)

    // Splash Screen
    implementation(libs.androidx.splashscreen)

    // Lottie Animation
    implementation(libs.lottie.compose)

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
