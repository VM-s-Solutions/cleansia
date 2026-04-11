# Cleansia Partner Android App - Kotlin Implementation Plan

## Table of Contents
1. [Executive Summary](#executive-summary)
2. [Architecture Overview](#architecture-overview)
3. [Technology Stack](#technology-stack)
4. [Project Structure](#project-structure)
5. [Core Components](#core-components)
6. [Feature Specifications](#feature-specifications)
7. [API Integration](#api-integration)
8. [Data Models](#data-models)
9. [UI/UX Design System](#uiux-design-system)
10. [Security Implementation](#security-implementation)
11. [Testing Strategy](#testing-strategy)
12. [Implementation Phases & Checklists](#implementation-phases--checklists)
13. [Deployment & CI/CD](#deployment--cicd)

---

## Executive Summary

This document outlines the complete architecture, implementation strategy, and technical specifications for rebuilding the Cleansia Partner mobile application as a native Android app using Kotlin. The app enables cleaning service employees to manage orders, view invoices, and maintain their profiles.

**Target Audience:** Cleaning service employees (partners)
**Primary Functions:** Order management, invoice viewing, profile management
**Languages:** English, Czech

---

## Architecture Overview

### Architecture Pattern: Clean Architecture + MVVM

```
┌─────────────────────────────────────────────────────────────────┐
│                      PRESENTATION LAYER                          │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐  │
│  │   Screens   │  │  ViewModels │  │   UI State (StateFlow)  │  │
│  │  (Compose)  │◄─┤   (Hilt)    │◄─┤                         │  │
│  └─────────────┘  └──────┬──────┘  └─────────────────────────┘  │
└──────────────────────────┼──────────────────────────────────────┘
                           │
┌──────────────────────────┼──────────────────────────────────────┐
│                      DOMAIN LAYER                                │
│  ┌─────────────┐  ┌──────┴──────┐  ┌─────────────────────────┐  │
│  │   Models    │  │ Repositories│  │      Use Cases          │  │
│  │  (Domain)   │  │ (Interfaces)│  │     (Optional)          │  │
│  └─────────────┘  └──────┬──────┘  └─────────────────────────┘  │
└──────────────────────────┼──────────────────────────────────────┘
                           │
┌──────────────────────────┼──────────────────────────────────────┐
│                       DATA LAYER                                 │
│  ┌─────────────┐  ┌──────┴──────┐  ┌─────────────────────────┐  │
│  │    DTOs     │  │ Repository  │  │     Data Sources        │  │
│  │             │  │   Impls     │  │  (API, Local Storage)   │  │
│  └─────────────┘  └─────────────┘  └─────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### Why Clean Architecture + MVVM?

| Aspect | Benefit |
|--------|---------|
| **Separation of Concerns** | Each layer has a single responsibility |
| **Testability** | Easy to unit test each layer independently |
| **Scalability** | New features can be added without affecting existing code |
| **Maintainability** | Clear boundaries make code easier to understand and modify |
| **Android Best Practices** | Recommended by Google for modern Android development |

### Data Flow

```
User Action → ViewModel → Repository → API/Local → Repository → ViewModel → UI Update
     │                                                                           │
     └───────────────────────────────────────────────────────────────────────────┘
                              Unidirectional Data Flow
```

---

## Technology Stack

### Core Technologies

| Category | Technology | Version | Purpose |
|----------|------------|---------|---------|
| **Language** | Kotlin | 1.9.x | Primary programming language |
| **UI Framework** | Jetpack Compose | BOM 2024.08 | Declarative UI toolkit |
| **Min SDK** | Android 8.0 | API 26 | Minimum supported version |
| **Target SDK** | Android 14 | API 35 | Target version |

### Architecture & DI

| Library | Version | Purpose |
|---------|---------|---------|
| Hilt | 2.51.1 | Dependency injection |
| Hilt Navigation Compose | 1.2.0 | ViewModel injection in Compose |
| Lifecycle ViewModel | 2.8.x | ViewModel support |
| Lifecycle Runtime Compose | 2.8.x | Lifecycle-aware Compose |

### Networking

| Library | Version | Purpose |
|---------|---------|---------|
| Retrofit | 2.11.0 | HTTP client |
| OkHttp | 4.12.0 | HTTP & WebSocket client |
| OkHttp Logging Interceptor | 4.12.0 | Network debugging |
| Gson | 2.11.0 | JSON serialization |

### Storage & Security

| Library | Version | Purpose |
|---------|---------|---------|
| EncryptedSharedPreferences | 1.1.0-alpha06 | Secure token storage |
| DataStore Preferences | 1.1.1 | User preferences |
| Security Crypto | 1.1.0-alpha06 | Encryption utilities |

### Navigation

| Library | Version | Purpose |
|---------|---------|---------|
| Navigation Compose | 2.7.7 | Screen navigation |

### UI Components

| Library | Version | Purpose |
|---------|---------|---------|
| Material3 | (Compose BOM) | Material Design 3 |
| Material Icons Extended | (Compose BOM) | Icon library |
| Coil Compose | 2.6.0 | Image loading |
| Vico | 1.15.0 | Charts and graphs |
| Accompanist Permissions | 0.34.0 | Runtime permissions |
| Core Splash Screen | 1.0.1 | Splash screen API |

### Asynchronous Programming

| Library | Version | Purpose |
|---------|---------|---------|
| Kotlin Coroutines Core | 1.8.1 | Async programming |
| Kotlin Coroutines Android | 1.8.1 | Android-specific coroutines |

### Testing

| Library | Version | Purpose |
|---------|---------|---------|
| JUnit | 4.13.2 | Unit testing |
| MockK | 1.13.11 | Mocking library |
| Coroutines Test | 1.8.1 | Testing coroutines |
| Compose UI Test | (Compose BOM) | UI testing |
| Espresso | 3.6.x | Integration testing |

### Build & Tooling

| Tool | Purpose |
|------|---------|
| Gradle KTS | Build configuration |
| ProGuard/R8 | Code obfuscation |
| BuildConfig | Environment configuration |

---

## Project Structure

```
app/
├── src/
│   ├── main/
│   │   ├── java/cz/cleansia/partner/
│   │   │   │
│   │   │   ├── CleansiaApp.kt                    # Application class
│   │   │   ├── MainActivity.kt                   # Single Activity
│   │   │   │
│   │   │   ├── data/                             # DATA LAYER
│   │   │   │   ├── api/
│   │   │   │   │   ├── ApiService.kt             # Retrofit interface
│   │   │   │   │   ├── AuthInterceptor.kt        # Token injection
│   │   │   │   │   ├── ErrorInterceptor.kt       # Error handling
│   │   │   │   │   └── dto/                      # Data Transfer Objects
│   │   │   │   │       ├── auth/
│   │   │   │   │       │   ├── LoginRequestDto.kt
│   │   │   │   │       │   ├── LoginResponseDto.kt
│   │   │   │   │       │   └── RegisterRequestDto.kt
│   │   │   │   │       ├── dashboard/
│   │   │   │   │       │   ├── DashboardStatsDto.kt
│   │   │   │   │       │   └── EarningsAnalyticsDto.kt
│   │   │   │   │       ├── orders/
│   │   │   │   │       │   ├── OrderDto.kt
│   │   │   │   │       │   └── PagedOrdersDto.kt
│   │   │   │   │       ├── invoices/
│   │   │   │   │       │   ├── InvoiceDto.kt
│   │   │   │   │       │   └── PagedInvoicesDto.kt
│   │   │   │   │       └── profile/
│   │   │   │   │           ├── EmployeeProfileDto.kt
│   │   │   │   │           └── EmployeeDocumentDto.kt
│   │   │   │   │
│   │   │   │   ├── repository/                   # Repository implementations
│   │   │   │   │   ├── AuthRepositoryImpl.kt
│   │   │   │   │   ├── DashboardRepositoryImpl.kt
│   │   │   │   │   ├── OrdersRepositoryImpl.kt
│   │   │   │   │   ├── InvoicesRepositoryImpl.kt
│   │   │   │   │   └── ProfileRepositoryImpl.kt
│   │   │   │   │
│   │   │   │   └── local/                        # Local storage
│   │   │   │       ├── SecureStorage.kt          # Encrypted prefs
│   │   │   │       └── PreferencesDataStore.kt   # User prefs
│   │   │   │
│   │   │   ├── domain/                           # DOMAIN LAYER
│   │   │   │   ├── model/                        # Domain models
│   │   │   │   │   ├── auth/
│   │   │   │   │   │   ├── AuthState.kt
│   │   │   │   │   │   └── User.kt
│   │   │   │   │   ├── dashboard/
│   │   │   │   │   │   ├── DashboardStats.kt
│   │   │   │   │   │   ├── EarningsAnalytics.kt
│   │   │   │   │   │   └── UpcomingOrder.kt
│   │   │   │   │   ├── orders/
│   │   │   │   │   │   ├── Order.kt
│   │   │   │   │   │   ├── OrderStatus.kt
│   │   │   │   │   │   └── PaymentStatus.kt
│   │   │   │   │   ├── invoices/
│   │   │   │   │   │   ├── Invoice.kt
│   │   │   │   │   │   ├── InvoiceDetail.kt
│   │   │   │   │   │   └── InvoiceStatus.kt
│   │   │   │   │   └── profile/
│   │   │   │   │       ├── EmployeeProfile.kt
│   │   │   │   │       ├── DocumentType.kt
│   │   │   │   │       └── DocumentStatus.kt
│   │   │   │   │
│   │   │   │   └── repository/                   # Repository interfaces
│   │   │   │       ├── AuthRepository.kt
│   │   │   │       ├── DashboardRepository.kt
│   │   │   │       ├── OrdersRepository.kt
│   │   │   │       ├── InvoicesRepository.kt
│   │   │   │       └── ProfileRepository.kt
│   │   │   │
│   │   │   ├── presentation/                     # PRESENTATION LAYER
│   │   │   │   ├── navigation/
│   │   │   │   │   ├── NavGraph.kt               # Navigation setup
│   │   │   │   │   └── Routes.kt                 # Route definitions
│   │   │   │   │
│   │   │   │   ├── theme/
│   │   │   │   │   ├── Theme.kt                  # Material theme
│   │   │   │   │   ├── Color.kt                  # Color palette
│   │   │   │   │   ├── Type.kt                   # Typography
│   │   │   │   │   └── Shape.kt                  # Shapes
│   │   │   │   │
│   │   │   │   ├── common/
│   │   │   │   │   └── components/               # Shared UI components
│   │   │   │   │       ├── CleansiaButton.kt
│   │   │   │   │       ├── CleansiaTextField.kt
│   │   │   │   │       ├── GlassCard.kt
│   │   │   │   │       ├── StatCard.kt
│   │   │   │   │       ├── LoadingView.kt
│   │   │   │   │       ├── ErrorView.kt
│   │   │   │   │       └── SkeletonLoader.kt
│   │   │   │   │
│   │   │   │   ├── auth/
│   │   │   │   │   ├── LoginScreen.kt
│   │   │   │   │   ├── LoginViewModel.kt
│   │   │   │   │   ├── RegisterScreen.kt
│   │   │   │   │   ├── RegisterViewModel.kt
│   │   │   │   │   ├── ConfirmEmailScreen.kt
│   │   │   │   │   ├── ConfirmEmailViewModel.kt
│   │   │   │   │   ├── ForgotPasswordScreen.kt
│   │   │   │   │   └── ForgotPasswordViewModel.kt
│   │   │   │   │
│   │   │   │   ├── dashboard/
│   │   │   │   │   ├── DashboardScreen.kt
│   │   │   │   │   ├── DashboardViewModel.kt
│   │   │   │   │   └── components/
│   │   │   │   │       ├── StatsGrid.kt
│   │   │   │   │       ├── EarningsChart.kt
│   │   │   │   │       └── UpcomingOrderCard.kt
│   │   │   │   │
│   │   │   │   ├── orders/
│   │   │   │   │   ├── OrdersScreen.kt
│   │   │   │   │   ├── OrdersViewModel.kt
│   │   │   │   │   ├── OrderDetailsScreen.kt
│   │   │   │   │   ├── OrderDetailsViewModel.kt
│   │   │   │   │   └── components/
│   │   │   │   │       ├── OrderCard.kt
│   │   │   │   │       ├── OrderTabs.kt
│   │   │   │   │       └── OrderActionButtons.kt
│   │   │   │   │
│   │   │   │   ├── invoices/
│   │   │   │   │   ├── InvoicesScreen.kt
│   │   │   │   │   ├── InvoicesViewModel.kt
│   │   │   │   │   ├── InvoiceDetailsScreen.kt
│   │   │   │   │   ├── InvoiceDetailsViewModel.kt
│   │   │   │   │   └── components/
│   │   │   │   │       ├── InvoiceCard.kt
│   │   │   │   │       └── InvoiceStatusBadge.kt
│   │   │   │   │
│   │   │   │   └── profile/
│   │   │   │       ├── ProfileScreen.kt
│   │   │   │       ├── ProfileViewModel.kt
│   │   │   │       └── components/
│   │   │   │           ├── PersonalInfoTab.kt
│   │   │   │           ├── AddressTab.kt
│   │   │   │           ├── BankDetailsTab.kt
│   │   │   │           ├── DocumentsTab.kt
│   │   │   │           └── AvailabilityTab.kt
│   │   │   │
│   │   │   └── di/                               # DEPENDENCY INJECTION
│   │   │       ├── AppModule.kt                  # App-wide dependencies
│   │   │       ├── NetworkModule.kt              # Network dependencies
│   │   │       └── RepositoryModule.kt           # Repository bindings
│   │   │
│   │   └── res/
│   │       ├── values/
│   │       │   ├── strings.xml                   # English strings
│   │       │   ├── colors.xml                    # Color resources
│   │       │   └── themes.xml                    # Theme resources
│   │       ├── values-cs/
│   │       │   └── strings.xml                   # Czech strings
│   │       └── drawable/                         # Drawables
│   │
│   ├── debug/                                    # Debug-specific
│   ├── staging/                                  # Staging-specific
│   └── release/                                  # Release-specific
│
├── build.gradle.kts                              # Module build config
└── proguard-rules.pro                            # ProGuard rules
```

---

## Core Components

### 1. Dependency Injection Modules

```kotlin
// AppModule.kt
@Module
@InstallIn(SingletonComponent::class)
object AppModule {
    @Provides
    @Singleton
    fun provideSecureStorage(@ApplicationContext context: Context): SecureStorage {
        return SecureStorage(context)
    }

    @Provides
    @Singleton
    fun providePreferencesDataStore(@ApplicationContext context: Context): PreferencesDataStore {
        return PreferencesDataStore(context)
    }
}

// NetworkModule.kt
@Module
@InstallIn(SingletonComponent::class)
object NetworkModule {
    @Provides
    @Singleton
    fun provideOkHttpClient(
        authInterceptor: AuthInterceptor,
        errorInterceptor: ErrorInterceptor
    ): OkHttpClient {
        return OkHttpClient.Builder()
            .addInterceptor(authInterceptor)
            .addInterceptor(errorInterceptor)
            .addInterceptor(HttpLoggingInterceptor().apply {
                level = if (BuildConfig.DEBUG) Level.BODY else Level.NONE
            })
            .connectTimeout(30, TimeUnit.SECONDS)
            .readTimeout(30, TimeUnit.SECONDS)
            .build()
    }

    @Provides
    @Singleton
    fun provideRetrofit(okHttpClient: OkHttpClient): Retrofit {
        return Retrofit.Builder()
            .baseUrl(BuildConfig.API_BASE_URL)
            .client(okHttpClient)
            .addConverterFactory(GsonConverterFactory.create())
            .build()
    }

    @Provides
    @Singleton
    fun provideApiService(retrofit: Retrofit): ApiService {
        return retrofit.create(ApiService::class.java)
    }
}

// RepositoryModule.kt
@Module
@InstallIn(SingletonComponent::class)
abstract class RepositoryModule {
    @Binds
    abstract fun bindAuthRepository(impl: AuthRepositoryImpl): AuthRepository

    @Binds
    abstract fun bindDashboardRepository(impl: DashboardRepositoryImpl): DashboardRepository

    @Binds
    abstract fun bindOrdersRepository(impl: OrdersRepositoryImpl): OrdersRepository

    @Binds
    abstract fun bindInvoicesRepository(impl: InvoicesRepositoryImpl): InvoicesRepository

    @Binds
    abstract fun bindProfileRepository(impl: ProfileRepositoryImpl): ProfileRepository
}
```

### 2. API Service Interface

```kotlin
interface ApiService {
    // Authentication
    @POST("Auth/Login")
    suspend fun login(@Body request: LoginRequestDto): Response<LoginResponseDto>

    @POST("Auth/RegisterEmployee")
    suspend fun register(@Body request: RegisterRequestDto): Response<Boolean>

    @POST("Auth/ConfirmUserEmail")
    suspend fun confirmEmail(@Body request: ConfirmEmailRequestDto): Response<LoginResponseDto>

    @POST("Auth/ResendConfirmationEmail")
    suspend fun resendConfirmation(@Body request: ResendConfirmationRequestDto): Response<Boolean>

    @POST("Auth/ForgotPassword")
    suspend fun forgotPassword(@Body request: ForgotPasswordRequestDto): Response<Boolean>

    @POST("Auth/ResetPassword")
    suspend fun resetPassword(@Body request: ResetPasswordRequestDto): Response<Boolean>

    // Dashboard
    @GET("Dashboard/GetStats")
    suspend fun getDashboardStats(@Query("employeeId") employeeId: String?): Response<DashboardStatsDto>

    @GET("Dashboard/GetEarningsAnalytics")
    suspend fun getEarningsAnalytics(
        @Query("employeeId") employeeId: String?,
        @Query("startDate") startDate: String?,
        @Query("endDate") endDate: String?
    ): Response<EarningsAnalyticsDto>

    @GET("Dashboard/GetUpcomingOrders")
    suspend fun getUpcomingOrders(
        @Query("id") employeeId: String?,
        @Query("limit") limit: Int = 5
    ): Response<List<UpcomingOrderDto>>

    // Orders
    @GET("Order/GetPaged")
    suspend fun getOrders(@QueryMap filters: Map<String, String>): Response<PagedOrdersResponseDto>

    @GET("Order/GetById")
    suspend fun getOrderById(@Query("OrderId") orderId: String): Response<OrderDetailDto>

    @POST("Order/TakeOrder")
    suspend fun takeOrder(@Query("OrderId") orderId: String): Response<Boolean>

    @POST("Order/StartOrder")
    suspend fun startOrder(@Query("OrderId") orderId: String): Response<Boolean>

    @POST("Order/CompleteOrder")
    suspend fun completeOrder(
        @Query("OrderId") orderId: String,
        @Body request: CompleteOrderRequestDto
    ): Response<Boolean>

    @Multipart
    @POST("Order/UploadPhoto")
    suspend fun uploadPhoto(
        @Part("orderId") orderId: RequestBody,
        @Part photo: MultipartBody.Part
    ): Response<String>

    // Invoices
    @GET("EmployeePayroll/GetPagedInvoices")
    suspend fun getInvoices(@QueryMap filters: Map<String, String>): Response<PagedInvoicesResponseDto>

    @GET("EmployeePayroll/GetInvoiceById/{invoiceId}")
    suspend fun getInvoiceById(@Path("invoiceId") invoiceId: String): Response<InvoiceDetailDto>

    @GET("EmployeePayroll/DownloadInvoice/{invoiceId}")
    @Streaming
    suspend fun downloadInvoicePdf(@Path("invoiceId") invoiceId: String): Response<ResponseBody>

    // Profile
    @GET("Employee/GetCurrentEmployee")
    suspend fun getCurrentEmployee(): Response<EmployeeProfileDto>

    @PUT("Employee/UpdateEmployee")
    suspend fun updateEmployee(@Body request: UpdateProfileRequestDto): Response<Boolean>

    @GET("Employee/GetMyDocuments")
    suspend fun getMyDocuments(): Response<List<EmployeeDocumentDto>>

    @Multipart
    @POST("Employee/SaveMyDocuments")
    suspend fun saveDocuments(
        @Part documents: List<MultipartBody.Part>,
        @Part("documentTypes") documentTypes: RequestBody
    ): Response<Boolean>

    @DELETE("Employee/DeleteMyDocument")
    suspend fun deleteDocument(@Query("DocumentId") documentId: String): Response<Boolean>

    @GET("Employee/DownloadMyDocument")
    @Streaming
    suspend fun downloadDocument(@Query("DocumentId") documentId: String): Response<ResponseBody>
}
```

### 3. Result Wrapper

```kotlin
sealed class ApiResult<out T> {
    data class Success<T>(val data: T) : ApiResult<T>()
    data class Error(val exception: ApiException) : ApiResult<Nothing>()

    inline fun <R> fold(
        onSuccess: (T) -> R,
        onError: (ApiException) -> R
    ): R = when (this) {
        is Success -> onSuccess(data)
        is Error -> onError(exception)
    }

    fun getOrNull(): T? = (this as? Success)?.data
    fun exceptionOrNull(): ApiException? = (this as? Error)?.exception
}

data class ApiException(
    val message: String,
    val code: String? = null,
    val statusCode: Int? = null,
    val validationErrors: List<String>? = null
)
```

### 4. Secure Storage

```kotlin
@Singleton
class SecureStorage @Inject constructor(
    @ApplicationContext private val context: Context
) {
    private val masterKey = MasterKey.Builder(context)
        .setKeyScheme(MasterKey.KeyScheme.AES256_GCM)
        .build()

    private val sharedPreferences = EncryptedSharedPreferences.create(
        context,
        "cleansia_secure_prefs",
        masterKey,
        EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
        EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
    )

    companion object {
        private const val KEY_TOKEN = "auth_token"
        private const val KEY_USER_EMAIL = "user_email"
    }

    suspend fun saveToken(token: String) = withContext(Dispatchers.IO) {
        sharedPreferences.edit().putString(KEY_TOKEN, token).apply()
    }

    suspend fun getToken(): String? = withContext(Dispatchers.IO) {
        sharedPreferences.getString(KEY_TOKEN, null)
    }

    suspend fun clearAll() = withContext(Dispatchers.IO) {
        sharedPreferences.edit().clear().apply()
    }

    suspend fun hasToken(): Boolean = getToken() != null
}
```

---

## Feature Specifications

### Feature Matrix

| Feature | Screen | Priority | Complexity |
|---------|--------|----------|------------|
| Login | LoginScreen | P0 | Medium |
| Registration | RegisterScreen | P0 | Medium |
| Email Confirmation | ConfirmEmailScreen | P0 | Low |
| Forgot Password | ForgotPasswordScreen | P1 | Medium |
| Dashboard Stats | DashboardScreen | P0 | Medium |
| Earnings Chart | DashboardScreen | P1 | High |
| Upcoming Orders | DashboardScreen | P1 | Low |
| Orders List | OrdersScreen | P0 | High |
| Order Details | OrderDetailsScreen | P0 | High |
| Take/Start/Complete Order | OrderDetailsScreen | P0 | Medium |
| Photo Upload | OrderDetailsScreen | P1 | Medium |
| Invoices List | InvoicesScreen | P0 | Medium |
| Invoice Details | InvoiceDetailsScreen | P0 | Medium |
| PDF Download | InvoiceDetailsScreen | P1 | Medium |
| Profile View/Edit | ProfileScreen | P0 | High |
| Document Upload | ProfileScreen | P1 | High |
| Availability Schedule | ProfileScreen | P2 | Medium |

### Screen Flow Diagram

```
┌─────────────┐
│   Splash    │
└──────┬──────┘
       │
       ▼
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Login     │────►│  Register   │────►│   Confirm   │
└──────┬──────┘     └─────────────┘     │    Email    │
       │                                └──────┬──────┘
       │◄──────────────────────────────────────┘
       │
       ▼
┌─────────────────────────────────────────────────────┐
│                    Main Tab Bar                      │
├─────────────┬─────────────┬─────────────┬───────────┤
│  Dashboard  │   Orders    │  Invoices   │  Profile  │
└──────┬──────┴──────┬──────┴──────┬──────┴─────┬─────┘
       │             │             │            │
       │             ▼             ▼            │
       │      ┌───────────┐ ┌───────────┐      │
       │      │  Order    │ │  Invoice  │      │
       │      │  Details  │ │  Details  │      │
       │      └───────────┘ └───────────┘      │
       │                                        │
       └────────────────────────────────────────┘
              Navigate to Order Details
```

---

## API Integration

### API Endpoints Summary

| Category | Endpoint | Method | Auth Required |
|----------|----------|--------|---------------|
| **Auth** | /Auth/Login | POST | No |
| | /Auth/RegisterEmployee | POST | No |
| | /Auth/ConfirmUserEmail | POST | No |
| | /Auth/ResendConfirmationEmail | POST | No |
| | /Auth/ForgotPassword | POST | No |
| | /Auth/ResetPassword | POST | No |
| **Dashboard** | /Dashboard/GetStats | GET | Yes |
| | /Dashboard/GetEarningsAnalytics | GET | Yes |
| | /Dashboard/GetUpcomingOrders | GET | Yes |
| **Orders** | /Order/GetPaged | GET | Yes |
| | /Order/GetById | GET | Yes |
| | /Order/TakeOrder | POST | Yes |
| | /Order/StartOrder | POST | Yes |
| | /Order/CompleteOrder | POST | Yes |
| | /Order/UploadPhoto | POST | Yes |
| **Invoices** | /EmployeePayroll/GetPagedInvoices | GET | Yes |
| | /EmployeePayroll/GetInvoiceById/{id} | GET | Yes |
| | /EmployeePayroll/DownloadInvoice/{id} | GET | Yes |
| **Profile** | /Employee/GetCurrentEmployee | GET | Yes |
| | /Employee/UpdateEmployee | PUT | Yes |
| | /Employee/GetMyDocuments | GET | Yes |
| | /Employee/SaveMyDocuments | POST | Yes |
| | /Employee/DeleteMyDocument | DELETE | Yes |
| | /Employee/DownloadMyDocument | GET | Yes |

### Environment Configuration

| Environment | Base URL |
|-------------|----------|
| Development | `http://10.0.2.2:5000/api` |
| Staging | `https://staging-api.cleansia.cz/api` |
| Production | `https://api.cleansia.cz/api` |

---

## Data Models

### Enums

```kotlin
enum class OrderStatus(val value: Int) {
    PENDING(0),
    CONFIRMED(1),
    IN_PROGRESS(2),
    COMPLETED(3),
    CANCELLED(4)
}

enum class PaymentStatus(val value: Int) {
    PENDING(0),
    PAID(1),
    FAILED(2),
    REFUNDED(3)
}

enum class InvoiceStatus(val value: Int) {
    PENDING(1),
    APPROVED(2),
    PAID(3),
    DISPUTED(4),
    REJECTED(5),
    CANCELLED(6)
}

enum class DocumentType(val value: Int, val displayName: String) {
    ID_DOCUMENT(1, "ID Document"),
    PROOF_OF_ADDRESS(2, "Proof of Address"),
    TAX_CERTIFICATE(3, "Tax Certificate"),
    INSURANCE(4, "Insurance"),
    BACKGROUND_CHECK(5, "Background Check"),
    BANK_CONFIRMATION(6, "Bank Confirmation"),
    WORK_PERMIT(7, "Work Permit"),
    DRIVING_LICENSE(8, "Driving License"),
    HEALTH_CERTIFICATE(9, "Health Certificate"),
    OTHER(10, "Other")
}

enum class DocumentStatus(val value: Int) {
    PENDING(1),
    APPROVED(2),
    REJECTED(3)
}
```

### Domain Models

See `domain/model/` directory in project structure for complete model definitions.

---

## UI/UX Design System

### Color Palette

```kotlin
object CleansiaColors {
    // Primary - Cyan/Sky Blue
    val Primary = Color(0xFF0284C7)
    val PrimaryLight = Color(0xFF38BDF8)
    val PrimaryDark = Color(0xFF0369A1)
    val PrimaryContainer = Color(0xFFE0F2FE)

    // Secondary - Slate
    val Secondary = Color(0xFF64748B)
    val SecondaryLight = Color(0xFF94A3B8)
    val SecondaryDark = Color(0xFF475569)

    // Background
    val Background = Color(0xFFF8FAFC)
    val Surface = Color(0xFFFFFFFF)
    val SurfaceVariant = Color(0xFFF1F5F9)

    // Status
    val Success = Color(0xFF22C55E)
    val Warning = Color(0xFFF59E0B)
    val Error = Color(0xFFEF4444)
    val Info = Color(0xFF3B82F6)

    // Text
    val OnPrimary = Color(0xFFFFFFFF)
    val OnBackground = Color(0xFF0F172A)
    val OnBackgroundSecondary = Color(0xFF64748B)
}
```

### Typography

```kotlin
val CleansiaTypography = Typography(
    displayLarge = TextStyle(fontWeight = FontWeight.Bold, fontSize = 32.sp),
    displayMedium = TextStyle(fontWeight = FontWeight.Bold, fontSize = 28.sp),
    displaySmall = TextStyle(fontWeight = FontWeight.Bold, fontSize = 24.sp),
    headlineLarge = TextStyle(fontWeight = FontWeight.SemiBold, fontSize = 22.sp),
    headlineMedium = TextStyle(fontWeight = FontWeight.SemiBold, fontSize = 20.sp),
    headlineSmall = TextStyle(fontWeight = FontWeight.SemiBold, fontSize = 18.sp),
    titleLarge = TextStyle(fontWeight = FontWeight.Medium, fontSize = 16.sp),
    titleMedium = TextStyle(fontWeight = FontWeight.Medium, fontSize = 14.sp),
    titleSmall = TextStyle(fontWeight = FontWeight.Medium, fontSize = 12.sp),
    bodyLarge = TextStyle(fontSize = 16.sp),
    bodyMedium = TextStyle(fontSize = 14.sp),
    bodySmall = TextStyle(fontSize = 12.sp),
    labelLarge = TextStyle(fontWeight = FontWeight.Medium, fontSize: 14.sp),
    labelMedium = TextStyle(fontWeight = FontWeight.Medium, fontSize = 12.sp),
    labelSmall = TextStyle(fontWeight = FontWeight.Medium, fontSize = 10.sp)
)
```

---

## Security Implementation

### Security Checklist

- [ ] JWT tokens stored in EncryptedSharedPreferences
- [ ] Certificate pinning for API calls (production)
- [ ] ProGuard/R8 code obfuscation
- [ ] No sensitive data in logs (release builds)
- [ ] Biometric authentication option (optional)
- [ ] Session timeout handling
- [ ] Secure WebView configuration (if used)

### Token Flow

```
Login Success → Save Token (Encrypted) → Auth Interceptor adds to headers → 401 Response → Clear Token → Redirect to Login
```

---

## Testing Strategy

### Test Coverage Goals

| Layer | Target Coverage |
|-------|-----------------|
| ViewModels | 80% |
| Repositories | 90% |
| Use Cases | 95% |
| UI Components | 60% |

### Test Types

1. **Unit Tests**
   - ViewModels with MockK
   - Repositories with fake data sources
   - Utility functions

2. **Integration Tests**
   - API integration with mock server
   - Database operations

3. **UI Tests**
   - Compose UI tests
   - Navigation tests
   - Form validation tests

---

## Implementation Phases & Checklists

### Phase 1: Project Foundation (Week 1)

#### Setup
- [ ] Create Android Studio project with Kotlin
- [ ] Configure Gradle with all dependencies
- [ ] Set up build variants (dev/staging/prod)
- [ ] Configure BuildConfig for API URLs
- [ ] Set up ProGuard rules

#### Core Infrastructure
- [ ] Create project package structure
- [ ] Implement SecureStorage class
- [ ] Implement PreferencesDataStore class
- [ ] Set up Hilt modules (App, Network, Repository)
- [ ] Create ApiService interface (all endpoints)
- [ ] Implement AuthInterceptor
- [ ] Implement ErrorInterceptor
- [ ] Create ApiResult wrapper class
- [ ] Create ApiException class

#### Theme & Design System
- [ ] Define color palette (CleansiaColors)
- [ ] Define typography (CleansiaTypography)
- [ ] Create Material3 theme
- [ ] Create common UI components:
  - [ ] CleansiaButton
  - [ ] CleansiaTextField
  - [ ] GlassCard
  - [ ] StatCard
  - [ ] LoadingView
  - [ ] ErrorView
  - [ ] SkeletonLoader

#### Navigation
- [ ] Define Routes sealed class
- [ ] Create NavGraph composable
- [ ] Set up bottom navigation items
- [ ] Implement MainScaffold with bottom nav

---

### Phase 2: Authentication (Week 2)

#### Data Layer
- [ ] Create auth DTOs (LoginRequest, LoginResponse, RegisterRequest, etc.)
- [ ] Create auth domain models (User, AuthState)
- [ ] Define AuthRepository interface
- [ ] Implement AuthRepositoryImpl

#### Login Feature
- [ ] Create LoginViewModel
  - [ ] Login action
  - [ ] Validation logic
  - [ ] Error handling
  - [ ] Loading state
- [ ] Create LoginScreen
  - [ ] Email input field
  - [ ] Password input field
  - [ ] Remember me checkbox
  - [ ] Login button
  - [ ] Forgot password link
  - [ ] Register link
  - [ ] Glassmorphism design
  - [ ] Loading state UI
  - [ ] Error display

#### Registration Feature
- [ ] Create RegisterViewModel
  - [ ] Registration action
  - [ ] Form validation
  - [ ] Error handling
- [ ] Create RegisterScreen
  - [ ] Email input
  - [ ] Password input
  - [ ] First name input
  - [ ] Last name input
  - [ ] Language selection
  - [ ] Register button
  - [ ] Login link

#### Email Confirmation Feature
- [ ] Create ConfirmEmailViewModel
  - [ ] Confirm action
  - [ ] Resend code action
  - [ ] Countdown timer
- [ ] Create ConfirmEmailScreen
  - [ ] Code input field
  - [ ] Verify button
  - [ ] Resend link
  - [ ] Timer display

#### Forgot Password Feature
- [ ] Create ForgotPasswordViewModel
  - [ ] Request reset action
  - [ ] Reset password action
  - [ ] Two-step flow state
- [ ] Create ForgotPasswordScreen
  - [ ] Email input (step 1)
  - [ ] Code + new password input (step 2)
  - [ ] Submit buttons

#### Auth Navigation
- [ ] Implement auth state observation
- [ ] Auto-redirect on login success
- [ ] Auto-redirect on logout
- [ ] Handle email confirmation required state

---

### Phase 3: Dashboard (Week 2-3)

#### Data Layer
- [ ] Create dashboard DTOs
- [ ] Create dashboard domain models
- [ ] Define DashboardRepository interface
- [ ] Implement DashboardRepositoryImpl

#### Dashboard Feature
- [ ] Create DashboardViewModel
  - [ ] Load all dashboard data (parallel)
  - [ ] Refresh action
  - [ ] Error handling
  - [ ] Loading states (initial, refreshing)
- [ ] Create DashboardScreen
  - [ ] Greeting message (time-aware)
  - [ ] Stats grid (2x2)
    - [ ] Available orders stat
    - [ ] My active orders stat
    - [ ] Completed this month stat
    - [ ] Pending earnings stat
  - [ ] Earnings chart
  - [ ] Upcoming orders section
  - [ ] Pull-to-refresh
  - [ ] Loading skeleton
  - [ ] Error state

#### Dashboard Components
- [ ] Create StatsGrid composable
- [ ] Create EarningsChart composable (using Vico)
- [ ] Create UpcomingOrderCard composable
- [ ] Implement skeleton loading animation

#### Dashboard Navigation
- [ ] Navigate to order details from upcoming orders
- [ ] Navigate to orders list from "See all"

---

### Phase 4: Orders (Week 3-4)

#### Data Layer
- [ ] Create orders DTOs
- [ ] Create orders domain models
- [ ] Define OrdersRepository interface
- [ ] Implement OrdersRepositoryImpl

#### Orders List Feature
- [ ] Create OrdersViewModel
  - [ ] Load orders with filters
  - [ ] Pagination (load more)
  - [ ] Tab switching (available/my orders)
  - [ ] Refresh action
  - [ ] Filter management
- [ ] Create OrdersScreen
  - [ ] Tab bar (Available / My Orders)
  - [ ] Orders list
  - [ ] Empty state
  - [ ] Loading state
  - [ ] Load more indicator
  - [ ] Pull-to-refresh
  - [ ] Search/filter UI (optional)

#### Orders List Components
- [ ] Create OrderCard composable
  - [ ] Order number
  - [ ] Customer name
  - [ ] Address
  - [ ] Date/time
  - [ ] Status badges
  - [ ] Price
  - [ ] Available spots
- [ ] Create OrderTabs composable
- [ ] Create order filter bottom sheet (optional)

#### Order Details Feature
- [ ] Create OrderDetailsViewModel
  - [ ] Load order details
  - [ ] Take order action
  - [ ] Start order action
  - [ ] Complete order action
  - [ ] Photo upload action
  - [ ] Action loading states
- [ ] Create OrderDetailsScreen
  - [ ] Order header (number, status)
  - [ ] Schedule section (date, time, duration)
  - [ ] Customer section (name, phone, email)
  - [ ] Location section (address, map link)
  - [ ] Property section (rooms, bathrooms)
  - [ ] Services section (packages, services)
  - [ ] Payment section (status, total)
  - [ ] Action buttons (contextual)
  - [ ] Completion dialog (notes, photos)

#### Order Details Components
- [ ] Create OrderActionButtons composable
- [ ] Create OrderSection composable
- [ ] Create CompletionDialog composable
- [ ] Implement photo picker integration

---

### Phase 5: Invoices (Week 4)

#### Data Layer
- [ ] Create invoices DTOs
- [ ] Create invoices domain models
- [ ] Define InvoicesRepository interface
- [ ] Implement InvoicesRepositoryImpl

#### Invoices List Feature
- [ ] Create InvoicesViewModel
  - [ ] Load invoices with filters
  - [ ] Pagination
  - [ ] Refresh action
  - [ ] Filter management
- [ ] Create InvoicesScreen
  - [ ] Invoices list
  - [ ] Empty state
  - [ ] Loading state
  - [ ] Pull-to-refresh
  - [ ] Filter options

#### Invoices List Components
- [ ] Create InvoiceCard composable
  - [ ] Invoice number
  - [ ] Pay period
  - [ ] Total amount
  - [ ] Status badge
  - [ ] Generated date
- [ ] Create InvoiceStatusBadge composable
- [ ] Create filter chips

#### Invoice Details Feature
- [ ] Create InvoiceDetailsViewModel
  - [ ] Load invoice details
  - [ ] Download PDF action
  - [ ] Download loading state
- [ ] Create InvoiceDetailsScreen
  - [ ] Status card (status, amount)
  - [ ] Invoice info section
  - [ ] Financial summary section
  - [ ] Status timeline
  - [ ] Order payments list
  - [ ] Admin notes (if any)
  - [ ] Download button

#### Invoice Details Components
- [ ] Create InvoiceStatusCard composable
- [ ] Create FinancialSummary composable
- [ ] Create StatusTimeline composable
- [ ] Create OrderPayItem composable
- [ ] Implement PDF download and open

---

### Phase 6: Profile (Week 5)

#### Data Layer
- [ ] Create profile DTOs
- [ ] Create profile domain models
- [ ] Define ProfileRepository interface
- [ ] Implement ProfileRepositoryImpl

#### Profile Feature
- [ ] Create ProfileViewModel
  - [ ] Load profile data
  - [ ] Load documents
  - [ ] Update profile action
  - [ ] Upload document action
  - [ ] Delete document action
  - [ ] Logout action
  - [ ] Various loading states
- [ ] Create ProfileScreen
  - [ ] Tab bar (Personal, Address, Bank, Documents, Settings)
  - [ ] Tab content switching
  - [ ] Pull-to-refresh
  - [ ] Loading state

#### Profile Tabs
- [ ] Create PersonalInfoTab
  - [ ] First name (editable)
  - [ ] Last name (editable)
  - [ ] Email (read-only)
  - [ ] Phone (editable)
  - [ ] Birth date (editable)
  - [ ] Save button

- [ ] Create AddressTab
  - [ ] Street (editable)
  - [ ] City (editable)
  - [ ] Zip code (editable)
  - [ ] Country (dropdown)
  - [ ] Nationality (dropdown)
  - [ ] Save button

- [ ] Create BankDetailsTab
  - [ ] IBAN (editable, formatted)
  - [ ] Save button

- [ ] Create DocumentsTab
  - [ ] Document type selector
  - [ ] File picker
  - [ ] Staged documents list
  - [ ] Upload button
  - [ ] Existing documents list (grouped by status)
  - [ ] Document actions (view, delete)

- [ ] Create AvailabilityTab (optional P2)
  - [ ] Day selector
  - [ ] Time range inputs
  - [ ] Save button

- [ ] Create Settings section
  - [ ] Language selector
  - [ ] Logout button
  - [ ] App version

---

### Phase 7: Polish & Testing (Week 6)

#### Localization
- [ ] Complete English strings (strings.xml)
- [ ] Complete Czech strings (strings.xml in values-cs)
- [ ] Test all strings display correctly
- [ ] Handle plurals where needed

#### Deep Linking
- [ ] Configure AndroidManifest for deep links
- [ ] Handle incoming deep links in MainActivity
- [ ] Navigate to correct screen from deep link
- [ ] Test deep link: cleansia://orders/{orderId}
- [ ] Test deep link: cleansia://invoices/{invoiceId}

#### Error Handling
- [ ] Implement global error handler
- [ ] Create error translation service
- [ ] Test all error scenarios
- [ ] Implement retry mechanisms
- [ ] Add offline detection

#### Performance
- [ ] Profile app with Android Studio Profiler
- [ ] Optimize image loading
- [ ] Implement list item recycling properly
- [ ] Add pagination to all lists
- [ ] Optimize recomposition in Compose

#### UI/UX Polish
- [ ] Add animations and transitions
- [ ] Implement haptic feedback
- [ ] Add pull-to-refresh animations
- [ ] Polish empty states
- [ ] Add loading skeletons everywhere
- [ ] Test on different screen sizes
- [ ] Test on different Android versions

#### Testing
- [ ] Write unit tests for all ViewModels
- [ ] Write unit tests for repositories
- [ ] Write UI tests for critical flows:
  - [ ] Login flow
  - [ ] Order take flow
  - [ ] Invoice download flow
- [ ] Write integration tests for API
- [ ] Achieve target test coverage

#### Pre-release
- [ ] Test release build
- [ ] Test ProGuard obfuscation
- [ ] Verify no sensitive logs in release
- [ ] Test on physical devices
- [ ] Create app signing keys
- [ ] Prepare store listing assets

---

## Deployment & CI/CD

### Build Commands

```bash
# Debug build
./gradlew assembleDevelopmentDebug

# Staging build
./gradlew assembleStagingRelease

# Production build
./gradlew assembleProductionRelease

# Run tests
./gradlew test

# Run lint
./gradlew lint
```

### CI/CD Pipeline (GitHub Actions example)

```yaml
name: Android CI

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
    - name: Set up JDK 17
      uses: actions/setup-java@v4
      with:
        java-version: '17'
        distribution: 'temurin'
    - name: Build with Gradle
      run: ./gradlew build
    - name: Run tests
      run: ./gradlew test
    - name: Upload APK
      uses: actions/upload-artifact@v4
      with:
        name: app-debug
        path: app/build/outputs/apk/development/debug/
```

### Play Store Checklist

- [ ] App icons (all sizes)
- [ ] Feature graphic (1024x500)
- [ ] Screenshots (phone, tablet)
- [ ] App description (both languages)
- [ ] Privacy policy URL
- [ ] Content rating questionnaire
- [ ] App signing by Google Play
- [ ] Internal testing track setup
- [ ] Closed testing track setup
- [ ] Production release

---

## Progress Tracking

### Overall Progress

| Phase | Status | Progress |
|-------|--------|----------|
| Phase 1: Foundation | Not Started | 0% |
| Phase 2: Authentication | Not Started | 0% |
| Phase 3: Dashboard | Not Started | 0% |
| Phase 4: Orders | Not Started | 0% |
| Phase 5: Invoices | Not Started | 0% |
| Phase 6: Profile | Not Started | 0% |
| Phase 7: Polish | Not Started | 0% |

### Current Sprint

**Sprint:** N/A
**Goal:** N/A
**Status:** Not Started

---

*Last Updated: [Date]*
*Document Version: 1.0*
