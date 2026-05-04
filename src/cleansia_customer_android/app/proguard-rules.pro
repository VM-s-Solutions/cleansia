-keepattributes Signature, InnerClasses, EnclosingMethod, *Annotation*

# kotlinx.serialization
-keep,includedescriptorclasses class cz.cleansia.customer.**$$serializer { *; }
-keepclassmembers class cz.cleansia.customer.** { *** Companion; }
-keepclasseswithmembers class cz.cleansia.customer.** { kotlinx.serialization.KSerializer serializer(...); }

# Retrofit
-keep,allowobfuscation,allowshrinking interface retrofit2.Call
-keep,allowobfuscation,allowshrinking class retrofit2.Response

# Hilt
-keep class dagger.hilt.** { *; }
-keep class * extends dagger.hilt.android.HiltAndroidApp

# OkHttp
-dontwarn okhttp3.**
-dontwarn okio.**

-dontwarn java.lang.invoke.StringConcatFactory
