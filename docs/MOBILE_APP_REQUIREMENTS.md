# Cleansia Partner Mobile App - Requirements & Implementation Guide

## Table of Contents
1. [Implementation Status](#implementation-status)
2. [Executive Summary](#executive-summary)
3. [Key Questions Answered](#key-questions-answered)
4. [Technology Comparison](#technology-comparison)
5. [Recommended Stack: Flutter](#recommended-stack-flutter)
6. [Development Environment Setup](#development-environment-setup)
7. [Project Structure](#project-structure)
8. [Feature Requirements](#feature-requirements)
9. [API Integration](#api-integration)
10. [State Management](#state-management)
11. [UI/UX Guidelines](#uiux-guidelines)
12. [Testing Strategy](#testing-strategy)
13. [Deployment](#deployment)
14. [Estimated Timeline](#estimated-timeline)

---

## Implementation Status

> **Last Updated:** January 2025

### Overall Progress: Phase 1-7 Complete (~90%)

| Phase | Status | Details |
|-------|--------|---------|
| **Phase 1: Setup** | ✅ Complete | Project created, architecture set up, CI configured |
| **Phase 2: Auth** | ✅ Complete | Login, Register, Email Confirmation, Forgot Password UI |
| **Phase 3: Dashboard** | ✅ Complete | Stats cards, charts, analytics, pull-to-refresh |
| **Phase 4: Orders** | ✅ Complete | Orders list, tabs, filters, details, take/start/complete flows |
| **Phase 5: Invoices** | ✅ Complete | Invoices list, details, PDF download, filter, status timeline |
| **Phase 6: Profile** | ✅ Complete | Personal info, bank details, documents with upload/download |
| **Phase 7: Polish** | ✅ Complete | Error handling widgets, loading skeletons, settings tab, logout |
| **Phase 8: Deployment** | ⏳ Not Started | |

### What's Been Implemented

#### ✅ Project Setup & Architecture
- [x] Flutter project created with `very_good_cli` (best practices scaffolding)
- [x] Clean Architecture + BLoC pattern implemented
- [x] Folder structure: `lib/core/`, `lib/features/`, `lib/shared/`
- [x] Multi-environment support (development, staging, production)
- [x] Root `.gitignore` updated for Flutter/Dart files
- [x] CI workflow created (`.github/workflows/mobile-ci.yml`)

#### ✅ Core Infrastructure
- [x] **API Client** (`lib/core/api/api_client.dart`)
  - Dio-based HTTP client with interceptors
  - Auth interceptor (adds Bearer token)
  - Error interceptor (handles API exceptions)
  - Logging interceptor (debug mode)
  - Environment-based base URL configuration
- [x] **Secure Storage** (`lib/core/services/secure_storage_service.dart`)
  - JWT token storage
  - User email storage
  - Login state checking
- [x] **API Constants** (`lib/core/constants/api_constants.dart`)
  - All API endpoints defined
  - Environment URLs configured
- [x] **API Result Pattern** (`lib/core/api/api_result.dart`)
  - Sealed class for Success/Failure handling

#### ✅ Authentication Feature
- [x] **AuthBloc** (`lib/features/auth/bloc/`)
  - Events: Login, Register, ConfirmEmail, ForgotPassword, Logout, etc.
  - States: initial, unauthenticated, authenticated, emailConfirmationRequired
  - Full event handling with error management
- [x] **AuthRepository** (`lib/core/services/auth_repository.dart`)
  - Login with token storage
  - Register employee
  - Confirm email
  - Resend confirmation
  - Forgot password
  - Reset password
- [x] **Auth UI Pages**:
  - Login page with email/password, remember me, navigation
  - Register page with password strength indicator
  - Email confirmation page with 6-digit code input
  - Forgot password page with email submission

#### ✅ Navigation & Routing
- [x] **GoRouter** configuration (`lib/core/routing/`)
  - Declarative routing with auth-based redirects
  - Route guards (authenticated vs unauthenticated)
  - Email confirmation redirect handling
  - Shell route for main app with bottom navigation

#### ✅ Shared Components
- [x] **MainShell** with bottom navigation (4 tabs: Dashboard, Orders, Invoices, Profile)
- [x] **AuthTextField** reusable form component
- [x] Placeholder pages for Orders, Invoices, Profile tabs

#### ✅ API Client Generation
- [x] OpenAPI Generator configured (`dart-dio` generator)
- [x] Generated 18 API classes from backend Swagger spec
- [x] Generated models with JSON serialization
- [x] Path dependency configured in `pubspec.yaml`
- [x] Build runner generates `.g.dart` files

#### ✅ Dashboard Feature
- [x] **DashboardBloc** (`lib/features/dashboard/bloc/`)
  - Events: LoadRequested, RefreshRequested, DateRangeChanged
  - State: stats, earnings, orders, time analytics, productivity metrics
  - Full data loading with error handling
- [x] **Dashboard Models** (`lib/core/models/dashboard/`)
  - DashboardStats, EarningsAnalytics, OrderAnalytics
  - TimeAnalytics, ProductivityMetrics
  - All fromDto converters for API integration
- [x] **DashboardRepository** (`lib/core/services/dashboard_repository.dart`)
  - getStats, getEarningsAnalytics, getOrderAnalytics
  - getTimeAnalytics, getProductivityMetrics
- [x] **Dashboard UI**:
  - Dashboard page with stats cards (4 cards in 2x2 grid)
  - Earnings chart with fl_chart
  - Pull-to-refresh functionality
  - Loading states with shimmer effects

#### ✅ Orders Feature
- [x] **OrdersBloc** (`lib/features/orders/bloc/`)
  - Events: LoadRequested, RefreshRequested, LoadMoreRequested, TabChanged, FilterChanged
  - Events: TakeRequested, StartRequested, CompleteRequested, ErrorCleared, SuccessCleared
  - State: orders list, filter, pagination, loading states, error/success messages
- [x] **Order Models** (`lib/core/models/orders/`)
  - Order, OrderStatus enum, PaymentStatus enum
  - ServiceItem, PackageItem with fromDto converters
  - OrderFilter with toQueryParams() for API
  - PagedOrders for pagination support
- [x] **OrdersRepository** (`lib/core/services/orders_repository.dart`)
  - getOrders, getAvailableOrders, getMyOrders
  - getOrderById, takeOrder, startOrder, completeOrder
  - uploadOrderPhoto with multipart form data
- [x] **Orders UI**:
  - Orders list page with TabBar (Available/My Orders)
  - OrderCard widget with status badges and action buttons
  - OrderCardSkeleton for loading states
  - Filter bottom sheet with search, date range
  - Order details page with all sections
  - Take/Start/Complete confirmation dialogs
  - url_launcher integration (phone, email, maps)

#### ✅ Invoices Feature
- [x] **InvoicesBloc** (`lib/features/invoices/bloc/`)
  - Events: LoadRequested, RefreshRequested, LoadMoreRequested, FilterChanged
  - Events: DownloadRequested, ErrorCleared, SuccessCleared
  - State: invoices list, filter, pagination, loading states, error/success messages
- [x] **Invoice Models** (`lib/core/models/invoices/`)
  - Invoice, InvoiceDetail with fromDto converters
  - InvoiceStatus enum with fromValue(int) for API integer status
  - OrderPay for order payment line items
  - InvoiceFilter with toQueryParams() for API
  - PagedInvoices for pagination support
- [x] **InvoicesRepository** (`lib/core/services/invoices_repository.dart`)
  - getInvoices with filter and pagination
  - getInvoiceById for invoice details
  - getInvoicePdfUrl for PDF download URL
  - downloadInvoicePdf for raw PDF bytes
- [x] **Invoices UI**:
  - Invoices list page with filter bottom sheet
  - InvoiceCard widget with status badges and download button
  - InvoiceCardSkeleton for loading states
  - Filter sheet with search, date range
  - Invoice details page with all sections:
    - Status card with amount
    - Invoice information section
    - Financial summary (subtotal, bonus, deductions, total)
    - Status timeline (generated, approved, paid)
    - Order payments list
    - Admin notes and bank transfer notes
  - PDF download via url_launcher

#### ✅ Profile Feature
- [x] **ProfileBloc** (`lib/features/profile/bloc/`)
  - Events: LoadRequested, RefreshRequested, PersonalInfoUpdated, BankDetailsUpdated
  - Events: EmergencyContactUpdated, DocumentsLoadRequested, DocumentUploadRequested
  - Events: DocumentDeleteRequested, ErrorCleared, SuccessCleared
  - State: profile, documents list, loading states, saving states, error/success messages
- [x] **Profile Models** (`lib/core/models/profile/`)
  - EmployeeProfile with personal info, address, emergency contact, availability
  - DocumentType enum (10 document types with isRequired flag)
  - DocumentStatus enum (Pending, Approved, Rejected)
  - EmployeeDocument with status, file info, review notes
  - UpdateProfileRequest for API updates
  - TimeRange for availability scheduling
- [x] **ProfileRepository** (`lib/core/services/profile_repository.dart`)
  - getCurrentProfile for employee data
  - updateProfile for saving changes
  - getMyDocuments for document list
  - uploadDocument with multipart form data
  - deleteDocument for removing documents
  - downloadDocument for raw bytes
- [x] **Profile UI**:
  - Profile page with TabBar (Personal, Bank, Documents, Settings)
  - Personal Information tab:
    - Profile header with avatar and email
    - Personal details form (name, phone, birth date)
    - Address form (street, city, zip code)
    - Emergency contact card with edit dialog
    - Save changes button with validation
  - Bank Details tab:
    - Info card about payment requirements
    - IBAN input with validation
    - Tax ID (optional) input
    - Bank status indicator card
    - Save changes button
  - Documents tab:
    - Status summary card (complete/incomplete/action required)
    - Required documents section with status indicators
    - Optional documents section
    - Document type cards with expand/collapse:
      - Upload button with camera/gallery picker
      - Download button for existing documents
      - Delete confirmation dialog
      - Rejection reason display for rejected documents
    - Upload new document bottom sheet
  - Settings tab:
    - Language selection dialog (English, Deutsch, Slovenčina)
    - Theme selection dialog (System, Light, Dark)
    - Notifications settings placeholder
    - App version info
    - Terms of Service and Privacy Policy links
    - Change password placeholder
    - Logout with confirmation dialog

#### ✅ Phase 7: Polish Features
- [x] **Shared Widgets** (`lib/shared/widgets/`)
  - ErrorView widget for error states (network, server, generic)
  - EmptyState widget for empty lists (no orders, invoices, documents, search results)
  - LoadingSkeleton widget with shimmer animation
  - ListItemSkeleton, CardSkeleton, FormFieldSkeleton, ProfileInfoSkeleton
  - Barrel export file (widgets.dart)
- [x] **Settings Integration**
  - Settings tab added to Profile page
  - Logout functionality with confirmation
  - Language/theme dialogs (ready for implementation)
  - App info and legal links

#### ✅ Dependencies Added
```yaml
# Navigation
go_router: ^14.0.0

# API & Networking
dio: ^5.4.0
json_annotation: ^4.9.0

# State Management
flutter_bloc: ^8.1.6
bloc: ^8.1.4
equatable: ^2.0.5

# Local Storage
shared_preferences: ^2.2.0
flutter_secure_storage: ^9.0.0

# UI Components
cached_network_image: ^3.3.0
shimmer: ^3.0.0
fl_chart: ^0.68.0

# Utilities
connectivity_plus: ^6.0.0
permission_handler: ^11.0.0
image_picker: ^1.0.0
url_launcher: ^6.2.0
```

### What's Left To Do

#### ⏳ Phase 8: Deployment (Next)
- [ ] Android keystore generation
- [ ] Android signing configuration
- [ ] iOS provisioning profiles
- [ ] App icons (replace placeholders with Cleansia branding)
- [ ] Splash screen
- [ ] Play Store listing preparation
- [ ] App Store listing preparation
- [ ] Beta testing via TestFlight/Firebase App Distribution

### Project Location

```
src/cleansia_mobile/
├── lib/
│   ├── app/                      # App widget & initialization
│   ├── core/
│   │   ├── api/                  # API client, interceptors, result pattern
│   │   ├── constants/            # API endpoints, app constants
│   │   ├── models/               # Auth, Dashboard, Orders, Invoices, Profile models
│   │   ├── routing/              # GoRouter configuration
│   │   └── services/             # Repositories (Auth, Dashboard, Orders, Invoices, Profile)
│   ├── features/
│   │   ├── auth/                 # Login, Register, Confirm, ForgotPassword
│   │   ├── dashboard/            # Dashboard stats, charts, analytics
│   │   ├── orders/               # Orders list, details, take/start/complete
│   │   ├── invoices/             # Invoices list, details, PDF download
│   │   └── profile/              # Profile tabs, personal info, bank, documents
│   ├── shared/
│   │   └── widgets/              # MainShell, reusable components
│   └── l10n/                     # Localization
├── android/                      # Android platform files
├── ios/                          # iOS platform files
├── pubspec.yaml                  # Dependencies
└── README.md
```

### How to Run

```bash
# Navigate to mobile project
cd src/cleansia_mobile

# Get dependencies
flutter pub get

# Generate API client (requires backend running)
curl -s http://localhost:5000/swagger/v1/swagger.json -o swagger.json
npx @openapitools/openapi-generator-cli generate \
  -i swagger.json \
  -g dart-dio \
  -o lib/core/api/generated \
  --skip-validate-spec

# Run build_runner for generated code
cd lib/core/api/generated && dart run build_runner build --delete-conflicting-outputs
cd ../../../..

# Run on Android emulator
flutter run

# Run on Chrome (web)
flutter run -d chrome
```

---

## Executive Summary

This document outlines the requirements for building a mobile version of the Cleansia Partner App. The mobile app will provide cleaning employees with the ability to:
- View and claim available cleaning orders
- Manage their assigned orders (start, complete with photos)
- Track earnings and view invoices
- Manage their profile and documents
- View real-time dashboard analytics

---

## Key Questions Answered

### Q1: Is there something similar to Nx for mobile development?

**Short Answer:** Flutter doesn't have an Nx equivalent, but there are established architecture patterns and CLI tools.

#### Architecture Standards

| Pattern | Description | Comparison to Web |
|---------|-------------|-------------------|
| **Clean Architecture + BLoC** | Most popular, separates UI/business logic/data | Similar to Nx + NgRx structure |
| **Riverpod** | Modern alternative to BLoC, less boilerplate | Simpler state management |
| **GetX** | All-in-one solution | Not recommended for large apps |

#### Recommended: Clean Architecture + BLoC

This is the **industry standard** for Flutter apps and mirrors your Angular/Nx structure:

| Nx (Angular) | Flutter Equivalent |
|--------------|-------------------|
| `apps/` | Single app (Flutter handles multi-platform) |
| `libs/feature-*` | `lib/features/` |
| `libs/shared/` | `lib/shared/` |
| `libs/core/` | `lib/core/` |
| NgRx Store | BLoC (flutter_bloc) |
| Services | Repositories |
| NSwag clients | Generated API clients (openapi-generator) |

#### CLI Tools for Consistency

| Tool | Purpose | Nx Equivalent |
|------|---------|---------------|
| **very_good_cli** | Project scaffolding with best practices | `nx generate` |
| **mason** | Code generation templates | Nx generators |
| **flutter_lints** / **very_good_analysis** | Strict linting rules | ESLint config |
| **build_runner** | Code generation (JSON, API clients) | NSwag, build tools |

**Setup Very Good CLI:**
```bash
# Install globally
dart pub global activate very_good_cli

# Create project with best practices (100+ lint rules, test coverage)
very_good create flutter_app cleansia_mobile --org com.cleansia

# Generate feature module
very_good create flutter_package orders --org com.cleansia
```

**Setup Mason for Templates:**
```bash
# Install mason
dart pub global activate mason_cli

# Use community bricks (templates)
mason add bloc                    # BLoC feature template
mason add feature_brick           # Clean architecture feature
mason make bloc --name orders     # Generate orders BLoC
```

---

### Q2: Do I need a separate API project for mobile?

**Short Answer:** No, your existing `Cleansia.Api` is sufficient.

#### Why You Don't Need a Separate API

| Concern | Solution |
|---------|----------|
| **Same business logic** | Mobile uses exact same endpoints |
| **Authentication** | JWT tokens work identically |
| **Data models** | Same DTOs for both platforms |
| **Performance** | API already optimized |

#### Mobile-Specific Features (If Needed Later)

If you need mobile-specific functionality in the future, add to existing API:

```csharp
// Example: Push notification device registration
[HttpPost("devices/register")]
public async Task<IActionResult> RegisterDevice(RegisterDeviceCommand command)
{
    // command: { DeviceToken, Platform (iOS/Android), UserId }
}

// Example: Image optimization endpoint (return smaller images for mobile)
[HttpGet("orders/{orderId}/photos")]
public async Task<IActionResult> GetPhotos(
    [FromRoute] Guid orderId,
    [FromQuery] int? maxWidth = null)  // Mobile passes maxWidth=800
```

#### Current API Endpoints Already Support Mobile

Your existing endpoints are **mobile-ready**:
- `POST /api/Auth/Login` - Returns JWT token
- `GET /api/Orders/GetPaged` - Pagination works on mobile
- `POST /api/Orders/CompleteOrder` - Multipart upload for photos
- `GET /api/EmployeePayroll/DownloadInvoice/{id}` - PDF download

**No changes needed to start mobile development.**

---

### Q3: Is there something similar to NSwag for mobile?

**Short Answer:** Yes! Use `openapi-generator` to auto-generate Dart API clients from your Swagger spec.

#### API Client Generation Options

| Tool | Description | Recommendation |
|------|-------------|----------------|
| **openapi-generator** | Generates Dart client from OpenAPI/Swagger | **Recommended** |
| **swagger_parser** | Dart-native generator | Good alternative |
| **chopper + chopper_generator** | Retrofit-like with annotations | Manual setup |
| **dio + retrofit** | Annotation-based generation | More control |

#### Recommended: openapi-generator (Like NSwag)

**Step 1: Install**
```bash
# Using npm (like NSwag)
npm install -g @openapitools/openapi-generator-cli

# Or using Homebrew (macOS)
brew install openapi-generator
```

**Step 2: Generate Dart Client**
```bash
# From your Swagger JSON (run backend first)
openapi-generator-cli generate \
  -i http://localhost:5000/swagger/v1/swagger.json \
  -g dart-dio \
  -o lib/core/api/generated \
  --additional-properties=pubName=cleansia_api,pubAuthor=Cleansia

# Or from saved swagger.json file
openapi-generator-cli generate \
  -i ./swagger.json \
  -g dart-dio \
  -o lib/core/api/generated
```

**Step 3: What Gets Generated**

```
lib/core/api/generated/
├── lib/
│   ├── api.dart                    # Main API entry point
│   ├── api/                        # API classes
│   │   ├── auth_api.dart           # POST /api/Auth/*
│   │   ├── orders_api.dart         # GET/POST /api/Orders/*
│   │   ├── employee_payroll_api.dart
│   │   └── dashboard_api.dart
│   ├── model/                      # Generated DTOs
│   │   ├── login_command.dart
│   │   ├── jwt_token_response.dart
│   │   ├── order_dto.dart
│   │   ├── paged_result_order_dto.dart
│   │   └── ... (all your DTOs)
│   └── serializers.dart            # JSON serialization
├── pubspec.yaml                    # Package dependencies
└── README.md
```

**Step 4: Usage in Flutter**

```dart
// Import generated client
import 'package:cleansia_api/api.dart';

// Configure with base URL and auth
final apiClient = ApiClient(basePath: 'https://api.cleansia.cz');
apiClient.addDefaultHeader('Authorization', 'Bearer $token');

// Use type-safe API calls
final authApi = AuthApi(apiClient);
final response = await authApi.login(LoginCommand(
  email: 'user@example.com',
  password: 'password123',
));

final ordersApi = OrdersApi(apiClient);
final orders = await ordersApi.getPagedOrders(
  pageNumber: 1,
  pageSize: 20,
  status: 'Confirmed',
);
```

#### Automation Script (Like NSwag in CI)

Create `scripts/generate-api.sh`:
```bash
#!/bin/bash
# Generate API client from backend Swagger

# Ensure backend is running or use saved swagger.json
SWAGGER_URL="${1:-http://localhost:5000/swagger/v1/swagger.json}"

echo "Generating API client from: $SWAGGER_URL"

openapi-generator-cli generate \
  -i "$SWAGGER_URL" \
  -g dart-dio \
  -o lib/core/api/generated \
  --additional-properties=pubName=cleansia_api \
  --skip-validate-spec

echo "Running build_runner for additional codegen..."
dart run build_runner build --delete-conflicting-outputs

echo "API client generated successfully!"
```

Add to `pubspec.yaml`:
```yaml
dependencies:
  # Use generated package as local dependency
  cleansia_api:
    path: lib/core/api/generated
```

#### Comparison: NSwag vs openapi-generator

| NSwag (C#/.NET) | openapi-generator (Flutter) |
|-----------------|----------------------------|
| `nswag.json` config | CLI arguments or config file |
| Generates TypeScript client | Generates Dart client |
| HttpClient wrapper | Dio HTTP client |
| Models with interfaces | Dart classes with JSON serialization |
| `npm run nswag` | `./scripts/generate-api.sh` |

---

## Technology Comparison

### Option 1: Flutter (Recommended)

| Aspect | Details |
|--------|---------|
| **Language** | Dart |
| **Learning Curve** | Moderate (2-4 weeks for web developers) |
| **Performance** | Near-native (compiles to ARM) |
| **Single Codebase** | iOS + Android + Web + Desktop |
| **Hot Reload** | Yes (instant) |
| **UI Components** | Rich Material & Cupertino widgets |
| **Community** | Large, growing rapidly |
| **Backed By** | Google |

**Pros:**
- Single codebase for iOS and Android
- Excellent performance (60fps animations)
- Rich ecosystem of packages
- Strong typing with Dart
- Great documentation
- Similar reactive patterns to Angular (familiar for you)

**Cons:**
- New language to learn (Dart)
- Larger app size (~15-20MB base)
- Less native feel compared to native development

### Option 2: React Native

| Aspect | Details |
|--------|---------|
| **Language** | JavaScript/TypeScript |
| **Learning Curve** | Lower (if you know React) |
| **Performance** | Good (JavaScript bridge) |
| **Single Codebase** | iOS + Android |
| **Hot Reload** | Yes |
| **UI Components** | Community-driven |
| **Community** | Very large |
| **Backed By** | Meta (Facebook) |

**Pros:**
- Uses JavaScript/TypeScript (familiar)
- Large ecosystem
- Code sharing with web possible

**Cons:**
- JavaScript bridge can cause performance issues
- More native code needed for complex features
- Fragmented ecosystem

### Option 3: Native Development

| Aspect | iOS (Swift) | Android (Kotlin) |
|--------|-------------|------------------|
| **Performance** | Best | Best |
| **Learning Curve** | High | High |
| **Codebase** | Separate | Separate |
| **Maintenance** | 2x effort | 2x effort |

**Not recommended** for your use case due to:
- Double development effort
- Double maintenance cost
- Requires learning two platforms

### Recommendation: Flutter

**Why Flutter is best for Cleansia Partner App:**

1. **Single Codebase** - Write once, deploy to iOS and Android
2. **Performance** - Native ARM compilation, smooth 60fps
3. **Similar Patterns** - Reactive programming similar to Angular/RxJS
4. **Rich Widgets** - Built-in Material Design components
5. **State Management** - BLoC pattern similar to NgRx
6. **Rapid Development** - Hot reload speeds up development
7. **Future-Proof** - Can expand to web/desktop later

---

## Recommended Stack: Flutter

### Core Technologies

```yaml
# Core
flutter: ^3.19.0
dart: ^3.3.0

# State Management
flutter_bloc: ^8.1.0          # BLoC pattern (similar to NgRx)
equatable: ^2.0.5             # Value equality for states

# Navigation
go_router: ^13.0.0            # Declarative routing

# API & Networking
dio: ^5.4.0                   # HTTP client (like HttpClient)
retrofit: ^4.1.0              # Type-safe API client generator
json_annotation: ^4.8.0       # JSON serialization

# Local Storage
shared_preferences: ^2.2.0    # Simple key-value storage
flutter_secure_storage: ^9.0.0 # Secure token storage
hive: ^2.2.3                  # NoSQL local database

# UI Components
flutter_svg: ^2.0.9           # SVG support
cached_network_image: ^3.3.0  # Image caching
shimmer: ^3.0.0               # Loading skeletons
fl_chart: ^0.66.0             # Charts (like ng2-charts)

# Forms & Validation
reactive_forms: ^16.1.0       # Reactive forms (like Angular)

# Internationalization
intl: ^0.19.0                 # Localization
easy_localization: ^3.0.3     # i18n management

# Utilities
connectivity_plus: ^5.0.0     # Network connectivity
permission_handler: ^11.0.0   # Runtime permissions
image_picker: ^1.0.0          # Camera/gallery access
url_launcher: ^6.2.0          # Open URLs/PDFs
```

---

## Development Environment Setup

### Prerequisites

#### 1. Install Flutter SDK

**Windows:**
```powershell
# Option A: Using Chocolatey
choco install flutter

# Option B: Manual Installation
# Download from https://docs.flutter.dev/get-started/install/windows
# Extract to C:\flutter
# Add C:\flutter\bin to PATH
```

**macOS:**
```bash
# Using Homebrew
brew install flutter

# Or download from https://docs.flutter.dev/get-started/install/macos
```

**Linux:**
```bash
# Using snap
sudo snap install flutter --classic
```

#### 2. Verify Installation

```bash
flutter doctor
```

Expected output:
```
[✓] Flutter (Channel stable, 3.19.x)
[✓] Android toolchain
[✓] Xcode (for iOS development on macOS)
[✓] Chrome (for web development)
[✓] Android Studio
[✓] VS Code
```

#### 3. Install Android Studio

1. Download from https://developer.android.com/studio
2. Install Android SDK (API 34+)
3. Create Android Virtual Device (emulator)
4. Install Flutter plugin

#### 4. Install Xcode (macOS only, for iOS)

```bash
xcode-select --install
sudo xcodebuild -license accept
```

#### 5. VS Code Extensions

Install these VS Code extensions:
- **Flutter** - Dart/Flutter support
- **Dart** - Dart language support
- **Flutter Widget Snippets** - Code snippets
- **Bloc** - BLoC pattern snippets

### Create Project

```bash
# Create new Flutter project
flutter create cleansia_partner --org com.cleansia

# Navigate to project
cd cleansia_partner

# Get dependencies
flutter pub get

# Run on connected device/emulator
flutter run
```

---

## Project Structure

```
cleansia_partner/
├── android/                    # Android-specific code
├── ios/                        # iOS-specific code
├── lib/
│   ├── main.dart              # App entry point
│   ├── app.dart               # App widget & theme
│   │
│   ├── config/
│   │   ├── app_config.dart    # Environment config
│   │   ├── routes.dart        # Route definitions
│   │   └── theme.dart         # App theme (colors, typography)
│   │
│   ├── core/
│   │   ├── api/
│   │   │   ├── api_client.dart        # Dio HTTP client
│   │   │   ├── interceptors/          # Auth, error interceptors
│   │   │   └── endpoints.dart         # API endpoints
│   │   │
│   │   ├── models/                    # Data models (DTOs)
│   │   │   ├── order.dart
│   │   │   ├── invoice.dart
│   │   │   ├── employee.dart
│   │   │   └── dashboard.dart
│   │   │
│   │   ├── services/
│   │   │   ├── auth_service.dart      # Authentication
│   │   │   ├── storage_service.dart   # Local storage
│   │   │   └── notification_service.dart
│   │   │
│   │   └── utils/
│   │       ├── constants.dart
│   │       ├── validators.dart
│   │       └── extensions.dart
│   │
│   ├── features/
│   │   ├── auth/
│   │   │   ├── bloc/
│   │   │   │   ├── auth_bloc.dart
│   │   │   │   ├── auth_event.dart
│   │   │   │   └── auth_state.dart
│   │   │   ├── screens/
│   │   │   │   ├── login_screen.dart
│   │   │   │   ├── register_screen.dart
│   │   │   │   └── forgot_password_screen.dart
│   │   │   └── widgets/
│   │   │
│   │   ├── dashboard/
│   │   │   ├── bloc/
│   │   │   ├── screens/
│   │   │   │   └── dashboard_screen.dart
│   │   │   └── widgets/
│   │   │       ├── stat_card.dart
│   │   │       ├── earnings_chart.dart
│   │   │       └── upcoming_orders.dart
│   │   │
│   │   ├── orders/
│   │   │   ├── bloc/
│   │   │   │   ├── orders_bloc.dart
│   │   │   │   └── order_details_bloc.dart
│   │   │   ├── screens/
│   │   │   │   ├── orders_screen.dart
│   │   │   │   └── order_details_screen.dart
│   │   │   └── widgets/
│   │   │       ├── order_card.dart
│   │   │       ├── order_filter_drawer.dart
│   │   │       └── complete_order_dialog.dart
│   │   │
│   │   ├── invoices/
│   │   │   ├── bloc/
│   │   │   ├── screens/
│   │   │   │   ├── invoices_screen.dart
│   │   │   │   └── invoice_details_screen.dart
│   │   │   └── widgets/
│   │   │
│   │   └── profile/
│   │       ├── bloc/
│   │       ├── screens/
│   │       │   └── profile_screen.dart
│   │       └── widgets/
│   │           ├── personal_info_form.dart
│   │           ├── bank_details_form.dart
│   │           ├── documents_section.dart
│   │           └── availability_calendar.dart
│   │
│   └── shared/
│       ├── widgets/
│       │   ├── app_bar.dart
│       │   ├── bottom_nav.dart
│       │   ├── loading_indicator.dart
│       │   ├── error_widget.dart
│       │   ├── status_badge.dart
│       │   └── filter_chip.dart
│       │
│       └── l10n/                      # Translations
│           ├── app_en.arb
│           └── app_cs.arb
│
├── assets/
│   ├── images/
│   ├── icons/
│   └── fonts/
│
├── test/                              # Unit & widget tests
├── integration_test/                  # Integration tests
├── pubspec.yaml                       # Dependencies
└── README.md
```

---

## Feature Requirements

### 1. Authentication Module

#### 1.1 Login Screen
| Field | Type | Validation |
|-------|------|------------|
| Email | TextInput | Required, valid email format |
| Password | TextInput | Required, min 8 chars |
| Remember Me | Checkbox | Optional |

**Functionality:**
- [ ] Email/password form validation
- [ ] Secure password input (show/hide toggle)
- [ ] "Remember me" checkbox (persistent session)
- [ ] Error handling with user-friendly messages
- [ ] Loading state during authentication
- [ ] Biometric login option (Face ID/Fingerprint)
- [ ] Navigate to registration/forgot password

**API Endpoint:** `POST /api/Auth/Login`

```dart
// Request
class LoginCommand {
  final String email;
  final String password;
  final bool rememberMe;
}

// Response
class JwtTokenResponse {
  final String token;
  final bool isEmailConfirmed;
}
```

#### 1.2 Registration Screen
| Field | Type | Validation |
|-------|------|------------|
| Email | TextInput | Required, valid email, unique |
| Password | TextInput | Min 8 chars, uppercase, lowercase, number, special char |
| Confirm Password | TextInput | Must match password |
| First Name | TextInput | Required |
| Last Name | TextInput | Required |
| Language | Dropdown | Required (cs/en) |
| Terms Acceptance | Checkbox | Required |

**Functionality:**
- [ ] Password strength indicator
- [ ] Real-time validation feedback
- [ ] Terms & conditions link
- [ ] Navigate to email confirmation after success

**API Endpoint:** `POST /api/Auth/RegisterEmployee`

#### 1.3 Email Confirmation Screen
- [ ] 6-digit confirmation code input
- [ ] Resend confirmation email button
- [ ] Timer for resend cooldown (60 seconds)
- [ ] Navigate to dashboard on success

**API Endpoints:**
- `POST /api/Auth/ConfirmUserEmail`
- `POST /api/Auth/ResendConfirmationEmail`

#### 1.4 Forgot Password Flow
- [ ] Email input for reset request
- [ ] Reset code entry screen
- [ ] New password setup screen
- [ ] Success confirmation

**API Endpoints:**
- `POST /api/Auth/ForgotPassword`
- `POST /api/Auth/ResetPassword`

---

### 2. Dashboard Module

#### 2.1 Dashboard Screen

**Stat Cards (4 cards in 2x2 grid):**
| Stat | Description | Icon |
|------|-------------|------|
| Available Orders | Count of orders available to claim | 📋 |
| My Active Orders | Count of assigned pending orders | 🔄 |
| Completed (Month) | Orders completed this month + trend | ✅ |
| Pending Earnings | Unpaid earnings amount | 💰 |

**Upcoming Orders Section:**
- [ ] List of next 5 upcoming orders
- [ ] Show: Date, Time, Address, Status badge
- [ ] Tap to navigate to order details

**Analytics Section (Date Range Filter):**
- [ ] Earnings chart (line/bar chart)
- [ ] Time analytics (hours worked)
- [ ] Order distribution (pie chart)
- [ ] Productivity metrics (gauges)
- [ ] Pull-to-refresh functionality

**API Endpoints:**
- `GET /api/Dashboard/GetStats` - All statistics
- `GET /api/Dashboard/GetEarningsAnalytics`
- `GET /api/Dashboard/GetTimeAnalytics`
- `GET /api/Dashboard/GetOrderAnalytics`
- `GET /api/Dashboard/GetProductivityMetrics`

---

### 3. Orders Module

#### 3.1 Orders List Screen

**Tabs:**
1. **Available Orders** - Unassigned orders with available spots
2. **My Orders** - Employee's assigned orders

**Order Card Display:**
- Order number
- Customer name
- Cleaning date & time
- Address (abbreviated)
- Status badge (color-coded)
- Estimated time
- Total price

**Filtering (Drawer/Bottom Sheet):**
| Filter | Type | Options |
|--------|------|---------|
| Search | TextInput | Customer name, email, order number |
| Order Status | Multi-select chips | Pending, Confirmed, InProgress, Completed, Cancelled |
| Payment Status | Multi-select chips | Pending, Paid, Failed, Refunded |
| Date Range | Date picker | From date, To date |

**Functionality:**
- [ ] Tab switching
- [ ] Pull-to-refresh
- [ ] Infinite scroll pagination
- [ ] Filter drawer with active filter chips
- [ ] Clear all filters button
- [ ] Sort by date (ascending/descending)
- [ ] Tap card to view details

**API Endpoint:** `POST /api/Orders/GetPaged`

#### 3.2 Order Details Screen

**Sections:**

**Header:**
- Order number
- Status badge
- Payment status badge
- Created date

**Customer Information:**
- Full name
- Email (tappable - opens email app)
- Phone (tappable - opens dialer)
- Full address (tappable - opens maps)

**Service Details:**
- Cleaning date & time
- Number of rooms/bathrooms
- Estimated time
- Package name
- Additional services list
- Special instructions
- Access instructions

**Price Information:**
- Sub-total
- Discounts (if any)
- Total price
- Currency

**Photos Section (if InProgress/Completed):**
- Before photos gallery
- After photos gallery (if completed)
- Full-screen photo viewer

**Action Buttons:**
| Status | Available Actions |
|--------|-------------------|
| Available | "Take Order" button |
| Confirmed (assigned to me) | "Start Order" button |
| InProgress (assigned to me) | "Complete Order" button |
| Completed | "Download Receipt" button |

**Take Order Flow:**
- [ ] Confirmation dialog
- [ ] Success/error handling
- [ ] Redirect to My Orders tab

**Start Order Flow:**
- [ ] Confirmation dialog
- [ ] Status updates to InProgress
- [ ] Enable photo uploads

**Complete Order Flow (Bottom Sheet/Dialog):**
- [ ] Upload after photos (camera or gallery)
- [ ] Minimum 1 photo required
- [ ] Enter actual completion time (minutes)
- [ ] Optional completion notes
- [ ] Submit button
- [ ] Loading state during upload
- [ ] Success confirmation

**API Endpoints:**
- `GET /api/Orders/{orderId}`
- `POST /api/Orders/TakeOrder`
- `POST /api/Orders/StartOrder`
- `POST /api/Orders/CompleteOrder`
- `GET /api/Orders/{orderId}/DownloadReceipt`

---

### 4. Invoices Module

#### 4.1 Invoices List Screen

**Invoice Card Display:**
- Invoice number
- Variable symbol
- Pay period label
- Total amount (with currency)
- Status badge
- Generated date

**Status Colors:**
| Status | Color |
|--------|-------|
| Pending | Orange |
| Approved | Blue |
| Paid | Green |
| Disputed | Red |
| Rejected | Dark Red |
| Cancelled | Gray |

**Filtering:**
- Invoice number search
- Date range
- Status multi-select
- Amount range

**Functionality:**
- [ ] Pull-to-refresh
- [ ] Infinite scroll pagination
- [ ] Filter bottom sheet
- [ ] Tap to view details

**API Endpoint:** `GET /api/EmployeePayroll/GetPagedInvoices`

#### 4.2 Invoice Details Screen

**Display:**
- Invoice number & variable symbol
- Pay period
- Employee name
- Breakdown:
  - Number of orders
  - Sub-total
  - Bonus amount
  - Deductions
  - **Total amount**
- Status with timeline
- Bank transfer note (if applicable)
- Admin notes (if any)

**Actions:**
- [ ] Download PDF button
- [ ] Share invoice option

**API Endpoint:** `GET /api/EmployeePayroll/DownloadInvoice/{invoiceId}`

---

### 5. Profile Module

#### 5.1 Profile Screen (Tabbed)

**Tab 1: Personal Information**
| Field | Type | Validation |
|-------|------|------------|
| First Name | TextInput | Required |
| Last Name | TextInput | Required |
| Email | TextInput | Read-only |
| Phone | TextInput | Valid phone format |
| Date of Birth | DatePicker | Required, must be 18+ |
| Gender | Dropdown | Male/Female/Other |
| Nationality | Dropdown | Country list |
| Street | TextInput | Required |
| City | TextInput | Required |
| Postal Code | TextInput | Required |

**Tab 2: Bank Details**
| Field | Type | Validation |
|-------|------|------------|
| Bank Name | TextInput | Required |
| IBAN | TextInput | Valid IBAN format |
| Bank Code | TextInput | Optional |
| Account Holder | TextInput | Required |

**Tab 3: Emergency Contact**
| Field | Type |
|-------|------|
| Contact Name | TextInput |
| Relationship | Dropdown |
| Phone Number | TextInput |
| Address | TextInput |

**Tab 4: Availability**
- [ ] Calendar view (month/week)
- [ ] Tap days to toggle availability
- [ ] Time slot selection per day
- [ ] Recurring patterns

**Tab 5: Documents**
| Document Type | Required |
|---------------|----------|
| ID Document | Yes |
| Proof of Address | Yes |
| Tax Certificate | No |
| Insurance | No |
| Background Check | No |
| Bank Confirmation | No |

**Document Management:**
- [ ] View uploaded documents
- [ ] Upload new document (camera/file picker)
- [ ] Document status indicator (Pending/Approved/Rejected)
- [ ] Delete document option
- [ ] Download document option
- [ ] Re-upload rejected documents

**Save Profile:**
- [ ] Form validation
- [ ] Loading state during save
- [ ] Success/error toast notifications

**API Endpoints:**
- `GET /api/Employee/GetCurrent`
- `PUT /api/Employee/UpdateEmployee`
- `GET /api/Employee/GetMyDocuments`
- `POST /api/Employee/SaveMyDocuments`
- `DELETE /api/Employee/DeleteMyDocument/{documentId}`
- `GET /api/Employee/DownloadMyDocument/{documentId}`

---

### 6. Common/Shared Features

#### 6.1 Navigation
- Bottom navigation bar with 4 tabs:
  - Dashboard
  - Orders
  - Invoices
  - Profile
- Active tab indicator

#### 6.2 App Bar
- Screen title
- Language switcher (CS/EN flag icons)
- Notification bell (future)
- Logout option (in settings/profile)

#### 6.3 Settings
- Language selection
- Notification preferences
- Dark mode toggle (optional)
- App version info
- Logout button

#### 6.4 Error Handling
- Network error screen with retry
- Session expired handling
- Form validation errors
- API error messages

#### 6.5 Loading States
- Skeleton loaders for lists
- Shimmer effects
- Progress indicators

#### 6.6 Offline Support (Optional)
- Cache last loaded data
- Offline indicator
- Queue actions for sync

---

## API Integration

### Base Configuration

```dart
// lib/core/api/api_client.dart
class ApiClient {
  late Dio _dio;

  ApiClient() {
    _dio = Dio(BaseOptions(
      baseUrl: AppConfig.apiBaseUrl,  // https://api.cleansia.cz
      connectTimeout: Duration(seconds: 30),
      receiveTimeout: Duration(seconds: 30),
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
      },
    ));

    _dio.interceptors.addAll([
      AuthInterceptor(),      // Adds Bearer token
      ErrorInterceptor(),     // Handles errors
      LogInterceptor(),       // Logging (debug only)
    ]);
  }
}
```

### Authentication Interceptor

```dart
// lib/core/api/interceptors/auth_interceptor.dart
class AuthInterceptor extends Interceptor {
  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) async {
    final token = await SecureStorage.getToken();
    if (token != null) {
      options.headers['Authorization'] = 'Bearer $token';
    }
    handler.next(options);
  }

  @override
  void onError(DioException err, ErrorInterceptorHandler handler) {
    if (err.response?.statusCode == 401) {
      // Token expired - logout user
      AuthBloc.logout();
    }
    handler.next(err);
  }
}
```

### API Endpoints Summary

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/Auth/Login` | POST | User login |
| `/api/Auth/RegisterEmployee` | POST | Employee registration |
| `/api/Auth/ConfirmUserEmail` | POST | Confirm email |
| `/api/Auth/ResendConfirmationEmail` | POST | Resend confirmation |
| `/api/Auth/ForgotPassword` | POST | Request password reset |
| `/api/Auth/ResetPassword` | POST | Reset password |
| `/api/Dashboard/GetStats` | GET | Dashboard statistics |
| `/api/Dashboard/GetEarningsAnalytics` | GET | Earnings data |
| `/api/Dashboard/GetTimeAnalytics` | GET | Time analytics |
| `/api/Dashboard/GetOrderAnalytics` | GET | Order distribution |
| `/api/Dashboard/GetProductivityMetrics` | GET | Productivity data |
| `/api/Orders/GetPaged` | POST | Get paginated orders |
| `/api/Orders/{id}` | GET | Get order details |
| `/api/Orders/TakeOrder` | POST | Take available order |
| `/api/Orders/StartOrder` | POST | Start order |
| `/api/Orders/CompleteOrder` | POST | Complete order |
| `/api/Orders/{id}/DownloadReceipt` | GET | Download receipt PDF |
| `/api/EmployeePayroll/GetPagedInvoices` | GET | Get paginated invoices |
| `/api/EmployeePayroll/DownloadInvoice/{id}` | GET | Download invoice PDF |
| `/api/Employee/GetCurrent` | GET | Get current employee |
| `/api/Employee/UpdateEmployee` | PUT | Update employee profile |
| `/api/Employee/GetMyDocuments` | GET | Get employee documents |
| `/api/Employee/SaveMyDocuments` | POST | Upload documents |
| `/api/Employee/DeleteMyDocument/{id}` | DELETE | Delete document |
| `/api/Employee/DownloadMyDocument/{id}` | GET | Download document |
| `/api/Countries/GetOverview` | GET | Get countries list |

---

## State Management

### BLoC Pattern (Similar to NgRx)

The BLoC (Business Logic Component) pattern is recommended for Flutter and works similarly to NgRx:

| NgRx Concept | BLoC Equivalent |
|--------------|-----------------|
| Actions | Events |
| Reducers | Bloc (on<Event>) |
| State | State |
| Selectors | BlocBuilder/BlocSelector |
| Effects | Bloc event handlers |

### Example: Orders BLoC

```dart
// lib/features/orders/bloc/orders_event.dart
abstract class OrdersEvent extends Equatable {}

class LoadAvailableOrders extends OrdersEvent {
  final OrderFilter? filter;
  final int offset;
  final int limit;

  LoadAvailableOrders({this.filter, this.offset = 0, this.limit = 20});
}

class LoadMyOrders extends OrdersEvent {
  final OrderFilter? filter;
}

class TakeOrder extends OrdersEvent {
  final String orderId;
}

class ApplyFilter extends OrdersEvent {
  final OrderFilter filter;
}

// lib/features/orders/bloc/orders_state.dart
class OrdersState extends Equatable {
  final List<Order> availableOrders;
  final List<Order> myOrders;
  final bool isLoading;
  final String? error;
  final int totalRecords;
  final OrderFilter? activeFilter;

  const OrdersState({
    this.availableOrders = const [],
    this.myOrders = const [],
    this.isLoading = false,
    this.error,
    this.totalRecords = 0,
    this.activeFilter,
  });

  OrdersState copyWith({...});
}

// lib/features/orders/bloc/orders_bloc.dart
class OrdersBloc extends Bloc<OrdersEvent, OrdersState> {
  final OrderRepository _orderRepository;

  OrdersBloc(this._orderRepository) : super(OrdersState()) {
    on<LoadAvailableOrders>(_onLoadAvailableOrders);
    on<LoadMyOrders>(_onLoadMyOrders);
    on<TakeOrder>(_onTakeOrder);
    on<ApplyFilter>(_onApplyFilter);
  }

  Future<void> _onLoadAvailableOrders(
    LoadAvailableOrders event,
    Emitter<OrdersState> emit,
  ) async {
    emit(state.copyWith(isLoading: true, error: null));

    try {
      final result = await _orderRepository.getAvailableOrders(
        filter: event.filter,
        offset: event.offset,
        limit: event.limit,
      );

      emit(state.copyWith(
        isLoading: false,
        availableOrders: result.items,
        totalRecords: result.totalCount,
      ));
    } catch (e) {
      emit(state.copyWith(isLoading: false, error: e.toString()));
    }
  }

  // ... other event handlers
}
```

---

## UI/UX Guidelines

### Design Principles

1. **Clean & Professional** - Match the web app's aesthetic
2. **Thumb-Friendly** - Important actions within thumb reach
3. **Quick Actions** - Swipe gestures for common actions
4. **Offline-Ready** - Graceful degradation when offline
5. **Accessibility** - Support for screen readers, large fonts

### Color Scheme (from Web App)

```dart
class AppColors {
  // Primary
  static const primary = Color(0xFF2196F3);      // Blue
  static const primaryDark = Color(0xFF1976D2);
  static const primaryLight = Color(0xFFBBDEFB);

  // Status Colors
  static const success = Color(0xFF4CAF50);      // Green
  static const warning = Color(0xFFFF9800);      // Orange
  static const error = Color(0xFFF44336);        // Red
  static const info = Color(0xFF2196F3);         // Blue

  // Order Status
  static const pending = Color(0xFFFF9800);      // Orange
  static const confirmed = Color(0xFF2196F3);   // Blue
  static const inProgress = Color(0xFF9C27B0);  // Purple
  static const completed = Color(0xFF4CAF50);   // Green
  static const cancelled = Color(0xFF9E9E9E);   // Gray

  // Payment Status
  static const paid = Color(0xFF4CAF50);         // Green
  static const paymentPending = Color(0xFFFF9800);
  static const failed = Color(0xFFF44336);       // Red
  static const refunded = Color(0xFF9C27B0);     // Purple

  // Background
  static const background = Color(0xFFF5F5F5);
  static const surface = Color(0xFFFFFFFF);
  static const divider = Color(0xFFE0E0E0);
}
```

### Typography

```dart
class AppTypography {
  static const headlineLarge = TextStyle(
    fontSize: 24,
    fontWeight: FontWeight.bold,
    fontFamily: 'Nunito',
  );

  static const headlineMedium = TextStyle(
    fontSize: 20,
    fontWeight: FontWeight.w600,
  );

  static const bodyLarge = TextStyle(
    fontSize: 16,
    fontWeight: FontWeight.normal,
  );

  static const bodyMedium = TextStyle(
    fontSize: 14,
  );

  static const labelSmall = TextStyle(
    fontSize: 12,
    color: Colors.grey,
  );
}
```

### Component Examples

**Status Badge:**
```dart
class StatusBadge extends StatelessWidget {
  final String status;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: EdgeInsets.symmetric(horizontal: 12, vertical: 4),
      decoration: BoxDecoration(
        color: color.withOpacity(0.1),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: color),
      ),
      child: Text(
        status,
        style: TextStyle(color: color, fontWeight: FontWeight.w600),
      ),
    );
  }
}
```

---

## Testing Strategy

### Unit Tests
```bash
# Run unit tests
flutter test
```

Test coverage:
- BLoC event handlers
- Repository methods
- Model serialization
- Validators

### Widget Tests
- Screen layouts
- Form validation UI
- Component interactions

### Integration Tests
```bash
# Run integration tests
flutter test integration_test/
```

Test flows:
- Login → Dashboard
- Orders list → Take order → Complete
- Profile update

### CI/CD Testing
```yaml
# .github/workflows/flutter-ci.yml
name: Flutter CI

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: subosito/flutter-action@v2
        with:
          flutter-version: '3.19.0'
      - run: flutter pub get
      - run: flutter analyze
      - run: flutter test --coverage
```

---

## Deployment

### Android

1. **Generate Keystore:**
```bash
keytool -genkey -v -keystore cleansia-partner.jks -keyalg RSA -keysize 2048 -validity 10000 -alias cleansia
```

2. **Configure signing:**
```properties
# android/key.properties
storePassword=<password>
keyPassword=<password>
keyAlias=cleansia
storeFile=<path>/cleansia-partner.jks
```

3. **Build APK/App Bundle:**
```bash
# Debug APK
flutter build apk --debug

# Release APK
flutter build apk --release

# App Bundle (for Play Store)
flutter build appbundle --release
```

4. **Upload to Play Store:**
- Create Google Play Developer account ($25 one-time)
- Create new app in Play Console
- Upload .aab file
- Fill store listing (screenshots, description)
- Submit for review

### iOS

1. **Configure Xcode:**
- Open `ios/Runner.xcworkspace` in Xcode
- Set Bundle ID: `com.cleansia.partner`
- Set team/signing certificates

2. **Build IPA:**
```bash
flutter build ipa --release
```

3. **Upload to App Store:**
- Create Apple Developer account ($99/year)
- Create app in App Store Connect
- Upload via Xcode or Transporter
- Fill store listing
- Submit for review

### TestFlight/Firebase App Distribution

For beta testing:
```bash
# iOS TestFlight
flutter build ipa
# Upload via Transporter

# Android Firebase App Distribution
flutter build apk
firebase appdistribution:distribute app-release.apk --app <firebase-app-id>
```

---

## Estimated Timeline

| Phase | Tasks | Duration |
|-------|-------|----------|
| **Phase 1: Setup** | Project setup, architecture, CI/CD | 1 week |
| **Phase 2: Auth** | Login, Register, Password reset | 1 week |
| **Phase 3: Dashboard** | Stats, Charts, Analytics | 1 week |
| **Phase 4: Orders** | List, Details, Take/Start/Complete | 2 weeks |
| **Phase 5: Invoices** | List, Details, PDF download | 1 week |
| **Phase 6: Profile** | All tabs, Documents, Availability | 2 weeks |
| **Phase 7: Polish** | Testing, Bug fixes, Performance | 1 week |
| **Phase 8: Deployment** | Store submissions, Beta testing | 1 week |
| **Total** | | **10 weeks** |

---

## Next Steps

1. **Environment Setup**
   - Install Flutter SDK
   - Install Android Studio
   - Set up VS Code with Flutter extension
   - Create new Flutter project

2. **First Sprint Goals**
   - Project structure setup
   - API client configuration
   - Authentication flow (Login/Register)
   - Basic navigation

3. **Resources**
   - [Flutter Official Docs](https://docs.flutter.dev/)
   - [Flutter Cookbook](https://docs.flutter.dev/cookbook)
   - [BLoC Library Docs](https://bloclibrary.dev/)
   - [Dart Language Tour](https://dart.dev/language)

---

## Appendix: Quick Command Reference

```bash
# Create project
flutter create cleansia_partner --org com.cleansia

# Run app
flutter run                    # Default device
flutter run -d chrome          # Chrome
flutter run -d <device_id>     # Specific device

# List devices
flutter devices

# Build
flutter build apk              # Android APK
flutter build appbundle        # Android Bundle
flutter build ios              # iOS
flutter build web              # Web

# Test
flutter test                   # Unit tests
flutter test integration_test/ # Integration

# Analyze
flutter analyze                # Lint checks

# Dependencies
flutter pub get                # Get dependencies
flutter pub upgrade            # Upgrade dependencies
flutter pub outdated           # Check outdated

# Generate code (JSON serialization, etc.)
dart run build_runner build

# Clean
flutter clean
```
