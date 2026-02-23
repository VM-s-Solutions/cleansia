# Cleansia Partner iOS App - Swift Implementation Plan

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
12. [Implementation Phases](#implementation-phases)
13. [Deployment & CI/CD](#deployment--cicd)
14. [Progress Tracking](#progress-tracking)
15. [App Store Compliance](#app-store-compliance)

---

## Executive Summary

This document outlines the complete architecture, implementation strategy, and technical specifications for building the Cleansia Partner iOS application using native Swift and SwiftUI. The app enables cleaning service employees to manage orders, view invoices, and maintain their profiles while adhering to Apple's App Store guidelines and Human Interface Guidelines.

**Key Goals:**
- Native iOS experience with SwiftUI
- Full compliance with Apple's Human Interface Guidelines
- Secure authentication and data storage using Keychain
- Offline-first approach where applicable
- App Store compliance and submission readiness

---

## Architecture Overview

### MVVM with Combine Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           PRESENTATION LAYER                            │
├─────────────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐    │
│  │   SwiftUI   │  │   SwiftUI   │  │   SwiftUI   │  │   SwiftUI   │    │
│  │    Views    │  │    Views    │  │    Views    │  │    Views    │    │
│  │ (Dashboard) │  │  (Orders)   │  │ (Invoices)  │  │  (Profile)  │    │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘    │
│         │                │                │                │            │
│         ▼                ▼                ▼                ▼            │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐    │
│  │ ViewModel   │  │ ViewModel   │  │ ViewModel   │  │ ViewModel   │    │
│  │ @Published  │  │ @Published  │  │ @Published  │  │ @Published  │    │
│  │   States    │  │   States    │  │   States    │  │   States    │    │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘    │
│         │                │                │                │            │
│         └────────────────┴────────┬───────┴────────────────┘            │
├───────────────────────────────────┼─────────────────────────────────────┤
│                           DOMAIN LAYER                                  │
├───────────────────────────────────┼─────────────────────────────────────┤
│                                   ▼                                     │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                        REPOSITORIES                              │   │
│  │  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌───────┐  │   │
│  │  │  Auth   │  │Dashboard│  │ Orders  │  │Invoices │  │Profile│  │   │
│  │  └─────────┘  └─────────┘  └─────────┘  └─────────┘  └───────┘  │   │
│  └──────────────────────────────┬──────────────────────────────────┘   │
├─────────────────────────────────┼───────────────────────────────────────┤
│                           DATA LAYER                                    │
├─────────────────────────────────┼───────────────────────────────────────┤
│                                 ▼                                       │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                         API CLIENT                               │   │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐           │   │
│  │  │  URLSession  │  │   Combine    │  │    Auth      │           │   │
│  │  │  DataTask    │  │  Publishers  │  │ Interceptor  │           │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘           │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                       LOCAL STORAGE                              │   │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐           │   │
│  │  │   Keychain   │  │ UserDefaults │  │  FileManager │           │   │
│  │  │  (Tokens)    │  │  (Settings)  │  │  (Cache)     │           │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘           │   │
│  └─────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
```

### Data Flow with Combine

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         COMBINE DATA FLOW                               │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│   User Action          ViewModel            Repository       API/Cache  │
│       │                    │                    │                │      │
│       │  tap/input         │                    │                │      │
│       ├───────────────────►│                    │                │      │
│       │                    │   fetch()          │                │      │
│       │                    ├───────────────────►│                │      │
│       │                    │                    │   request()    │      │
│       │                    │                    ├───────────────►│      │
│       │                    │                    │                │      │
│       │                    │                    │◄───────────────┤      │
│       │                    │                    │  AnyPublisher  │      │
│       │                    │◄───────────────────┤                │      │
│       │                    │   Data/Error       │                │      │
│       │                    │                    │                │      │
│       │                    │ @Published         │                │      │
│       │◄───────────────────┤   state update     │                │      │
│       │   SwiftUI          │                    │                │      │
│       │   re-render        │                    │                │      │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### Navigation Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      NAVIGATION ARCHITECTURE                            │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│                        ┌─────────────────┐                              │
│                        │  AppCoordinator │                              │
│                        │  @Published     │                              │
│                        │   authState     │                              │
│                        │   path          │                              │
│                        └────────┬────────┘                              │
│                                 │                                       │
│              ┌──────────────────┼──────────────────┐                    │
│              ▼                  ▼                  ▼                    │
│     ┌────────────────┐  ┌─────────────┐  ┌────────────────┐            │
│     │ Unauthenticated│  │   Email     │  │ Authenticated  │            │
│     │    State       │  │ Confirmation│  │    State       │            │
│     └───────┬────────┘  └─────────────┘  └───────┬────────┘            │
│             │                                     │                     │
│             ▼                                     ▼                     │
│     ┌────────────────┐              ┌─────────────────────┐            │
│     │ AuthNavigation │              │    MainTabView      │            │
│     │  └─ Login      │              │  ┌─────────────────┐│            │
│     │  └─ Register   │              │  │   TabBar        ││            │
│     │  └─ ForgotPwd  │              │  │ ┌───┬───┬───┬───┤│            │
│     └────────────────┘              │  │ │ D │ O │ I │ P ││            │
│                                     │  │ │ a │ r │ n │ r ││            │
│                                     │  │ │ s │ d │ v │ o ││            │
│                                     │  │ │ h │ e │ o │ f ││            │
│                                     │  │ │   │ r │ i │ i ││            │
│                                     │  │ │   │ s │ c │ l ││            │
│                                     │  │ │   │   │ e │ e ││            │
│                                     │  │ └───┴───┴───┴───┤│            │
│                                     │  └─────────────────┘│            │
│                                     └─────────────────────┘            │
│                                                                         │
│                        NavigationStack per Tab                         │
│                        with NavigationPath                              │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Technology Stack

### Core Technologies

| Category | Technology | Version | Purpose |
|----------|-----------|---------|---------|
| **Language** | Swift | 5.9+ | Primary development language |
| **UI Framework** | SwiftUI | iOS 16+ | Declarative UI framework |
| **Reactive** | Combine | Native | Async data streams and state management |
| **Concurrency** | Swift Concurrency | Native | async/await for modern async code |
| **Architecture** | MVVM | - | Separation of concerns |

### iOS Target & Deployment

| Setting | Value |
|---------|-------|
| **Minimum iOS** | iOS 16.0 |
| **Swift Version** | 5.9 |
| **Interface** | SwiftUI |
| **Lifecycle** | SwiftUI App |
| **Bundle ID** | cz.cleansia.partner |
| **Xcode Version** | 15.0+ |

### Dependencies (Swift Package Manager)

| Package | Version | Purpose |
|---------|---------|---------|
| **KeychainAccess** | 4.2.0+ | Secure token/credentials storage |
| **Alamofire** | 5.9.0+ | HTTP networking (optional, URLSession used by default) |
| **Nuke** | 12.0.0+ | Image loading and caching |
| **Charts** | 5.0.0+ | Data visualization (Swift Charts native iOS 16+) |
| **swift-dependencies** | 1.0.0+ | Dependency injection (optional) |

### Native iOS Frameworks

| Framework | Purpose |
|-----------|---------|
| **Foundation** | Core utilities, data types, collections |
| **SwiftUI** | Declarative UI building |
| **Combine** | Reactive programming |
| **Security** | Keychain access for secure storage |
| **PhotosUI** | PHPicker for photo selection |
| **PDFKit** | PDF rendering and viewing |
| **LocalAuthentication** | Face ID / Touch ID biometrics |
| **CoreLocation** | Location services (optional) |
| **UserNotifications** | Push notifications |
| **BackgroundTasks** | Background refresh |

### Build Configurations

| Environment | Bundle ID Suffix | API URL |
|-------------|------------------|---------|
| **Development** | .dev | http://localhost:5000/api |
| **Staging** | .staging | https://staging-api.cleansia.cz/api |
| **Production** | (none) | https://api.cleansia.cz/api |

---

## Project Structure

```
CleansiaPartner/
├── CleansiaPartnerApp.swift              # App entry point
├── ContentView.swift                      # Root view with navigation
│
├── Config/
│   ├── AppConfig.swift                   # Environment configuration
│   ├── Constants.swift                   # App constants
│   └── Secrets.swift                     # API keys (gitignored)
│
├── Core/
│   ├── Network/
│   │   ├── APIClient.swift               # Network client with URLSession
│   │   ├── APIEndpoint.swift             # Endpoint definitions
│   │   ├── APIError.swift                # Error types
│   │   ├── AuthInterceptor.swift         # Token injection
│   │   ├── NetworkMonitor.swift          # Connectivity monitoring
│   │   └── RequestBuilder.swift          # URL request construction
│   │
│   ├── Storage/
│   │   ├── KeychainService.swift         # Secure credential storage
│   │   ├── UserDefaultsService.swift     # Preferences storage
│   │   └── CacheService.swift            # Local data caching
│   │
│   ├── Extensions/
│   │   ├── Date+Extensions.swift
│   │   ├── String+Extensions.swift
│   │   ├── View+Extensions.swift
│   │   ├── Color+Extensions.swift
│   │   └── Publisher+Extensions.swift
│   │
│   └── Utilities/
│       ├── Logger.swift                  # Logging utility
│       ├── Validators.swift              # Input validation
│       └── Formatters.swift              # Date/currency formatters
│
├── Domain/
│   ├── Models/
│   │   ├── Auth/
│   │   │   ├── LoginRequest.swift
│   │   │   ├── LoginResponse.swift
│   │   │   ├── RegisterRequest.swift
│   │   │   └── User.swift
│   │   │
│   │   ├── Dashboard/
│   │   │   ├── DashboardStats.swift
│   │   │   ├── EarningsAnalytics.swift
│   │   │   └── UpcomingOrder.swift
│   │   │
│   │   ├── Orders/
│   │   │   ├── Order.swift
│   │   │   ├── OrderDetail.swift
│   │   │   ├── OrderStatus.swift
│   │   │   ├── PaymentStatus.swift
│   │   │   └── ServiceItem.swift
│   │   │
│   │   ├── Invoices/
│   │   │   ├── Invoice.swift
│   │   │   ├── InvoiceDetail.swift
│   │   │   ├── InvoiceStatus.swift
│   │   │   └── OrderPay.swift
│   │   │
│   │   └── Profile/
│   │       ├── EmployeeProfile.swift
│   │       ├── EmployeeDocument.swift
│   │       ├── DocumentType.swift
│   │       └── DocumentStatus.swift
│   │
│   ├── Repositories/
│   │   ├── Protocols/
│   │   │   ├── AuthRepositoryProtocol.swift
│   │   │   ├── DashboardRepositoryProtocol.swift
│   │   │   ├── OrdersRepositoryProtocol.swift
│   │   │   ├── InvoicesRepositoryProtocol.swift
│   │   │   └── ProfileRepositoryProtocol.swift
│   │   │
│   │   └── Implementations/
│   │       ├── AuthRepository.swift
│   │       ├── DashboardRepository.swift
│   │       ├── OrdersRepository.swift
│   │       ├── InvoicesRepository.swift
│   │       └── ProfileRepository.swift
│   │
│   └── UseCases/
│       ├── Auth/
│       │   ├── LoginUseCase.swift
│       │   └── RegisterUseCase.swift
│       └── Orders/
│           └── TakeOrderUseCase.swift
│
├── Features/
│   ├── Auth/
│   │   ├── Views/
│   │   │   ├── LoginView.swift
│   │   │   ├── RegisterView.swift
│   │   │   ├── ConfirmEmailView.swift
│   │   │   └── ForgotPasswordView.swift
│   │   │
│   │   ├── ViewModels/
│   │   │   ├── LoginViewModel.swift
│   │   │   ├── RegisterViewModel.swift
│   │   │   ├── ConfirmEmailViewModel.swift
│   │   │   └── ForgotPasswordViewModel.swift
│   │   │
│   │   └── Components/
│   │       └── AuthHeaderView.swift
│   │
│   ├── Dashboard/
│   │   ├── Views/
│   │   │   └── DashboardView.swift
│   │   │
│   │   ├── ViewModels/
│   │   │   └── DashboardViewModel.swift
│   │   │
│   │   └── Components/
│   │       ├── StatsGridView.swift
│   │       ├── StatCardView.swift
│   │       ├── EarningsChartView.swift
│   │       └── UpcomingOrderCardView.swift
│   │
│   ├── Orders/
│   │   ├── Views/
│   │   │   ├── OrdersView.swift
│   │   │   └── OrderDetailsView.swift
│   │   │
│   │   ├── ViewModels/
│   │   │   ├── OrdersViewModel.swift
│   │   │   └── OrderDetailsViewModel.swift
│   │   │
│   │   └── Components/
│   │       ├── OrderCardView.swift
│   │       ├── OrderTabsView.swift
│   │       ├── OrderActionButtonsView.swift
│   │       └── OrderPhotoCaptureView.swift
│   │
│   ├── Invoices/
│   │   ├── Views/
│   │   │   ├── InvoicesView.swift
│   │   │   └── InvoiceDetailsView.swift
│   │   │
│   │   ├── ViewModels/
│   │   │   ├── InvoicesViewModel.swift
│   │   │   └── InvoiceDetailsViewModel.swift
│   │   │
│   │   └── Components/
│   │       ├── InvoiceCardView.swift
│   │       ├── InvoiceStatusBadgeView.swift
│   │       └── InvoicePDFView.swift
│   │
│   └── Profile/
│       ├── Views/
│       │   └── ProfileView.swift
│       │
│       ├── ViewModels/
│       │   └── ProfileViewModel.swift
│       │
│       └── Components/
│           ├── PersonalInfoTabView.swift
│           ├── AddressTabView.swift
│           ├── BankDetailsTabView.swift
│           ├── DocumentsTabView.swift
│           └── AvailabilityTabView.swift
│
├── Shared/
│   ├── Components/
│   │   ├── CleansiaButton.swift
│   │   ├── CleansiaTextField.swift
│   │   ├── GlassCard.swift
│   │   ├── LoadingView.swift
│   │   ├── ErrorView.swift
│   │   ├── SkeletonView.swift
│   │   ├── EmptyStateView.swift
│   │   ├── RefreshableScrollView.swift
│   │   └── StatusBadge.swift
│   │
│   ├── Styles/
│   │   ├── ButtonStyles.swift
│   │   ├── TextFieldStyles.swift
│   │   └── CardStyles.swift
│   │
│   └── Modifiers/
│       ├── ShimmerModifier.swift
│       ├── GlassmorphismModifier.swift
│       └── KeyboardAdaptive.swift
│
├── Navigation/
│   ├── AppCoordinator.swift
│   ├── Router.swift
│   ├── Route.swift
│   └── DeepLinkHandler.swift
│
├── Theme/
│   ├── CleansiaColors.swift
│   ├── CleansiaTypography.swift
│   ├── CleansiaSpacing.swift
│   └── CleansiaIcons.swift
│
├── Localization/
│   ├── en.lproj/
│   │   └── Localizable.strings
│   └── cs.lproj/
│       └── Localizable.strings
│
├── DI/
│   └── DependencyContainer.swift         # Service locator / DI container
│
├── Resources/
│   ├── Assets.xcassets/
│   │   ├── AppIcon.appiconset/
│   │   ├── Colors/
│   │   └── Images/
│   ├── Info.plist
│   └── LaunchScreen.storyboard
│
└── Tests/
    ├── UnitTests/
    │   ├── ViewModels/
    │   ├── Repositories/
    │   └── Utilities/
    │
    └── UITests/
        └── AuthFlowTests.swift
```

---

## Core Components

### 5.1 API Endpoints Enum

```swift
import Foundation

enum APIEndpoint {
    // Base URL from config
    static var baseURL: String {
        Bundle.main.object(forInfoDictionaryKey: "API_BASE_URL") as? String
            ?? "https://api.cleansia.cz/api"
    }

    // Auth
    case login
    case register
    case confirmEmail
    case resendConfirmation
    case forgotPassword
    case resetPassword

    // Dashboard
    case dashboardStats(employeeId: String?)
    case earningsAnalytics(employeeId: String?, startDate: Date?, endDate: Date?)
    case upcomingOrders(employeeId: String?, limit: Int)

    // Orders
    case getOrders(filters: OrderFilter)
    case getOrderById(orderId: String)
    case takeOrder(orderId: String)
    case startOrder(orderId: String)
    case completeOrder(orderId: String)
    case uploadPhoto(orderId: String)

    // Invoices
    case getInvoices(filters: InvoiceFilter)
    case getInvoiceById(invoiceId: String)
    case downloadInvoicePdf(invoiceId: String)

    // Profile
    case getCurrentEmployee
    case updateEmployee
    case getMyDocuments
    case saveDocuments
    case deleteDocument(documentId: String)
    case downloadDocument(documentId: String)

    var path: String {
        switch self {
        case .login: return "/Auth/Login"
        case .register: return "/Auth/RegisterEmployee"
        case .confirmEmail: return "/Auth/ConfirmUserEmail"
        case .resendConfirmation: return "/Auth/ResendConfirmationEmail"
        case .forgotPassword: return "/Auth/ForgotPassword"
        case .resetPassword: return "/Auth/ResetPassword"

        case .dashboardStats: return "/Dashboard/GetStats"
        case .earningsAnalytics: return "/Dashboard/GetEarningsAnalytics"
        case .upcomingOrders: return "/Dashboard/GetUpcomingOrders"

        case .getOrders: return "/Order/GetPaged"
        case .getOrderById: return "/Order/GetById"
        case .takeOrder: return "/Order/TakeOrder"
        case .startOrder: return "/Order/StartOrder"
        case .completeOrder: return "/Order/CompleteOrder"
        case .uploadPhoto: return "/Order/UploadPhoto"

        case .getInvoices: return "/EmployeePayroll/GetPagedInvoices"
        case .getInvoiceById(let id): return "/EmployeePayroll/GetInvoiceById/\(id)"
        case .downloadInvoicePdf(let id): return "/EmployeePayroll/DownloadInvoice/\(id)"

        case .getCurrentEmployee: return "/Employee/GetCurrentEmployee"
        case .updateEmployee: return "/Employee/UpdateEmployee"
        case .getMyDocuments: return "/Employee/GetMyDocuments"
        case .saveDocuments: return "/Employee/SaveMyDocuments"
        case .deleteDocument: return "/Employee/DeleteMyDocument"
        case .downloadDocument: return "/Employee/DownloadMyDocument"
        }
    }

    var method: HTTPMethod {
        switch self {
        case .login, .register, .confirmEmail, .resendConfirmation,
             .forgotPassword, .resetPassword, .takeOrder, .startOrder,
             .completeOrder, .uploadPhoto, .saveDocuments:
            return .post
        case .updateEmployee:
            return .put
        case .deleteDocument:
            return .delete
        default:
            return .get
        }
    }

    var requiresAuth: Bool {
        switch self {
        case .login, .register, .confirmEmail, .resendConfirmation,
             .forgotPassword, .resetPassword:
            return false
        default:
            return true
        }
    }

    var url: URL {
        URL(string: Self.baseURL + path)!
    }
}

enum HTTPMethod: String {
    case get = "GET"
    case post = "POST"
    case put = "PUT"
    case delete = "DELETE"
}
```

### 5.2 API Client with URLSession and Combine

```swift
import Foundation
import Combine

protocol APIClientProtocol {
    func request<T: Decodable>(_ endpoint: APIEndpoint, body: Encodable?) -> AnyPublisher<T, APIError>
    func request(_ endpoint: APIEndpoint, body: Encodable?) -> AnyPublisher<Data, APIError>
    func upload<T: Decodable>(_ endpoint: APIEndpoint, data: Data, filename: String, mimeType: String) -> AnyPublisher<T, APIError>
}

final class APIClient: APIClientProtocol {
    private let session: URLSession
    private let keychainService: KeychainServiceProtocol
    private let decoder: JSONDecoder
    private let encoder: JSONEncoder

    init(
        session: URLSession = .shared,
        keychainService: KeychainServiceProtocol
    ) {
        self.session = session
        self.keychainService = keychainService

        self.decoder = JSONDecoder()
        self.decoder.dateDecodingStrategy = .iso8601
        self.decoder.keyDecodingStrategy = .convertFromSnakeCase

        self.encoder = JSONEncoder()
        self.encoder.dateEncodingStrategy = .iso8601
        self.encoder.keyEncodingStrategy = .convertToSnakeCase
    }

    func request<T: Decodable>(
        _ endpoint: APIEndpoint,
        body: Encodable? = nil
    ) -> AnyPublisher<T, APIError> {
        createRequest(endpoint: endpoint, body: body)
            .flatMap { request in
                self.session.dataTaskPublisher(for: request)
                    .mapError { APIError.network($0) }
                    .flatMap { data, response -> AnyPublisher<T, APIError> in
                        guard let httpResponse = response as? HTTPURLResponse else {
                            return Fail(error: APIError.invalidResponse).eraseToAnyPublisher()
                        }

                        switch httpResponse.statusCode {
                        case 200...299:
                            return Just(data)
                                .decode(type: T.self, decoder: self.decoder)
                                .mapError { APIError.decoding($0) }
                                .eraseToAnyPublisher()
                        case 401:
                            return Fail(error: APIError.unauthorized).eraseToAnyPublisher()
                        case 400:
                            return self.parseErrorResponse(data)
                        case 404:
                            return Fail(error: APIError.notFound).eraseToAnyPublisher()
                        case 500...599:
                            return Fail(error: APIError.serverError(httpResponse.statusCode)).eraseToAnyPublisher()
                        default:
                            return Fail(error: APIError.unknown(httpResponse.statusCode)).eraseToAnyPublisher()
                        }
                    }
                    .eraseToAnyPublisher()
            }
            .eraseToAnyPublisher()
    }

    func request(_ endpoint: APIEndpoint, body: Encodable? = nil) -> AnyPublisher<Data, APIError> {
        createRequest(endpoint: endpoint, body: body)
            .flatMap { request in
                self.session.dataTaskPublisher(for: request)
                    .mapError { APIError.network($0) }
                    .tryMap { data, response -> Data in
                        guard let httpResponse = response as? HTTPURLResponse,
                              (200...299).contains(httpResponse.statusCode) else {
                            throw APIError.invalidResponse
                        }
                        return data
                    }
                    .mapError { $0 as? APIError ?? APIError.unknown(0) }
                    .eraseToAnyPublisher()
            }
            .eraseToAnyPublisher()
    }

    func upload<T: Decodable>(
        _ endpoint: APIEndpoint,
        data: Data,
        filename: String,
        mimeType: String
    ) -> AnyPublisher<T, APIError> {
        var request = URLRequest(url: endpoint.url)
        request.httpMethod = endpoint.method.rawValue

        let boundary = UUID().uuidString
        request.setValue("multipart/form-data; boundary=\(boundary)", forHTTPHeaderField: "Content-Type")

        if endpoint.requiresAuth, let token = keychainService.getToken() {
            request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        }

        var body = Data()
        body.append("--\(boundary)\r\n".data(using: .utf8)!)
        body.append("Content-Disposition: form-data; name=\"file\"; filename=\"\(filename)\"\r\n".data(using: .utf8)!)
        body.append("Content-Type: \(mimeType)\r\n\r\n".data(using: .utf8)!)
        body.append(data)
        body.append("\r\n--\(boundary)--\r\n".data(using: .utf8)!)

        request.httpBody = body

        return session.dataTaskPublisher(for: request)
            .mapError { APIError.network($0) }
            .flatMap { data, response -> AnyPublisher<T, APIError> in
                guard let httpResponse = response as? HTTPURLResponse,
                      (200...299).contains(httpResponse.statusCode) else {
                    return Fail(error: APIError.invalidResponse).eraseToAnyPublisher()
                }
                return Just(data)
                    .decode(type: T.self, decoder: self.decoder)
                    .mapError { APIError.decoding($0) }
                    .eraseToAnyPublisher()
            }
            .eraseToAnyPublisher()
    }

    private func createRequest(
        endpoint: APIEndpoint,
        body: Encodable?
    ) -> AnyPublisher<URLRequest, APIError> {
        var request = URLRequest(url: endpoint.url)
        request.httpMethod = endpoint.method.rawValue
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("application/json", forHTTPHeaderField: "Accept")

        if endpoint.requiresAuth {
            guard let token = keychainService.getToken() else {
                return Fail(error: APIError.unauthorized).eraseToAnyPublisher()
            }
            request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        }

        if let body = body {
            do {
                request.httpBody = try encoder.encode(AnyEncodable(body))
            } catch {
                return Fail(error: APIError.encoding(error)).eraseToAnyPublisher()
            }
        }

        return Just(request)
            .setFailureType(to: APIError.self)
            .eraseToAnyPublisher()
    }

    private func parseErrorResponse<T>(_ data: Data) -> AnyPublisher<T, APIError> {
        if let errorResponse = try? decoder.decode(APIErrorResponse.self, from: data) {
            return Fail(error: APIError.apiError(
                message: errorResponse.message ?? "An error occurred",
                code: errorResponse.code,
                validationErrors: errorResponse.errors
            )).eraseToAnyPublisher()
        }
        return Fail(error: APIError.badRequest("Invalid request")).eraseToAnyPublisher()
    }
}

// Helper for encoding any Encodable
struct AnyEncodable: Encodable {
    private let encode: (Encoder) throws -> Void

    init<T: Encodable>(_ wrapped: T) {
        encode = wrapped.encode
    }

    func encode(to encoder: Encoder) throws {
        try encode(encoder)
    }
}
```

### 5.3 API Error Types

```swift
import Foundation

enum APIError: LocalizedError {
    case network(Error)
    case decoding(Error)
    case encoding(Error)
    case invalidResponse
    case unauthorized
    case notFound
    case badRequest(String)
    case serverError(Int)
    case unknown(Int)
    case apiError(message: String, code: String?, validationErrors: [String: [String]]?)

    var errorDescription: String? {
        switch self {
        case .network(let error):
            return "Network error: \(error.localizedDescription)"
        case .decoding(let error):
            return "Failed to decode response: \(error.localizedDescription)"
        case .encoding(let error):
            return "Failed to encode request: \(error.localizedDescription)"
        case .invalidResponse:
            return "Invalid server response"
        case .unauthorized:
            return "Session expired. Please login again."
        case .notFound:
            return "The requested resource was not found"
        case .badRequest(let message):
            return message
        case .serverError(let code):
            return "Server error (\(code)). Please try again later."
        case .unknown(let code):
            return "An unexpected error occurred (\(code))"
        case .apiError(let message, _, _):
            return message
        }
    }

    var code: String? {
        if case .apiError(_, let code, _) = self {
            return code
        }
        return nil
    }

    var validationErrors: [String: [String]]? {
        if case .apiError(_, _, let errors) = self {
            return errors
        }
        return nil
    }
}

struct APIErrorResponse: Decodable {
    let message: String?
    let code: String?
    let title: String?
    let errors: [String: [String]]?
}
```

### 5.4 Keychain Service

```swift
import Foundation
import KeychainAccess

protocol KeychainServiceProtocol {
    func saveToken(_ token: String)
    func getToken() -> String?
    func saveUserEmail(_ email: String)
    func getUserEmail() -> String?
    func saveUserId(_ userId: String)
    func getUserId() -> String?
    func clearAll()
    func hasToken() -> Bool
}

final class KeychainService: KeychainServiceProtocol {
    private let keychain: Keychain

    private enum Keys {
        static let token = "auth_token"
        static let userEmail = "user_email"
        static let userId = "user_id"
    }

    init() {
        self.keychain = Keychain(service: Bundle.main.bundleIdentifier ?? "cz.cleansia.partner")
            .accessibility(.whenUnlockedThisDeviceOnly)
    }

    func saveToken(_ token: String) {
        try? keychain.set(token, key: Keys.token)
    }

    func getToken() -> String? {
        try? keychain.get(Keys.token)
    }

    func saveUserEmail(_ email: String) {
        try? keychain.set(email, key: Keys.userEmail)
    }

    func getUserEmail() -> String? {
        try? keychain.get(Keys.userEmail)
    }

    func saveUserId(_ userId: String) {
        try? keychain.set(userId, key: Keys.userId)
    }

    func getUserId() -> String? {
        try? keychain.get(Keys.userId)
    }

    func clearAll() {
        try? keychain.remove(Keys.token)
        try? keychain.remove(Keys.userEmail)
        try? keychain.remove(Keys.userId)
    }

    func hasToken() -> Bool {
        getToken() != nil
    }
}
```

### 5.5 Base ViewModel Protocol

```swift
import Foundation
import Combine

@MainActor
protocol ViewModelProtocol: ObservableObject {
    associatedtype State
    var state: State { get }
    var cancellables: Set<AnyCancellable> { get set }
}

@MainActor
class BaseViewModel<State>: ObservableObject {
    @Published var state: State
    var cancellables = Set<AnyCancellable>()

    init(initialState: State) {
        self.state = initialState
    }
}

// Publisher async extension
extension Publisher {
    func async() async throws -> Output {
        try await withCheckedThrowingContinuation { continuation in
            var cancellable: AnyCancellable?
            cancellable = self.first()
                .sink { completion in
                    if case .failure(let error) = completion {
                        continuation.resume(throwing: error)
                    }
                    cancellable?.cancel()
                } receiveValue: { value in
                    continuation.resume(returning: value)
                }
        }
    }
}

extension Publisher {
    func asResult() -> AnyPublisher<Result<Output, Failure>, Never> {
        self
            .map { Result.success($0) }
            .catch { Just(Result.failure($0)) }
            .eraseToAnyPublisher()
    }
}
```

### 5.6 App Coordinator

```swift
import SwiftUI
import Combine

enum AuthState {
    case unknown
    case unauthenticated
    case emailConfirmationRequired(email: String)
    case authenticated
}

@MainActor
final class AppCoordinator: ObservableObject {
    @Published var authState: AuthState = .unknown
    @Published var path = NavigationPath()
    @Published var selectedTab: Tab = .dashboard

    private let keychainService: KeychainServiceProtocol
    private let authRepository: AuthRepositoryProtocol
    private var cancellables = Set<AnyCancellable>()

    init(
        keychainService: KeychainServiceProtocol,
        authRepository: AuthRepositoryProtocol
    ) {
        self.keychainService = keychainService
        self.authRepository = authRepository

        checkAuthStatus()
    }

    func checkAuthStatus() {
        if keychainService.hasToken() {
            authState = .authenticated
        } else {
            authState = .unauthenticated
        }
    }

    func login(token: String, email: String, userId: String, isEmailConfirmed: Bool) {
        keychainService.saveToken(token)
        keychainService.saveUserEmail(email)
        keychainService.saveUserId(userId)

        if isEmailConfirmed {
            authState = .authenticated
        } else {
            authState = .emailConfirmationRequired(email: email)
        }
    }

    func confirmEmail(token: String) {
        keychainService.saveToken(token)
        authState = .authenticated
    }

    func logout() {
        keychainService.clearAll()
        authState = .unauthenticated
        path = NavigationPath()
        selectedTab = .dashboard
    }

    // Navigation helpers
    func navigateToOrderDetails(orderId: String) {
        path.append(Route.orderDetails(orderId))
    }

    func navigateToInvoiceDetails(invoiceId: String) {
        path.append(Route.invoiceDetails(invoiceId))
    }

    func navigateToOrders() {
        selectedTab = .orders
    }

    func navigateToInvoices() {
        selectedTab = .invoices
    }

    func navigateBack() {
        if !path.isEmpty {
            path.removeLast()
        }
    }
}
```

---

## Feature Specifications

### Feature Priority Matrix

| Priority | Feature | Complexity | Dependencies |
|----------|---------|------------|--------------|
| P0 - Critical | Login/Logout | Medium | API Client, Keychain |
| P0 - Critical | Dashboard View | Medium | Auth, Stats API |
| P0 - Critical | Orders List | High | Auth, Pagination |
| P0 - Critical | Order Details | Medium | Orders API |
| P1 - High | Registration | Medium | Auth API |
| P1 - High | Invoices List | Medium | Auth, Pagination |
| P1 - High | Invoice Details | Medium | Invoices API, PDFKit |
| P1 - High | Profile View | High | Employee API |
| P2 - Medium | Photo Upload | Medium | PhotosUI, File Upload |
| P2 - Medium | Document Management | High | File Upload/Download |
| P2 - Medium | Push Notifications | Medium | APNS Setup |
| P3 - Low | Biometric Auth | Low | LocalAuthentication |
| P3 - Low | Deep Linking | Medium | Universal Links |
| P3 - Low | Offline Mode | High | Core Data/SwiftData |

### Feature Details

#### 6.1 Authentication Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                     AUTHENTICATION FLOW                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────┐     ┌──────────┐     ┌──────────────┐            │
│  │  Login   │────►│  API     │────►│   Success    │            │
│  │  Screen  │     │  Call    │     │   (Token)    │            │
│  └────┬─────┘     └────┬─────┘     └──────┬───────┘            │
│       │                │                   │                    │
│       │                │                   ▼                    │
│       │                │           ┌──────────────┐            │
│       │                │           │ Email        │            │
│       │                │           │ Confirmed?   │            │
│       │                │           └──────┬───────┘            │
│       │                │                  │                     │
│       │                │       ┌──────────┴──────────┐         │
│       │                │       │                     │         │
│       │                │       ▼                     ▼         │
│       │                │  ┌─────────┐         ┌──────────┐    │
│       │                │  │  Yes    │         │   No     │    │
│       │                │  │         │         │          │    │
│       │                │  └────┬────┘         └────┬─────┘    │
│       │                │       │                   │           │
│       │                │       ▼                   ▼           │
│       │                │  ┌─────────┐       ┌───────────┐     │
│       │                │  │  Main   │       │  Confirm  │     │
│       │                │  │  App    │       │  Email    │     │
│       │                │  └─────────┘       │  Screen   │     │
│       │                │                    └───────────┘     │
│       │                │                                       │
│       ▼                ▼                                       │
│  ┌──────────┐     ┌──────────┐                                │
│  │ Register │     │  Error   │                                │
│  │  Link    │     │  Alert   │                                │
│  └──────────┘     └──────────┘                                │
│                                                                │
└─────────────────────────────────────────────────────────────────┘
```

#### 6.2 Order Workflow

```
┌─────────────────────────────────────────────────────────────────┐
│                      ORDER WORKFLOW                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│   ┌────────────┐                                                │
│   │ Available  │                                                │
│   │   Orders   │                                                │
│   └─────┬──────┘                                                │
│         │                                                        │
│         │ Take Order                                            │
│         ▼                                                        │
│   ┌────────────┐                                                │
│   │   Taken    │                                                │
│   │   (Mine)   │                                                │
│   └─────┬──────┘                                                │
│         │                                                        │
│         │ Start Order                                           │
│         ▼                                                        │
│   ┌────────────┐                                                │
│   │ In Progress│                                                │
│   │            │                                                │
│   └─────┬──────┘                                                │
│         │                                                        │
│         │ Complete Order (+ Photos)                             │
│         ▼                                                        │
│   ┌────────────┐                                                │
│   │ Completed  │                                                │
│   │            │                                                │
│   └────────────┘                                                │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## API Integration

### API Endpoints Summary

| Module | Endpoint | Method | Auth | Description |
|--------|----------|--------|------|-------------|
| **Auth** | /Auth/Login | POST | No | User login |
| | /Auth/RegisterEmployee | POST | No | Employee registration |
| | /Auth/ConfirmUserEmail | POST | No | Email confirmation |
| | /Auth/ResendConfirmationEmail | POST | No | Resend confirmation |
| | /Auth/ForgotPassword | POST | No | Password reset request |
| | /Auth/ResetPassword | POST | No | Password reset |
| **Dashboard** | /Dashboard/GetStats | GET | Yes | Dashboard statistics |
| | /Dashboard/GetEarningsAnalytics | GET | Yes | Earnings chart data |
| | /Dashboard/GetUpcomingOrders | GET | Yes | Upcoming orders list |
| **Orders** | /Order/GetPaged | GET | Yes | Paginated orders |
| | /Order/GetById | GET | Yes | Order details |
| | /Order/TakeOrder | POST | Yes | Take an order |
| | /Order/StartOrder | POST | Yes | Start an order |
| | /Order/CompleteOrder | POST | Yes | Complete an order |
| | /Order/UploadPhoto | POST | Yes | Upload completion photo |
| **Invoices** | /EmployeePayroll/GetPagedInvoices | GET | Yes | Paginated invoices |
| | /EmployeePayroll/GetInvoiceById/{id} | GET | Yes | Invoice details |
| | /EmployeePayroll/DownloadInvoice/{id} | GET | Yes | Download PDF |
| **Profile** | /Employee/GetCurrentEmployee | GET | Yes | Get profile |
| | /Employee/UpdateEmployee | PUT | Yes | Update profile |
| | /Employee/GetMyDocuments | GET | Yes | Get documents |
| | /Employee/SaveMyDocuments | POST | Yes | Upload documents |
| | /Employee/DeleteMyDocument | DELETE | Yes | Delete document |
| | /Employee/DownloadMyDocument | GET | Yes | Download document |

---

## Data Models

### 8.1 Order Status Enum

```swift
enum OrderStatus: String, Codable, CaseIterable {
    case created = "Created"
    case taken = "Taken"
    case inProgress = "InProgress"
    case completed = "Completed"
    case cancelled = "Cancelled"

    var displayName: String {
        switch self {
        case .created: return NSLocalizedString("status_created", comment: "")
        case .taken: return NSLocalizedString("status_taken", comment: "")
        case .inProgress: return NSLocalizedString("status_in_progress", comment: "")
        case .completed: return NSLocalizedString("status_completed", comment: "")
        case .cancelled: return NSLocalizedString("status_cancelled", comment: "")
        }
    }

    var color: Color {
        switch self {
        case .created: return CleansiaColors.info
        case .taken: return CleansiaColors.warning
        case .inProgress: return CleansiaColors.primary
        case .completed: return CleansiaColors.success
        case .cancelled: return CleansiaColors.error
        }
    }
}
```

### 8.2 Payment Status Enum

```swift
enum PaymentStatus: String, Codable, CaseIterable {
    case pending = "Pending"
    case paid = "Paid"
    case failed = "Failed"
    case refunded = "Refunded"

    var displayName: String {
        switch self {
        case .pending: return NSLocalizedString("payment_pending", comment: "")
        case .paid: return NSLocalizedString("payment_paid", comment: "")
        case .failed: return NSLocalizedString("payment_failed", comment: "")
        case .refunded: return NSLocalizedString("payment_refunded", comment: "")
        }
    }

    var color: Color {
        switch self {
        case .pending: return CleansiaColors.warning
        case .paid: return CleansiaColors.success
        case .failed: return CleansiaColors.error
        case .refunded: return CleansiaColors.secondary
        }
    }
}
```

### 8.3 Invoice Status Enum

```swift
enum InvoiceStatus: String, Codable, CaseIterable {
    case draft = "Draft"
    case pending = "Pending"
    case paid = "Paid"
    case overdue = "Overdue"
    case cancelled = "Cancelled"

    var displayName: String {
        switch self {
        case .draft: return NSLocalizedString("invoice_draft", comment: "")
        case .pending: return NSLocalizedString("invoice_pending", comment: "")
        case .paid: return NSLocalizedString("invoice_paid", comment: "")
        case .overdue: return NSLocalizedString("invoice_overdue", comment: "")
        case .cancelled: return NSLocalizedString("invoice_cancelled", comment: "")
        }
    }

    var color: Color {
        switch self {
        case .draft: return CleansiaColors.secondary
        case .pending: return CleansiaColors.warning
        case .paid: return CleansiaColors.success
        case .overdue: return CleansiaColors.error
        case .cancelled: return CleansiaColors.secondary
        }
    }
}
```

### 8.4 Document Type Enum

```swift
enum DocumentType: String, Codable, CaseIterable {
    case idCard = "IdCard"
    case passport = "Passport"
    case drivingLicense = "DrivingLicense"
    case workPermit = "WorkPermit"
    case residencePermit = "ResidencePermit"
    case taxDocument = "TaxDocument"
    case other = "Other"

    var displayName: String {
        switch self {
        case .idCard: return NSLocalizedString("doc_id_card", comment: "")
        case .passport: return NSLocalizedString("doc_passport", comment: "")
        case .drivingLicense: return NSLocalizedString("doc_driving_license", comment: "")
        case .workPermit: return NSLocalizedString("doc_work_permit", comment: "")
        case .residencePermit: return NSLocalizedString("doc_residence_permit", comment: "")
        case .taxDocument: return NSLocalizedString("doc_tax_document", comment: "")
        case .other: return NSLocalizedString("doc_other", comment: "")
        }
    }
}
```

---

## UI/UX Design System

### 9.1 Color Palette

```swift
import SwiftUI

enum CleansiaColors {
    // Primary - Sky Blue
    static let primary = Color(hex: "0284C7")
    static let primaryLight = Color(hex: "38BDF8")
    static let primaryDark = Color(hex: "0369A1")
    static let primaryContainer = Color(hex: "E0F2FE")

    // Secondary - Slate
    static let secondary = Color(hex: "64748B")
    static let secondaryLight = Color(hex: "94A3B8")
    static let secondaryDark = Color(hex: "475569")

    // Background
    static let background = Color(hex: "F8FAFC")
    static let surface = Color.white
    static let surfaceVariant = Color(hex: "F1F5F9")

    // Status colors
    static let success = Color(hex: "22C55E")
    static let warning = Color(hex: "F59E0B")
    static let error = Color(hex: "EF4444")
    static let info = Color(hex: "3B82F6")

    // Text
    static let onPrimary = Color.white
    static let onBackground = Color(hex: "0F172A")
    static let onBackgroundSecondary = Color(hex: "64748B")
    static let onSurface = Color(hex: "1E293B")
}

extension Color {
    init(hex: String) {
        let hex = hex.trimmingCharacters(in: CharacterSet.alphanumerics.inverted)
        var int: UInt64 = 0
        Scanner(string: hex).scanHexInt64(&int)
        let a, r, g, b: UInt64
        switch hex.count {
        case 3: // RGB (12-bit)
            (a, r, g, b) = (255, (int >> 8) * 17, (int >> 4 & 0xF) * 17, (int & 0xF) * 17)
        case 6: // RGB (24-bit)
            (a, r, g, b) = (255, int >> 16, int >> 8 & 0xFF, int & 0xFF)
        case 8: // ARGB (32-bit)
            (a, r, g, b) = (int >> 24, int >> 16 & 0xFF, int >> 8 & 0xFF, int & 0xFF)
        default:
            (a, r, g, b) = (1, 1, 1, 0)
        }
        self.init(
            .sRGB,
            red: Double(r) / 255,
            green: Double(g) / 255,
            blue: Double(b) / 255,
            opacity: Double(a) / 255
        )
    }
}
```

### 9.2 Typography

```swift
import SwiftUI

enum CleansiaTypography {
    // Headings
    static let h1 = Font.system(size: 32, weight: .bold)
    static let h2 = Font.system(size: 24, weight: .bold)
    static let h3 = Font.system(size: 20, weight: .semibold)
    static let h4 = Font.system(size: 18, weight: .semibold)

    // Body
    static let bodyLarge = Font.system(size: 16, weight: .regular)
    static let bodyMedium = Font.system(size: 14, weight: .regular)
    static let bodySmall = Font.system(size: 12, weight: .regular)

    // Labels
    static let labelLarge = Font.system(size: 14, weight: .medium)
    static let labelMedium = Font.system(size: 12, weight: .medium)
    static let labelSmall = Font.system(size: 10, weight: .medium)

    // Button
    static let button = Font.system(size: 14, weight: .semibold)

    // Caption
    static let caption = Font.system(size: 11, weight: .regular)
}
```

### 9.3 Spacing

```swift
enum CleansiaSpacing {
    static let xs: CGFloat = 4
    static let sm: CGFloat = 8
    static let md: CGFloat = 16
    static let lg: CGFloat = 24
    static let xl: CGFloat = 32
    static let xxl: CGFloat = 48

    // Component specific
    static let cardPadding: CGFloat = 16
    static let screenPadding: CGFloat = 16
    static let sectionSpacing: CGFloat = 24
    static let itemSpacing: CGFloat = 12
}
```

### 9.4 Reusable Components

```swift
// GlassCard Component
struct GlassCard<Content: View>: View {
    let content: Content

    init(@ViewBuilder content: () -> Content) {
        self.content = content()
    }

    var body: some View {
        content
            .padding(CleansiaSpacing.cardPadding)
            .background(.ultraThinMaterial)
            .clipShape(RoundedRectangle(cornerRadius: 16))
            .shadow(color: .black.opacity(0.1), radius: 10, x: 0, y: 4)
    }
}

// StatusBadge Component
struct StatusBadge: View {
    let text: String
    let color: Color

    var body: some View {
        Text(text)
            .font(CleansiaTypography.labelSmall)
            .fontWeight(.medium)
            .foregroundColor(color)
            .padding(.horizontal, 8)
            .padding(.vertical, 4)
            .background(color.opacity(0.1))
            .clipShape(Capsule())
    }
}

// CleansiaButton Component
struct CleansiaButton: View {
    let title: String
    let icon: String?
    let style: ButtonStyle
    let isLoading: Bool
    let action: () -> Void

    enum ButtonStyle {
        case primary
        case secondary
        case outlined
        case text
    }

    init(
        _ title: String,
        icon: String? = nil,
        style: ButtonStyle = .primary,
        isLoading: Bool = false,
        action: @escaping () -> Void
    ) {
        self.title = title
        self.icon = icon
        self.style = style
        self.isLoading = isLoading
        self.action = action
    }

    var body: some View {
        Button(action: action) {
            HStack(spacing: 8) {
                if isLoading {
                    ProgressView()
                        .tint(foregroundColor)
                } else if let icon = icon {
                    Image(systemName: icon)
                }
                Text(title)
            }
            .font(CleansiaTypography.button)
            .foregroundColor(foregroundColor)
            .frame(maxWidth: .infinity)
            .frame(height: 48)
            .background(backgroundColor)
            .clipShape(RoundedRectangle(cornerRadius: 12))
            .overlay(
                RoundedRectangle(cornerRadius: 12)
                    .stroke(borderColor, lineWidth: style == .outlined ? 1 : 0)
            )
        }
        .disabled(isLoading)
    }

    private var backgroundColor: Color {
        switch style {
        case .primary: return CleansiaColors.primary
        case .secondary: return CleansiaColors.secondary
        case .outlined, .text: return .clear
        }
    }

    private var foregroundColor: Color {
        switch style {
        case .primary: return CleansiaColors.onPrimary
        case .secondary: return CleansiaColors.onPrimary
        case .outlined: return CleansiaColors.primary
        case .text: return CleansiaColors.primary
        }
    }

    private var borderColor: Color {
        switch style {
        case .outlined: return CleansiaColors.primary
        default: return .clear
        }
    }
}
```

---

## Security Implementation

### Security Checklist

- [ ] **Authentication**
  - [ ] JWT token stored in Keychain (not UserDefaults)
  - [ ] Token refresh mechanism
  - [ ] Auto-logout on 401 responses
  - [ ] Secure token transmission (HTTPS only)

- [ ] **Keychain Security**
  - [ ] Use `.whenUnlockedThisDeviceOnly` accessibility
  - [ ] No token/credential logging
  - [ ] Clear credentials on logout
  - [ ] Use app-specific keychain service identifier

- [ ] **Network Security**
  - [ ] HTTPS for all API calls
  - [ ] Certificate pinning for production
  - [ ] App Transport Security (ATS) configured
  - [ ] No sensitive data in URL parameters

- [ ] **Data Protection**
  - [ ] Sensitive data encrypted at rest
  - [ ] No sensitive data in logs
  - [ ] Secure file storage for documents
  - [ ] Clear cache on logout

- [ ] **Input Validation**
  - [ ] Email format validation
  - [ ] Password strength requirements
  - [ ] Server-side validation error handling
  - [ ] Sanitize inputs before API calls

- [ ] **Biometric Security** (Optional)
  - [ ] Face ID / Touch ID integration
  - [ ] Fallback to PIN/password
  - [ ] Biometric data not stored

### Certificate Pinning Implementation

```swift
import Foundation

final class CertificatePinner: NSObject, URLSessionDelegate {
    private let pinnedCertificates: [Data]

    init(certificateNames: [String]) {
        self.pinnedCertificates = certificateNames.compactMap { name in
            guard let path = Bundle.main.path(forResource: name, ofType: "cer"),
                  let data = try? Data(contentsOf: URL(fileURLWithPath: path)) else {
                return nil
            }
            return data
        }
        super.init()
    }

    func urlSession(
        _ session: URLSession,
        didReceive challenge: URLAuthenticationChallenge,
        completionHandler: @escaping (URLSession.AuthChallengeDisposition, URLCredential?) -> Void
    ) {
        guard challenge.protectionSpace.authenticationMethod == NSURLAuthenticationMethodServerTrust,
              let serverTrust = challenge.protectionSpace.serverTrust else {
            completionHandler(.cancelAuthenticationChallenge, nil)
            return
        }

        // Get server certificate
        guard let serverCertificate = SecTrustGetCertificateAtIndex(serverTrust, 0) else {
            completionHandler(.cancelAuthenticationChallenge, nil)
            return
        }

        let serverCertificateData = SecCertificateCopyData(serverCertificate) as Data

        // Check if pinned
        if pinnedCertificates.contains(serverCertificateData) {
            completionHandler(.useCredential, URLCredential(trust: serverTrust))
        } else {
            completionHandler(.cancelAuthenticationChallenge, nil)
        }
    }
}
```

---

## Testing Strategy

### Testing Coverage Goals

| Test Type | Coverage Goal | Priority |
|-----------|---------------|----------|
| Unit Tests | 70%+ | P0 |
| ViewModel Tests | 80%+ | P0 |
| Repository Tests | 75%+ | P1 |
| UI Tests | Critical flows | P1 |
| Snapshot Tests | Key screens | P2 |
| Integration Tests | API flows | P2 |

### Unit Test Structure

```swift
import XCTest
import Combine
@testable import CleansiaPartner

class LoginViewModelTests: XCTestCase {
    var sut: LoginViewModel!
    var mockAuthRepository: MockAuthRepository!
    var cancellables: Set<AnyCancellable>!

    override func setUp() {
        super.setUp()
        mockAuthRepository = MockAuthRepository()
        sut = LoginViewModel(authRepository: mockAuthRepository)
        cancellables = []
    }

    override func tearDown() {
        sut = nil
        mockAuthRepository = nil
        cancellables = nil
        super.tearDown()
    }

    func testLoginSuccess() async {
        // Given
        mockAuthRepository.loginResult = .success(LoginResponse(
            token: "test-token",
            userId: "123",
            email: "test@example.com",
            isEmailConfirmed: true
        ))

        // When
        await sut.login(email: "test@example.com", password: "password123")

        // Then
        XCTAssertFalse(sut.state.isLoading)
        XCTAssertNil(sut.state.error)
        XCTAssertTrue(sut.state.isLoggedIn)
    }

    func testLoginFailure() async {
        // Given
        mockAuthRepository.loginResult = .failure(.unauthorized)

        // When
        await sut.login(email: "test@example.com", password: "wrong")

        // Then
        XCTAssertFalse(sut.state.isLoading)
        XCTAssertNotNil(sut.state.error)
        XCTAssertFalse(sut.state.isLoggedIn)
    }

    func testEmailValidation() {
        XCTAssertFalse(sut.isValidEmail("invalid"))
        XCTAssertFalse(sut.isValidEmail("invalid@"))
        XCTAssertTrue(sut.isValidEmail("valid@email.com"))
    }
}

// Mock Repository
class MockAuthRepository: AuthRepositoryProtocol {
    var loginResult: Result<LoginResponse, APIError> = .failure(.unknown(0))

    func login(email: String, password: String) -> AnyPublisher<LoginResponse, APIError> {
        switch loginResult {
        case .success(let response):
            return Just(response)
                .setFailureType(to: APIError.self)
                .eraseToAnyPublisher()
        case .failure(let error):
            return Fail(error: error)
                .eraseToAnyPublisher()
        }
    }
}
```

### UI Test Structure

```swift
import XCTest

class AuthFlowUITests: XCTestCase {
    var app: XCUIApplication!

    override func setUp() {
        super.setUp()
        continueAfterFailure = false
        app = XCUIApplication()
        app.launchArguments = ["UI_TESTING"]
        app.launch()
    }

    func testSuccessfulLogin() {
        // Enter credentials
        let emailField = app.textFields["email_field"]
        emailField.tap()
        emailField.typeText("test@example.com")

        let passwordField = app.secureTextFields["password_field"]
        passwordField.tap()
        passwordField.typeText("password123")

        // Tap login button
        app.buttons["login_button"].tap()

        // Verify navigation to dashboard
        XCTAssertTrue(app.navigationBars["Dashboard"].waitForExistence(timeout: 5))
    }

    func testLoginValidationErrors() {
        // Tap login without entering credentials
        app.buttons["login_button"].tap()

        // Verify error message appears
        XCTAssertTrue(app.staticTexts["validation_error"].exists)
    }
}
```

---

## Implementation Phases

### Phase 1: Project Foundation
**Timeline: Week 1**

#### 1.1 Project Setup
- [ ] Create Xcode project with SwiftUI lifecycle
- [ ] Configure project settings (bundle ID, deployment target)
- [ ] Set up build configurations (Dev/Staging/Prod)
- [ ] Configure xcconfig files for each environment
- [ ] Add Swift Package dependencies
- [ ] Create project folder structure

#### 1.2 Core Infrastructure
- [ ] Implement `AppConfig` for environment configuration
- [ ] Create `Constants.swift` with app-wide constants
- [ ] Implement `Logger` utility
- [ ] Set up `Formatters` (date, currency, number)

#### 1.3 Network Layer
- [ ] Implement `APIEndpoint` enum
- [ ] Create `HTTPMethod` enum
- [ ] Implement `APIClient` with URLSession + Combine
- [ ] Create `APIError` types
- [ ] Implement `APIErrorResponse` decoder
- [ ] Add request logging for debug builds

#### 1.4 Storage Layer
- [ ] Implement `KeychainService` protocol and class
- [ ] Create `UserDefaultsService` for preferences
- [ ] Set up Keychain accessibility settings

#### 1.5 Theme & Design System
- [ ] Create `CleansiaColors` enum
- [ ] Implement `Color+Hex` extension
- [ ] Create `CleansiaTypography` enum
- [ ] Create `CleansiaSpacing` enum
- [ ] Build base UI components:
  - [ ] `CleansiaButton`
  - [ ] `CleansiaTextField`
  - [ ] `GlassCard`
  - [ ] `LoadingView`
  - [ ] `ErrorView`
  - [ ] `StatusBadge`

---

### Phase 2: Authentication
**Timeline: Week 2**

#### 2.1 Navigation Setup
- [ ] Implement `AppCoordinator` with @Published state
- [ ] Create `AuthState` enum
- [ ] Create `Route` enum for navigation
- [ ] Implement `Tab` enum
- [ ] Set up `ContentView` with auth state switching
- [ ] Create `AuthNavigationView`
- [ ] Create `MainTabView` with NavigationStack

#### 2.2 Auth Models
- [ ] Create `LoginRequest` model
- [ ] Create `LoginResponse` model
- [ ] Create `RegisterRequest` model
- [ ] Create `User` model
- [ ] Create `ConfirmEmailRequest` model

#### 2.3 Auth Repository
- [ ] Define `AuthRepositoryProtocol`
- [ ] Implement `AuthRepository`
  - [ ] `login()` method
  - [ ] `register()` method
  - [ ] `confirmEmail()` method
  - [ ] `resendConfirmation()` method
  - [ ] `forgotPassword()` method
  - [ ] `resetPassword()` method

#### 2.4 Login Feature
- [ ] Create `LoginState` struct
- [ ] Implement `LoginViewModel`
  - [ ] Form validation
  - [ ] Login API call
  - [ ] Error handling
  - [ ] Remember me functionality
- [ ] Create `LoginView`
  - [ ] Email field
  - [ ] Password field with visibility toggle
  - [ ] Remember me checkbox
  - [ ] Login button with loading state
  - [ ] Forgot password link
  - [ ] Register link

#### 2.5 Registration Feature
- [ ] Create `RegistrationState` struct
- [ ] Implement `RegisterViewModel`
  - [ ] Form validation (email, password, confirm)
  - [ ] Registration API call
  - [ ] Error handling
- [ ] Create `RegisterView`
  - [ ] Personal info fields
  - [ ] Email field
  - [ ] Password fields
  - [ ] Terms acceptance
  - [ ] Register button
  - [ ] Login link

#### 2.6 Email Confirmation
- [ ] Create `ConfirmEmailViewModel`
- [ ] Create `ConfirmEmailView`
  - [ ] Code input fields
  - [ ] Resend code button
  - [ ] Countdown timer
  - [ ] Verify button

#### 2.7 Forgot Password
- [ ] Create `ForgotPasswordViewModel`
- [ ] Create `ForgotPasswordView`
  - [ ] Email input
  - [ ] Request reset button
  - [ ] Success message

---

### Phase 3: Dashboard
**Timeline: Week 2-3**

#### 3.1 Dashboard Models
- [ ] Create `DashboardStats` model
- [ ] Create `EarningsAnalytics` model
- [ ] Create `EarningsDataPoint` model
- [ ] Create `UpcomingOrder` model

#### 3.2 Dashboard Repository
- [ ] Define `DashboardRepositoryProtocol`
- [ ] Implement `DashboardRepository`
  - [ ] `getStats()` method
  - [ ] `getEarningsAnalytics()` method
  - [ ] `getUpcomingOrders()` method

#### 3.3 Dashboard ViewModel
- [ ] Create `DashboardState` struct
- [ ] Implement `DashboardViewModel`
  - [ ] Load all data with Publishers.Zip3
  - [ ] Pull-to-refresh with async/await
  - [ ] Error handling and retry
  - [ ] Greeting based on time of day

#### 3.4 Dashboard UI Components
- [ ] Create `StatsGridView`
  - [ ] 2x2 grid layout
  - [ ] Responsive sizing
- [ ] Create `StatCardView`
  - [ ] Icon with background
  - [ ] Value and title
  - [ ] Trend indicator
  - [ ] Tap navigation
- [ ] Create `EarningsChartView`
  - [ ] Swift Charts integration
  - [ ] Bar chart with earnings data
  - [ ] Custom styling
- [ ] Create `UpcomingOrderCardView`
  - [ ] Order summary display
  - [ ] Status badge
  - [ ] Tap to navigate

#### 3.5 Dashboard View
- [ ] Create `DashboardView`
  - [ ] Greeting header
  - [ ] Stats grid
  - [ ] Earnings chart section
  - [ ] Upcoming orders section with "See all"
  - [ ] Pull-to-refresh
  - [ ] Loading skeleton
  - [ ] Error state with retry

---

### Phase 4: Orders
**Timeline: Week 3-4**

#### 4.1 Order Models
- [ ] Create `Order` model
- [ ] Create `OrderDetail` model
- [ ] Create `OrderStatus` enum
- [ ] Create `PaymentStatus` enum
- [ ] Create `ServiceItem` model
- [ ] Create `OrderFilter` model
- [ ] Create `PagedResponse<T>` generic model

#### 4.2 Orders Repository
- [ ] Define `OrdersRepositoryProtocol`
- [ ] Implement `OrdersRepository`
  - [ ] `getOrders()` with pagination
  - [ ] `getOrderById()` method
  - [ ] `takeOrder()` method
  - [ ] `startOrder()` method
  - [ ] `completeOrder()` method
  - [ ] `uploadPhoto()` method

#### 4.3 Orders List ViewModel
- [ ] Create `OrdersState` struct
- [ ] Implement `OrdersViewModel`
  - [ ] Tab management (Available, My Orders, Completed)
  - [ ] Pagination support
  - [ ] Search/filter functionality
  - [ ] Pull-to-refresh
  - [ ] Load more on scroll

#### 4.4 Orders List UI
- [ ] Create `OrdersView`
  - [ ] Segmented control for tabs
  - [ ] Search bar
  - [ ] Order cards list
  - [ ] Empty state for each tab
  - [ ] Loading and error states
  - [ ] Pull-to-refresh
  - [ ] Infinite scroll pagination
- [ ] Create `OrderCardView`
  - [ ] Order ID and date
  - [ ] Customer/location info
  - [ ] Status badge
  - [ ] Service summary
  - [ ] Total amount
- [ ] Create `OrderTabsView`

#### 4.5 Order Details ViewModel
- [ ] Create `OrderDetailState` struct
- [ ] Implement `OrderDetailViewModel`
  - [ ] Load order details
  - [ ] Take order action
  - [ ] Start order action
  - [ ] Complete order action
  - [ ] Photo upload handling

#### 4.6 Order Details UI
- [ ] Create `OrderDetailsView`
  - [ ] Order header with status
  - [ ] Customer information section
  - [ ] Address with map link
  - [ ] Services breakdown
  - [ ] Notes section
  - [ ] Action buttons based on status
  - [ ] Photo capture for completion
- [ ] Create `OrderActionButtonsView`
  - [ ] Take/Start/Complete buttons
  - [ ] Confirmation dialogs

#### 4.7 Photo Capture
- [ ] Create `OrderPhotoCaptureView`
  - [ ] PHPicker integration
  - [ ] Camera option
  - [ ] Photo preview
  - [ ] Upload progress
  - [ ] Multiple photo support

---

### Phase 5: Invoices
**Timeline: Week 4-5**

#### 5.1 Invoice Models
- [ ] Create `Invoice` model
- [ ] Create `InvoiceDetail` model
- [ ] Create `InvoiceStatus` enum
- [ ] Create `OrderPay` model (invoice line items)
- [ ] Create `InvoiceFilter` model

#### 5.2 Invoices Repository
- [ ] Define `InvoicesRepositoryProtocol`
- [ ] Implement `InvoicesRepository`
  - [ ] `getInvoices()` with pagination
  - [ ] `getInvoiceById()` method
  - [ ] `downloadInvoicePdf()` method

#### 5.3 Invoices List ViewModel
- [ ] Create `InvoicesState` struct
- [ ] Implement `InvoicesViewModel`
  - [ ] Pagination support
  - [ ] Filter by status
  - [ ] Search functionality
  - [ ] Pull-to-refresh

#### 5.4 Invoices List UI
- [ ] Create `InvoicesView`
  - [ ] Search bar
  - [ ] Filter chips
  - [ ] Invoice cards list
  - [ ] Empty state
  - [ ] Loading skeleton
  - [ ] Pull-to-refresh
- [ ] Create `InvoiceCardView`
  - [ ] Invoice number and date
  - [ ] Period covered
  - [ ] Status badge
  - [ ] Total amount
- [ ] Create `InvoiceStatusBadgeView`

#### 5.5 Invoice Details ViewModel
- [ ] Create `InvoiceDetailState` struct
- [ ] Implement `InvoiceDetailViewModel`
  - [ ] Load invoice details
  - [ ] Download PDF action
  - [ ] Share functionality

#### 5.6 Invoice Details UI
- [ ] Create `InvoiceDetailsView`
  - [ ] Invoice header
  - [ ] Status and dates
  - [ ] Line items (orders) list
  - [ ] Totals breakdown
  - [ ] Download PDF button
  - [ ] Share button
- [ ] Create `InvoicePDFView`
  - [ ] PDFKit integration
  - [ ] Zoom and pan
  - [ ] Share sheet

---

### Phase 6: Profile
**Timeline: Week 5**

#### 6.1 Profile Models
- [ ] Create `EmployeeProfile` model
- [ ] Create `EmployeeDocument` model
- [ ] Create `DocumentType` enum
- [ ] Create `DocumentStatus` enum
- [ ] Create `BankDetails` model
- [ ] Create `Address` model

#### 6.2 Profile Repository
- [ ] Define `ProfileRepositoryProtocol`
- [ ] Implement `ProfileRepository`
  - [ ] `getCurrentEmployee()` method
  - [ ] `updateEmployee()` method
  - [ ] `getMyDocuments()` method
  - [ ] `saveDocuments()` method
  - [ ] `deleteDocument()` method
  - [ ] `downloadDocument()` method

#### 6.3 Profile ViewModel
- [ ] Create `ProfileState` struct
- [ ] Implement `ProfileViewModel`
  - [ ] Load profile data
  - [ ] Form state management
  - [ ] Save changes
  - [ ] Document upload/delete
  - [ ] Logout action

#### 6.4 Profile UI
- [ ] Create `ProfileView`
  - [ ] TabView with sections
  - [ ] Edit mode toggle
  - [ ] Save button
  - [ ] Logout button

#### 6.5 Profile Tab Components
- [ ] Create `PersonalInfoTabView`
  - [ ] Avatar with edit
  - [ ] Name fields
  - [ ] Email (read-only)
  - [ ] Phone number
  - [ ] Date of birth
- [ ] Create `AddressTabView`
  - [ ] Street address
  - [ ] City, state, zip
  - [ ] Country picker
- [ ] Create `BankDetailsTabView`
  - [ ] Bank name
  - [ ] Account number
  - [ ] IBAN
  - [ ] Swift code
- [ ] Create `DocumentsTabView`
  - [ ] Document list
  - [ ] Upload button
  - [ ] Document type picker
  - [ ] Delete confirmation
  - [ ] Download option
- [ ] Create `AvailabilityTabView`
  - [ ] Weekly schedule
  - [ ] Day toggles
  - [ ] Time pickers

---

### Phase 7: Polish & Release
**Timeline: Week 6**

#### 7.1 Localization
- [ ] Create `en.lproj/Localizable.strings`
- [ ] Create `cs.lproj/Localizable.strings`
- [ ] Implement string extension for localization
- [ ] Localize all user-facing strings
- [ ] Add plural support where needed
- [ ] Test RTL layout (if needed)

#### 7.2 Deep Linking & Universal Links
- [ ] Configure URL schemes in Info.plist
- [ ] Set up Associated Domains entitlement
- [ ] Create AASA file for server
- [ ] Implement `DeepLinkHandler`
- [ ] Handle app launch from deep link
- [ ] Test all deep link routes

#### 7.3 Push Notifications
- [ ] Configure APNs capability
- [ ] Request notification permissions
- [ ] Handle device token registration
- [ ] Implement notification handling
- [ ] Test notification navigation

#### 7.4 Error Handling & Logging
- [ ] Implement centralized error handling
- [ ] Add API error translation
- [ ] Configure release logging
- [ ] Add crash reporting (optional)
- [ ] Test offline error scenarios

#### 7.5 Performance Optimization
- [ ] Audit and optimize image loading
- [ ] Implement list view optimization
- [ ] Add skeleton loading screens
- [ ] Optimize network calls
- [ ] Profile memory usage
- [ ] Test on older devices

#### 7.6 Accessibility
- [ ] Add accessibility labels
- [ ] Implement Dynamic Type support
- [ ] Test VoiceOver navigation
- [ ] Verify color contrast ratios
- [ ] Add accessibility hints

#### 7.7 Testing
- [ ] Write unit tests for ViewModels
- [ ] Write unit tests for Repositories
- [ ] Write unit tests for utilities
- [ ] Create UI tests for auth flow
- [ ] Create UI tests for critical paths
- [ ] Achieve 70%+ code coverage

#### 7.8 App Store Preparation
- [ ] Create app icons for all sizes
- [ ] Design and export screenshots
- [ ] Write App Store description (EN/CS)
- [ ] Prepare privacy policy
- [ ] Set up support URL
- [ ] Complete App Store Connect profile
- [ ] Submit for TestFlight
- [ ] Beta testing round
- [ ] Submit for App Store review

---

## Deployment & CI/CD

### Build Commands

```bash
# Development build
xcodebuild -scheme "CleansiaPartner-Dev" \
  -configuration Debug \
  -destination 'platform=iOS Simulator,name=iPhone 15' \
  build

# Staging build
xcodebuild -scheme "CleansiaPartner-Staging" \
  -configuration Release \
  -destination 'platform=iOS Simulator,name=iPhone 15' \
  build

# Production archive
xcodebuild -scheme "CleansiaPartner" \
  -configuration Release \
  -archivePath ./build/CleansiaPartner.xcarchive \
  archive

# Export IPA
xcodebuild -exportArchive \
  -archivePath ./build/CleansiaPartner.xcarchive \
  -exportPath ./build \
  -exportOptionsPlist ExportOptions.plist

# Run tests
xcodebuild test \
  -scheme "CleansiaPartner-Dev" \
  -destination 'platform=iOS Simulator,name=iPhone 15'
```

### Fastlane Configuration (Optional)

```ruby
# Fastfile
default_platform(:ios)

platform :ios do
  desc "Run tests"
  lane :test do
    run_tests(
      scheme: "CleansiaPartner-Dev",
      devices: ["iPhone 15"]
    )
  end

  desc "Build and upload to TestFlight"
  lane :beta do
    increment_build_number
    build_app(
      scheme: "CleansiaPartner",
      configuration: "Release"
    )
    upload_to_testflight
  end

  desc "Deploy to App Store"
  lane :release do
    build_app(
      scheme: "CleansiaPartner",
      configuration: "Release"
    )
    upload_to_app_store(
      skip_metadata: false,
      skip_screenshots: false
    )
  end
end
```

### GitHub Actions CI

```yaml
name: iOS CI

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    runs-on: macos-14

    steps:
    - uses: actions/checkout@v4

    - name: Select Xcode
      run: sudo xcode-select -s /Applications/Xcode_15.0.app

    - name: Install dependencies
      run: |
        cd ios
        xcodebuild -resolvePackageDependencies \
          -scheme CleansiaPartner-Dev \
          -clonedSourcePackagesDirPath SourcePackages

    - name: Build
      run: |
        cd ios
        xcodebuild build \
          -scheme CleansiaPartner-Dev \
          -destination 'platform=iOS Simulator,name=iPhone 15' \
          -clonedSourcePackagesDirPath SourcePackages

    - name: Test
      run: |
        cd ios
        xcodebuild test \
          -scheme CleansiaPartner-Dev \
          -destination 'platform=iOS Simulator,name=iPhone 15' \
          -clonedSourcePackagesDirPath SourcePackages \
          -resultBundlePath TestResults

    - name: Upload test results
      uses: actions/upload-artifact@v3
      if: failure()
      with:
        name: test-results
        path: ios/TestResults
```

---

## Progress Tracking

### Overall Progress

| Phase | Status | Completion |
|-------|--------|------------|
| Phase 1: Foundation | Not Started | 0% |
| Phase 2: Authentication | Not Started | 0% |
| Phase 3: Dashboard | Not Started | 0% |
| Phase 4: Orders | Not Started | 0% |
| Phase 5: Invoices | Not Started | 0% |
| Phase 6: Profile | Not Started | 0% |
| Phase 7: Polish & Release | Not Started | 0% |

### Feature Completion Matrix

| Feature | Models | Repository | ViewModel | UI | Tests | Status |
|---------|--------|------------|-----------|----|----|--------|
| Login | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ | Not Started |
| Register | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ | Not Started |
| Confirm Email | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ | Not Started |
| Forgot Password | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ | Not Started |
| Dashboard | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ | Not Started |
| Orders List | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ | Not Started |
| Order Details | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ | Not Started |
| Photo Upload | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ | Not Started |
| Invoices List | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ | Not Started |
| Invoice Details | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ | Not Started |
| PDF Viewer | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ | Not Started |
| Profile View | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ | Not Started |
| Documents | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ | Not Started |

**Legend:** ⬜ Not Started | 🟨 In Progress | ✅ Completed

### Milestone Tracking

| Milestone | Target Date | Actual Date | Status |
|-----------|-------------|-------------|--------|
| Project Setup Complete | Week 1 | - | ⬜ |
| Auth Flow Working | Week 2 | - | ⬜ |
| Dashboard Functional | Week 3 | - | ⬜ |
| Orders Feature Complete | Week 4 | - | ⬜ |
| Invoices Feature Complete | Week 5 | - | ⬜ |
| Profile Feature Complete | Week 5 | - | ⬜ |
| TestFlight Beta | Week 6 | - | ⬜ |
| App Store Submission | Week 7 | - | ⬜ |
| App Store Approval | Week 8 | - | ⬜ |

---

## App Store Compliance

### App Store Submission Checklist

#### Required Assets
- [ ] App icon (1024x1024 App Store icon)
- [ ] All app icon sizes for devices
- [ ] Launch screen
- [ ] Screenshots for:
  - [ ] 6.7" iPhone (iPhone 15 Pro Max)
  - [ ] 6.5" iPhone (iPhone 11 Pro Max)
  - [ ] 5.5" iPhone (iPhone 8 Plus)
  - [ ] iPad Pro 12.9"

#### App Store Connect
- [ ] App name and subtitle
- [ ] App description (EN)
- [ ] App description (CS)
- [ ] Keywords
- [ ] Support URL
- [ ] Marketing URL (optional)
- [ ] Privacy policy URL
- [ ] Category selection
- [ ] Age rating questionnaire

#### Compliance
- [ ] Export compliance (encryption)
- [ ] IDFA usage declaration
- [ ] Privacy nutrition labels
- [ ] App privacy details
- [ ] Content rights declaration

#### Technical Requirements
- [ ] Minimum iOS version set correctly
- [ ] All required device capabilities declared
- [ ] App Transport Security configured
- [ ] Background modes declared (if used)
- [ ] Push notification entitlement (if used)
- [ ] Associated domains configured (if used)

### Privacy Requirements (Info.plist)

```xml
<!-- Camera Usage -->
<key>NSCameraUsageDescription</key>
<string>We need camera access to take photos of completed cleaning jobs</string>

<!-- Photo Library Usage -->
<key>NSPhotoLibraryUsageDescription</key>
<string>We need photo library access to select photos for order completion</string>

<!-- Location Usage (if needed) -->
<key>NSLocationWhenInUseUsageDescription</key>
<string>We need your location to show nearby cleaning jobs</string>

<!-- Face ID Usage (if biometric auth used) -->
<key>NSFaceIDUsageDescription</key>
<string>Use Face ID for quick and secure login</string>
```

### Human Interface Guidelines Compliance

- [ ] Use standard iOS navigation patterns
- [ ] Support Dynamic Type
- [ ] Support Dark Mode
- [ ] Use SF Symbols where appropriate
- [ ] Follow iOS spacing and sizing guidelines
- [ ] Implement proper keyboard handling
- [ ] Support landscape orientation (or justify portrait-only)
- [ ] Use system colors for semantic meaning
- [ ] Implement proper loading states
- [ ] Handle errors gracefully with clear messaging

---

This document provides the complete blueprint for implementing the Cleansia Partner iOS app in native Swift with SwiftUI. The architecture follows Apple's recommended patterns and ensures compliance with App Store guidelines and Human Interface Guidelines.
