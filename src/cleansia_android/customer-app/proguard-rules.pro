# Generic reflection metadata used by serialization, Retrofit type-erasure
# resolution, and Compose-Nav typed-route reflection.
-keepattributes Signature, InnerClasses, EnclosingMethod, *Annotation*
-keepattributes RuntimeVisibleAnnotations, RuntimeVisibleParameterAnnotations
-keepattributes RuntimeVisibleTypeAnnotations, AnnotationDefault

# Crash readability — without these, Sentry stack traces are obfuscated noise.
# `-renamesourcefileattribute SourceFile` collapses leaked source paths, then
# the R8 mapping file deobfuscates everything during Sentry symbolication.
-keepattributes SourceFile, LineNumberTable
-renamesourcefileattribute SourceFile

# ── kotlinx.serialization ──
# Generated $$serializer companions for every @Serializable class in the app.
-keep,includedescriptorclasses class cz.cleansia.customer.**$$serializer { *; }
-keepclassmembers class cz.cleansia.customer.** { *** Companion; }
-keepclasseswithmembers class cz.cleansia.customer.** { kotlinx.serialization.KSerializer serializer(...); }
# Compose-Nav 2.8 typed routes also reflect on these in the navigation package.
-keep,includedescriptorclasses class cz.cleansia.customer.navigation.**$$serializer { *; }
-keepclassmembers class cz.cleansia.customer.navigation.** { *** Companion; }

# ── kotlinx-datetime ──
# Serializers live in kotlinx.datetime.serializers; without these R8 strips
# them and `Json.encodeToString(LocalDate)` blows up at runtime.
-keep class kotlinx.datetime.** { *; }
-keep class kotlinx.datetime.serializers.** { *; }

# ── Retrofit ──
# Service interface methods rely on Signature + parameter annotations to
# resolve generic types and @Body/@Path/@Query metadata at runtime.
-keep,allowobfuscation,allowshrinking interface retrofit2.Call
-keep,allowobfuscation,allowshrinking class retrofit2.Response
-keepclasseswithmembers class * {
    @retrofit2.http.* <methods>;
}

# ── Hilt ──
-keep class dagger.hilt.** { *; }
-keep class * extends dagger.hilt.android.HiltAndroidApp
-keep class * extends androidx.lifecycle.ViewModel { <init>(...); }

# ── OkHttp ──
-dontwarn okhttp3.**
-dontwarn okio.**

# ── Sentry ──
# Sentry's own consumer rules cover most of this, but the okhttp interceptor
# extension trips a warning without an explicit dontwarn.
-dontwarn io.sentry.android.okhttp.**

# ── Misc ──
-dontwarn java.lang.invoke.StringConcatFactory
