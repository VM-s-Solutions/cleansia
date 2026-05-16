// Top-level build file for the cleansia_android_shared monorepo.
// Plugins declared `apply false` here so subprojects can opt in via their
// own build.gradle.kts. Customer-only plugins (Spotless, Google Services)
// are still listed here so customer-app can resolve them via the catalog
// without re-declaring versions.
plugins {
    alias(libs.plugins.android.application) apply false
    alias(libs.plugins.android.library) apply false
    alias(libs.plugins.kotlin.android) apply false
    alias(libs.plugins.kotlin.compose) apply false
    alias(libs.plugins.kotlin.serialization) apply false
    alias(libs.plugins.ksp) apply false
    alias(libs.plugins.hilt) apply false
    alias(libs.plugins.spotless) apply false
    alias(libs.plugins.openapi.generator) apply false
    alias(libs.plugins.google.services) apply false
}
