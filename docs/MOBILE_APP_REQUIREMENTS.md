# Cleansia Partner Mobile App - Requirements & Implementation Guide

## Table of Contents
1. [Executive Summary](#executive-summary)
2. [Technology Comparison](#technology-comparison)
3. [Recommended Stack: Flutter](#recommended-stack-flutter)
4. [Development Environment Setup](#development-environment-setup)
5. [Project Structure](#project-structure)
6. [Feature Requirements](#feature-requirements)
7. [API Integration](#api-integration)
8. [State Management](#state-management)
9. [UI/UX Guidelines](#uiux-guidelines)
10. [Testing Strategy](#testing-strategy)
11. [Deployment](#deployment)
12. [Estimated Timeline](#estimated-timeline)

---

## Executive Summary

This document outlines the requirements for building a mobile version of the Cleansia Partner App. The mobile app will provide cleaning employees with the ability to:
- View and claim available cleaning orders
- Manage their assigned orders (start, complete with photos)
- Track earnings and view invoices
- Manage their profile and documents
- View real-time dashboard analytics

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
