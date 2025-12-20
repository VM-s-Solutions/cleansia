# Cleansia - Project Documentation

## Table of Contents

1. [Project Overview](#project-overview)
2. [Technology Stack](#technology-stack)
3. [Architecture](#architecture)
4. [Features](#features)
5. [Setup & Installation](#setup--installation)
6. [API Documentation](#api-documentation)
7. [Frontend Documentation](#frontend-documentation)
8. [Database Schema](#database-schema)
9. [Background Jobs](#background-jobs)
10. [Email System](#email-system)
11. [Payment Integration](#payment-integration)
12. [Security](#security)
13. [Deployment](#deployment)

---

## Project Overview

**Cleansia** is a comprehensive **Employee Payroll & Order Management System** designed for cleaning service businesses. It provides end-to-end management of orders, employee assignments, payroll calculations, invoice generation, and payment processing.

### Key Capabilities

- **Order Management**: Customer order creation, employee assignment, order tracking, search & filters
- **Payment Processing**: Stripe integration for card payments, cash payment support
- **Employee Payroll**: Automated pay calculations, bi-weekly pay periods, invoice generation, cancellation support
- **Photo Management**: Before/after photo uploads for service quality tracking, gallery view, download
- **Receipt & Invoice Generation**: Automated PDF generation with country-specific templates
- **Email Notifications**: SendGrid integration for transactional emails with multi-language support
- **Background Jobs**: Hangfire for scheduled tasks (period closure, reminders)
- **Multi-language Support**: Czech and English localization
- **Analytics Dashboard**: Earnings, productivity, and time tracking metrics
- **Health Monitoring**: Comprehensive health checks for database, blob storage, and external services
- **Request Logging**: Structured logging middleware for all HTTP requests
- **Mobile Responsive**: Full mobile support with responsive sidebar and touch-friendly UI
- **Dispute Management**: Order dispute tracking and resolution (backend complete)

---

## Technology Stack

### Backend

| Technology | Version | Purpose |
|------------|---------|---------|
| .NET | 8.0 | Application framework |
| C# | 12.0 | Programming language |
| Entity Framework Core | 8.0 | ORM for database access |
| PostgreSQL | Latest | Primary database |
| MediatR | Latest | CQRS pattern implementation |
| FluentValidation | Latest | Input validation |
| Hangfire | Latest | Background job processing |
| SendGrid | Latest | Email delivery |
| Stripe | Latest | Payment processing |
| Puppeteer Sharp | Latest | PDF generation |
| Azure Storage Blobs | Latest | File storage |
| Polly | Latest | Retry policies |

### Frontend

| Technology | Version | Purpose |
|------------|---------|---------|
| Angular | 17+ | Frontend framework |
| TypeScript | 5.x | Programming language |
| Nx | Latest | Monorepo tooling |
| NgRx | Latest | State management |
| RxJS | Latest | Reactive programming |
| PrimeNG | Latest | UI component library |
| @ngx-translate | Latest | Internationalization |

### DevOps

| Technology | Purpose |
|------------|---------|
| Docker | Containerization |
| Azurite | Local blob storage emulation |
| Git | Version control |

---

## Architecture

### Backend Architecture (Clean Architecture)

```
┌─────────────────────────────────────────────────────────┐
│                    Cleansia.Web                         │
│              (API Controllers, Startup)                 │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│             Cleansia.Core.AppServices                   │
│        (Features, Services, Validation)                 │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│              Cleansia.Core.Domain                       │
│         (Entities, Value Objects, Enums)                │
└─────────────────────────────────────────────────────────┘
                     ▲
                     │
┌────────────────────┴────────────────────────────────────┐
│            Cleansia.Infra.Database                      │
│         (DbContext, Repositories, Migrations)           │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│           Cleansia.Infra.Services                       │
│        (PDF, Email, Blob Storage, Templates)            │
└─────────────────────────────────────────────────────────┘
```

### Frontend Architecture (Nx Monorepo)

```
apps/
└── cleansia-partner.app/           # Partner application

libs/
├── cleansia-partner-features/      # Feature modules
│   ├── login/
│   ├── register/
│   ├── confirm-email/
│   ├── forgot-password/
│   ├── dashboard/
│   ├── orders/
│   └── invoices/
├── core/                           # Core services
│   └── services/
│       ├── client/                 # HTTP clients
│       ├── auth/                   # Authentication
│       └── dialog/                 # Dialog service
├── shared/                         # Shared components
│   ├── components/
│   │   ├── cleansia-button/       # Custom button component
│   │   ├── cleansia-sidebar-menu/ # Responsive sidebar
│   │   ├── cleansia-brand-name/   # Branding
│   │   └── ...
│   ├── pipes/
│   ├── directives/
│   └── assets/
│       └── styles/                 # Global styles
└── data-access/                    # State management
    └── stores/                     # NgRx stores
```

### Mobile Responsiveness (NEW)

#### Features
- **Responsive Sidebar**: Hamburger menu for mobile devices
- **Touch-Friendly UI**: All interactive elements optimized for touch
- **Backdrop Overlay**: Semi-transparent overlay when sidebar is open
- **Smooth Animations**: CSS transitions for sidebar slide-in/out
- **Responsive Breakpoint**: 768px width threshold

#### Sidebar Implementation

**Mobile Features** (< 768px):
- Hamburger menu button (fixed top-left position)
- Sidebar slides in from left with smooth 0.3s transition
- Backdrop overlay with fade-in animation
- Clicking backdrop or menu item closes sidebar
- Collapse/expand button hidden (not needed for mobile)
- Full touch-friendly interactions

**Desktop Features** (≥ 768px):
- Always visible sidebar
- Collapse/expand button in footer
- Smooth width transition (expanded: 16rem, collapsed: 4rem)
- Hover effects on menu items

**Implementation**:
- [cleansia-sidebar-menu.component.ts](../Cleansia.App/libs/shared/components/src/lib/cleansia-sidebar-menu/cleansia-sidebar-menu.component.ts)
- [cleansia-sidebar-menu.component.html](../Cleansia.App/libs/shared/components/src/lib/cleansia-sidebar-menu/cleansia-sidebar-menu.component.html)
- [cleansia-sidebar-menu.component.scss](../Cleansia.App/libs/shared/assets/src/styles/components/cleansia-sidebar-menu.component.scss)

#### Custom Components

**cleansia-button Component**:
- Wraps PrimeNG button with consistent styling
- Supports all PrimeNG button features
- Inputs: `severity`, `icon`, `title`, `size`, `rounded`, `disabled`, `loading`
- Custom size types: `xx-small-width`, `x-small-width`, `small-width`, `default-width`, etc.
- Used throughout the application for consistency

### CQRS Pattern

All business operations use the Command Query Responsibility Segregation pattern via MediatR:

```csharp
// Command Example
public class CreateOrder
{
    public record Command(...) : ICommand<Response>;
    public record Response(...);

    public class Validator : AbstractValidator<Command> { }
    public class Handler : ICommandHandler<Command, Response> { }
}

// Query Example
public class GetOrderDetails
{
    public record Query(...) : IQuery<Response>;
    public record Response(...);

    public class Validator : AbstractValidator<Query> { }
    public class Handler : IQueryHandler<Query, Response> { }
}
```

---

## Features

### 1. User Management

#### Registration Flow
1. User provides email, password, name, and preferred language
2. System creates user account with unconfirmed email
3. Sends confirmation email with 15-minute expiration code
4. User enters code to confirm email
5. System redirects to profile completion

#### Authentication
- **Email/Password**: Standard authentication with JWT tokens
- **Google OAuth**: Social login integration
- **Password Reset**: Email-based token system
- **Token Expiration**: 4 hours (configurable)

#### User Profiles
- Personal information (name, phone, birthdate)
- Preferred language and currency
- Profile photo upload
- Email confirmation status
- **Profile Completion Validation**: Registration lock component enforces profile completion
- **Employee Status Tracking**: NgRx store tracks employee registration status

#### Password Policy
- **Strong Password Requirements**: Minimum 12 characters, complexity requirements
- **Validation**: Backend (BaseAuthValidator) and frontend (register.models.ts) validation
- **Requirements**: At least one uppercase, lowercase, digit, and special character

### 2. Order Management

#### Order Creation
```typescript
{
  customerName: string
  customerEmail: string
  customerPhone: string
  address: {
    street: string
    city: string
    postalCode: string
    countryId: string
  }
  services: Array<{
    serviceId: string
    quantity: number
  }>
  packages: Array<{
    packageId: string
    quantity: number
  }>
  extras: Record<string, any>
  numberOfRooms: number
  numberOfBathrooms: number
  cleaningDate: Date
  paymentType: 'Card' | 'Cash'
}
```

#### Order Lifecycle

```
Created
  ↓
Pending Payment (if Card)
  ↓
Paid (payment confirmed)
  ↓
Confirmed (awaiting employee)
  ↓
Employee Takes Order
  ↓
InProgress
  ↓
Employee Completes (with notes)
  ↓
Completed (receipt generated)
```

#### Features
- Multi-service/package selection
- Dynamic pricing based on rooms/bathrooms
- Employee capacity calculation (required vs max)
- Estimated time calculation
- Distance tracking for travel compensation
- Order status tracking with audit trail
- Before/after photo uploads with gallery view
- Receipt generation and email delivery
- **Order Search & Filters**: Comprehensive filtering by name, email, phone, status, dates, prices
- **Error Handling**: Robust error handling in CreateOrder with Stripe error recovery
- **Photo Management**: Upload, view, download, and delete order photos
- **Receipt Download**: Direct download of order receipts as PDF

### 3. Payment Processing

#### Stripe Integration
- **Checkout Session**: Creates Stripe checkout for card payments
- **Webhook Handler**: Processes payment confirmation events
- **Payment Status Tracking**: Real-time payment status updates
- **Metadata**: Tracks OrderId for reconciliation

#### Payment Flow (Card)
1. User selects "Card" payment
2. Backend creates Stripe checkout session
3. Frontend redirects to Stripe checkout
4. User completes payment
5. Stripe sends webhook to `/api/payments/webhook`
6. Backend validates webhook signature
7. Updates order payment status to Paid
8. Generates and emails receipt

#### Payment Flow (Cash)
1. User selects "Cash" payment
2. Backend immediately marks payment as Paid
3. Generates and emails receipt
4. Order ready for employee assignment

### 4. Employee Payroll System

#### Pay Calculation

Each employee has `EmployeePayConfig` defining rates:

```csharp
{
  servicePayRate: decimal        // Pay per service unit
  packagePayRate: decimal        // Pay per package
  extraPayRate: decimal          // Pay per extra
  distancePayRate: decimal       // Pay per km
  minPay: decimal               // Minimum pay per order
  maxPay: decimal               // Maximum pay per order
}
```

Calculation formula:
```csharp
basePay = (services × serviceRate) + (packages × packageRate)
extrasPay = sum(extras × extraRate)
expensesPay = distance × distanceRate
totalPay = min(max(basePay + extrasPay + expensesPay, minPay), maxPay)
```

Admin can add bonuses or deductions:
```csharp
finalPay = totalPay + bonusPay - deductionPay
```

#### Pay Periods

- **Duration**: Configurable (default: bi-weekly)
- **Status**: Open → Closed → Paid
- **Auto-Close**: Background job runs daily at 2 AM UTC
- **New Period**: Automatically created when previous closes

#### Invoice Generation

**Manual Generation** (Admin):
1. Select pay period and employee
2. System finds all unpaid `OrderEmployeePay` records
3. Creates `EmployeeInvoice` with:
   - Invoice number (auto-increment)
   - Variable Symbol (hash-based for Czech banking)
   - Subtotal (sum of order pays)
   - Bonus/Deduction amounts
   - Total amount
4. Generates PDF from template
5. Uploads PDF to blob storage
6. Returns invoice

**Automatic Generation** (Background Job):
1. When period closes, iterates through all employees
2. For each employee:
   - Generates invoice (if unpaid orders exist)
   - Sends email with invoice PDF attachment
3. Creates new pay period

#### Invoice Workflow

```
Pending (newly created)
  ↓
Admin Reviews
  ↓
Approved (admin confirms amounts)
  ↓
Paid (admin records payment)
  ↓
Employee Notified

OR

Cancelled (admin cancels with reason)
```

#### Invoice Cancellation (NEW)

**Feature**: Admins can cancel invoices with reason tracking

**Implementation**:
- `EmployeeInvoice.Cancel(reason, cancelledBy)` domain method
- Business rules: Cannot cancel paid or already-cancelled invoices
- Tracks: `IsCancelled`, `CancellationReason`, `CancelledAt`, `CancelledBy`
- Status changes to `Cancelled` when cancelled
- Admin-only permission via `CanCancelInvoice` policy

**API Endpoint**: `PUT /api/employeepayroll/CancelInvoice`

**Request**:
```json
{
  "invoiceId": "invoice-id",
  "reason": "Duplicate invoice created",
  "cancelledBy": "admin-user-id"
}
```

#### Variable Symbol

For Czech bank transfers, system generates unique Variable Symbol:

```csharp
variableSymbol = Hash(employeeId) + Hash(payPeriodId)
// Example: 482951
```

This enables automatic payment reconciliation.

### 5. Dashboard & Analytics

#### Metrics
- **Dashboard Stats**: Total orders, revenue, active employees
- **Earnings Analytics**: Revenue trends over time
- **Order Distribution**: Breakdown by status, service type
- **Time Analytics**: Time spent per order, variance tracking
- **Productivity Metrics**: Orders per employee, completion rates

#### Date Range Filtering
All analytics support custom date range selection.

### 6. Photo Management

#### Features
- **Upload**: Before/after photos per order
- **Photo Types**: BeforeService, AfterService
- **Storage**: Azure Blob Storage
- **Gallery View**: Photo carousel in order details
- **Delete**: Remove unwanted photos

#### Upload Flow
1. Employee selects photos from device
2. Photos uploaded to blob storage
3. `OrderPhoto` records created with blob URLs
4. Photos associated with order
5. Displayed in order details gallery

### 7. Email System

#### Email Templates (SendGrid)

| Email Type | Trigger | Attachment |
|------------|---------|------------|
| Email Confirmation | User registration | None |
| Password Reset | Password reset request | None |
| Order Receipt | Order payment confirmed | Receipt PDF |
| Period Closed | Pay period closed | Invoice PDF |
| Period End Reminder | 3 days before period end | None |

#### Email Translations

All emails support multi-language via `EmailTemplateTranslation` table:

```sql
EmailType | Language | TemplateContent
----------|----------|----------------
PeriodClosed | en | {"Subject": "Pay Period Closed", ...}
PeriodClosed | cs | {"Subject": "Mzdové období uzavřeno", ...}
```

System merges translations with runtime data:

```csharp
var mergeData = MergeTranslationsWithData(translations, new {
    EmployeeName = "John Doe",
    PeriodLabel = "2025-01",
    StartDate = "2025-01-01",
    EndDate = "2025-01-14"
});
```

#### Retry Policy

All emails use Polly retry policy:
- 3 retry attempts
- Exponential backoff (300ms × attempt)
- Logs each retry attempt

### 8. Dispute Management (Backend Complete)

#### Overview
Comprehensive dispute tracking and resolution system for order-related issues.

#### Features
- **Dispute Creation**: Customers can create disputes for orders
- **Dispute Types**: Refund, Chargeback, ServiceQuality, BillingError, Other
- **Status Tracking**: Open, InReview, Resolved, Closed, Escalated
- **Evidence Upload**: Attach supporting documents and photos
- **Admin Resolution**: Admin-only dispute resolution workflow
- **Customer-Only Access**: Disputes restricted to customer-facing applications

#### Implementation Status
- ✅ Backend complete with full CQRS handlers
- ✅ DisputeController with HandleResult pattern
- ✅ Database seed data (8 sample disputes)
- ✅ Permission system (CustomerOnly, AdminOnly)
- ✅ Removed from partner app (not employee-facing)
- ⏳ TODO: Implement UI in Customer & Admin applications

#### API Endpoints

**Create Dispute**: `POST /api/dispute/CreateDispute`
```json
{
  "orderId": "order-id",
  "disputeType": "ServiceQuality",
  "description": "Service was not completed as agreed",
  "requestedResolution": "Partial refund of 50%"
}
```

**Get Dispute**: `GET /api/dispute/{id}`

**Update Dispute Status**: `PUT /api/dispute/UpdateStatus`
```json
{
  "disputeId": "dispute-id",
  "status": "Resolved",
  "resolutionNotes": "Refund processed"
}
```

**Add Evidence**: `POST /api/dispute/AddEvidence`
```json
{
  "disputeId": "dispute-id",
  "evidenceType": "Photo",
  "description": "Photo of incomplete work",
  "fileUrl": "https://blob.storage/evidence.jpg"
}
```

### 9. PDF Generation

#### Invoice PDFs

**Template System**:
- Country-specific templates (CZE, etc.)
- Language-specific content (en, cs)
- Stored in blob storage
- HTML templates with Handlebars syntax

**Generation Flow**:
1. Fetch invoice data (employee, orders, company info)
2. Get country invoice config (VAT rules, signature requirements)
3. Fetch template HTML from blob storage
4. Compile template with `ITemplateEngine`
5. Generate PDF via Puppeteer (Chrome headless)
6. Upload to blob storage
7. Return blob URL

**Country-Specific Rules**:
```csharp
{
  VatRequired: bool              // Include VAT calculation
  VatRate: decimal              // VAT percentage
  DigitalSignatureRequired: bool // Require digital signature
  EInvoiceFormat: string        // E-invoice format (ISDOC, etc.)
  LegalDisclaimerTemplate: string // Legal text template
}
```

#### Receipt PDFs

Similar to invoices, generates order receipts with:
- Receipt number (YYYY-SEQ format)
- Order details
- Customer information
- Services/packages breakdown
- Total amount
- Company branding

### 10. Infrastructure & Monitoring

#### Health Checks (NEW)

**Endpoint**: `GET /api/health`

**Features**:
- Database connectivity check (SQL query execution)
- Blob Storage accessibility check with response time tracking
- SendGrid configuration validation
- Stripe configuration validation
- Returns 200 OK when healthy, 503 Service Unavailable when unhealthy

**Response**:
```json
{
  "status": "Healthy",
  "checks": {
    "database": { "status": "Healthy", "responseTime": "45ms" },
    "blobStorage": { "status": "Healthy", "responseTime": "120ms" },
    "sendGrid": { "status": "Healthy" },
    "stripe": { "status": "Healthy" }
  },
  "timestamp": "2025-12-20T10:30:00Z"
}
```

**Implementation**: [HealthController.cs](../Cleansia.Web/Controllers/HealthController.cs)

#### Request Logging Middleware (NEW)

**Features**:
- Structured logging for all HTTP requests
- Logs: HTTP method, path, query string, user ID, email, IP address
- Request/response body logging (truncated for performance)
- Duration tracking (milliseconds)
- Different log levels based on status code:
  - Error (5xx)
  - Warning (4xx)
  - Info (2xx/3xx)
- Skips logging for: health checks, swagger, static files, Hangfire dashboard

**Example Log**:
```json
{
  "RequestId": "abc-123",
  "Method": "POST",
  "Path": "/api/order/CreateOrder",
  "UserId": "user-id",
  "UserEmail": "user@example.com",
  "IPAddress": "192.168.1.1",
  "Duration": 245,
  "StatusCode": 200,
  "Timestamp": "2025-12-20T10:30:00Z"
}
```

**Implementation**: [RequestLoggingMiddleware.cs](../Cleansia.Web/Middleware/RequestLoggingMiddleware.cs)

#### Performance Optimizations (NEW)

**N+1 Query Fixes**:
- Fixed `RegenerateInvoicePdf.cs` - Added `.Include(op => op.Order)`
- Verified all query handlers use proper `.Include()` statements
- Confirmed `.AsSplitQuery()` optimization in complex queries
- Reviewed 15+ query handlers across Orders, Invoices, Dashboard features

**DTO Optimization**:
- Lightweight DTOs for list endpoints: `EmployeeInvoiceDto`, `OrderListItem`
- Detailed DTOs for detail endpoints: `EmployeeInvoiceDetailDto`, `OrderItem`
- No over-fetching in paginated endpoints
- Proper separation of concerns

**Query Performance**:
- Optimized `GetPagedOrders` with comprehensive `.Include()` chains
- `GetOrderDetails` uses `.AsSplitQuery()` for complex relationships
- `GetPagedInvoices` includes only necessary related entities
- All dashboard queries optimized with specific projections

### 11. Background Jobs (Hangfire)

#### Job: Close Expired Pay Periods
- **Schedule**: Daily at 2 AM UTC
- **Purpose**: Automatically close expired pay periods
- **Workflow**:
  1. Find all open periods with `EndDate < today`
  2. For each period:
     - Set status to Closed
     - Get all active employees
     - For each employee:
       - Generate invoice (if unpaid orders exist)
       - Generate invoice PDF
       - Send email with PDF attachment
  3. Create new pay period if none active
  4. Commit all changes

#### Job: Send Period End Reminders
- **Schedule**: Daily at 9 AM UTC
- **Purpose**: Remind employees about upcoming period end
- **Workflow**:
  1. Find open period with `EndDate - today <= 3 days`
  2. Get all active employees
  3. For each employee:
     - Send reminder email
     - Include period details
     - Encourage to verify pay calculations

#### Hangfire Dashboard
- **URL**: `/hangfire`
- **Authentication**: Custom authorization filter
- **Features**: Job monitoring, manual triggering, logs

---

## Setup & Installation

### Prerequisites

- **.NET 8 SDK**
- **Node.js 18+** and **npm**
- **PostgreSQL 14+**
- **Docker** (optional, for containerization)
- **Azurite** (optional, for local blob storage)

### Backend Setup

1. **Clone Repository**
   ```bash
   git clone <repository-url>
   cd cleansia/src
   ```

2. **Configure Database**

   Update `Cleansia.Web/appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "ConnectionString": "Host=localhost;Database=cleansia;Username=postgres;Password=yourpassword"
     }
   }
   ```

3. **Run Migrations**
   ```bash
   cd Cleansia.Web
   dotnet ef database update --project ../Cleansia.Infra.Database
   ```

4. **Configure External Services**

   Update `appsettings.json`:
   ```json
   {
     "JwtSettings": {
       "Secret": "your-jwt-secret-key-at-least-32-chars",
       "TokenExpiration": "04:00:00"
     },
     "Stripe": {
       "SecretKey": "sk_test_...",
       "PublishableKey": "pk_test_...",
       "WebhookSecret": "whsec_..."
     },
     "SendGrid": {
       "ApiKey": "SG...",
       "AddressFrom": "noreply@cleansia.com",
       "EmailConfirmationTemplateId": "d-...",
       "ResetPasswordTemplateId": "d-...",
       "OrderReceiptTemplateId": "d-...",
       "PeriodClosedTemplateId": "d-...",
       "PeriodEndReminderTemplateId": "d-..."
     }
   }
   ```

5. **Run Application**
   ```bash
   dotnet run --project Cleansia.Web
   ```

   API available at: `https://localhost:7001`

### Frontend Setup

1. **Navigate to Frontend**
   ```bash
   cd cleansia/src/Cleansia.App
   ```

2. **Install Dependencies**
   ```bash
   npm install
   ```

3. **Configure API URL**

   Update `apps/cleansia-partner.app/src/environments/environment.ts`:
   ```typescript
   export const environment = {
     production: false,
     apiUrl: 'https://localhost:7001/api'
   };
   ```

4. **Run Application**
   ```bash
   npm run start:cleansia-partner
   ```

   App available at: `http://localhost:4200`

### Seed Data (Optional)

```sql
-- Run in PostgreSQL
\i Cleansia.Infra.Scripts/SeedData/insert_seed_data.sql
```

This seeds:
- Languages (Czech, English)
- Currencies (CZK, EUR, USD)
- Countries
- Initial services and packages

---

## API Documentation

### Authentication Endpoints

#### POST /api/auth/register
Register new user account.

**Request**:
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123",
  "confirmPassword": "SecurePassword123",
  "firstName": "John",
  "lastName": "Doe",
  "preferredLanguageId": "language-id"
}
```

**Response**:
```json
{
  "success": true,
  "data": {
    "userId": "user-id",
    "message": "Registration successful. Please check your email to confirm."
  }
}
```

#### POST /api/auth/login
Authenticate user and receive JWT token.

**Request**:
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123"
}
```

**Response**:
```json
{
  "success": true,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "expiresAt": "2025-01-01T12:00:00Z",
    "user": {
      "id": "user-id",
      "email": "user@example.com",
      "firstName": "John",
      "lastName": "Doe"
    }
  }
}
```

#### POST /api/auth/confirm-email
Confirm user email with verification code.

**Request**:
```json
{
  "email": "user@example.com",
  "code": "123456"
}
```

**Response**:
```json
{
  "success": true,
  "message": "Email confirmed successfully"
}
```

### Order Endpoints

#### POST /api/order/CreateOrder
Create new order.

**Request**:
```json
{
  "customerName": "Jane Smith",
  "customerEmail": "jane@example.com",
  "customerPhone": "+420123456789",
  "address": {
    "street": "Main Street 123",
    "city": "Prague",
    "postalCode": "11000",
    "countryId": "country-id"
  },
  "services": [
    { "serviceId": "service-id-1", "quantity": 2 }
  ],
  "packages": [
    { "packageId": "package-id-1", "quantity": 1 }
  ],
  "extras": {
    "windowCleaning": true,
    "carpetCleaning": false
  },
  "numberOfRooms": 3,
  "numberOfBathrooms": 2,
  "cleaningDate": "2025-01-15T09:00:00Z",
  "paymentType": "Card"
}
```

**Response (Card Payment)**:
```json
{
  "success": true,
  "data": {
    "orderId": "order-id",
    "confirmationCode": "ABC123",
    "stripeSessionId": "cs_test_..."
  }
}
```

**Response (Cash Payment)**:
```json
{
  "success": true,
  "data": {
    "orderId": "order-id",
    "confirmationCode": "ABC123",
    "receiptUrl": "https://blob.storage/receipt.pdf"
  }
}
```

#### GET /api/order/GetPaged
Get paginated list of orders.

**Query Parameters**:
- `pageNumber` (default: 1)
- `pageSize` (default: 10)
- `status` (optional): Filter by OrderStatus
- `employeeId` (optional): Filter by assigned employee
- `fromDate` (optional): Filter by date range
- `toDate` (optional): Filter by date range

**Response**:
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "order-id",
        "displayOrderNumber": "ORD-2025-001",
        "customerName": "Jane Smith",
        "cleaningDate": "2025-01-15T09:00:00Z",
        "status": "Confirmed",
        "totalPrice": 1500.00,
        "currency": "CZK"
      }
    ],
    "pageNumber": 1,
    "pageSize": 10,
    "totalCount": 50,
    "totalPages": 5
  }
}
```

#### GET /api/order/GetById/{id}
Get detailed order information.

**Response**:
```json
{
  "success": true,
  "data": {
    "id": "order-id",
    "displayOrderNumber": "ORD-2025-001",
    "customerName": "Jane Smith",
    "customerEmail": "jane@example.com",
    "customerPhone": "+420123456789",
    "address": {
      "street": "Main Street 123",
      "city": "Prague",
      "postalCode": "11000",
      "country": "Czech Republic"
    },
    "services": [...],
    "packages": [...],
    "extras": {...},
    "numberOfRooms": 3,
    "numberOfBathrooms": 2,
    "cleaningDate": "2025-01-15T09:00:00Z",
    "status": "Confirmed",
    "paymentStatus": "Paid",
    "paymentType": "Card",
    "totalPrice": 1500.00,
    "currency": "CZK",
    "assignedEmployees": [...],
    "statusHistory": [...]
  }
}
```

#### POST /api/order/TakeOrder
Employee claims order assignment.

**Request**:
```json
{
  "orderId": "order-id",
  "employeeId": "employee-id"
}
```

**Response**:
```json
{
  "success": true,
  "message": "Order successfully assigned"
}
```

#### POST /api/order/CompleteOrder
Mark order as completed.

**Request**:
```json
{
  "orderId": "order-id",
  "employeeId": "employee-id",
  "actualCompletionTime": 180,
  "notes": "All tasks completed successfully"
}
```

**Response**:
```json
{
  "success": true,
  "data": {
    "orderId": "order-id",
    "completedAt": "2025-01-15T12:30:00Z",
    "receiptUrl": "https://blob.storage/receipt.pdf"
  }
}
```

#### POST /api/order/UploadOrderPhoto (NEW)
Upload before/after photos for an order.

**Request**: Multipart form-data
- `orderId`: Order ID
- `photoType`: BeforeService | AfterService
- `file`: Image file

**Response**:
```json
{
  "success": true,
  "data": {
    "photoId": "photo-id",
    "photoUrl": "https://blob.storage/photo.jpg",
    "photoType": "BeforeService",
    "uploadedAt": "2025-12-20T10:30:00Z"
  }
}
```

#### GET /api/order/GetOrderPhotos/{orderId} (NEW)
Get all photos for an order.

**Response**:
```json
{
  "success": true,
  "data": [
    {
      "id": "photo-id",
      "photoUrl": "https://blob.storage/photo.jpg",
      "photoType": "BeforeService",
      "uploadedAt": "2025-12-20T10:30:00Z"
    }
  ]
}
```

#### DELETE /api/order/DeleteOrderPhoto/{photoId} (NEW)
Delete an order photo.

**Response**:
```json
{
  "success": true,
  "message": "Photo deleted successfully"
}
```

#### GET /api/order/DownloadOrderReceipt/{orderId} (NEW)
Download order receipt PDF.

**Response**: PDF file stream

### Payroll Endpoints

#### GET /api/employeepayroll/GetPagedInvoices
Get paginated list of invoices.

**Query Parameters**:
- `pageNumber` (default: 1)
- `pageSize` (default: 10)
- `employeeId` (optional)
- `payPeriodId` (optional)
- `status` (optional): Pending, Approved, Paid, Disputed, Rejected

**Response**:
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "invoice-id",
        "invoiceNumber": "INV-2025-001",
        "variableSymbol": "482951",
        "employeeName": "John Doe",
        "payPeriodLabel": "2025-01",
        "totalAmount": 15000.00,
        "currency": "CZK",
        "status": "Pending",
        "createdOn": "2025-01-15T00:00:00Z"
      }
    ],
    "pageNumber": 1,
    "pageSize": 10,
    "totalCount": 20,
    "totalPages": 2
  }
}
```

#### POST /api/employeepayroll/GenerateInvoice
Generate invoice for employee and pay period.

**Request**:
```json
{
  "employeeId": "employee-id",
  "payPeriodId": "period-id"
}
```

**Response**:
```json
{
  "success": true,
  "data": {
    "invoiceId": "invoice-id",
    "invoiceNumber": "INV-2025-001",
    "variableSymbol": "482951",
    "pdfUrl": "https://blob.storage/invoice.pdf"
  }
}
```

#### GET /api/employeepayroll/DownloadInvoice/{id}
Download invoice PDF.

**Response**: PDF file stream

#### PUT /api/employeepayroll/CancelInvoice (NEW)
Cancel an invoice with reason.

**Permission**: Admin only (`CanCancelInvoice`)

**Request**:
```json
{
  "invoiceId": "invoice-id",
  "reason": "Duplicate invoice created",
  "cancelledBy": "admin-user-id"
}
```

**Response**:
```json
{
  "success": true,
  "message": "Invoice cancelled successfully"
}
```

**Business Rules**:
- Cannot cancel paid invoices
- Cannot cancel already-cancelled invoices
- Sets `IsCancelled = true`, `Status = Cancelled`
- Tracks cancellation details (reason, timestamp, user)

### Dashboard Endpoints

#### GET /api/dashboard/stats
Get dashboard statistics.

**Query Parameters**:
- `fromDate` (optional)
- `toDate` (optional)

**Response**:
```json
{
  "success": true,
  "data": {
    "totalOrders": 150,
    "completedOrders": 120,
    "totalRevenue": 225000.00,
    "activeEmployees": 10,
    "averageOrderValue": 1500.00,
    "averageCompletionTime": 180
  }
}
```

### Health & Monitoring Endpoints (NEW)

#### GET /api/health
Get system health status.

**Response (Healthy)**:
```json
{
  "status": "Healthy",
  "checks": {
    "database": {
      "status": "Healthy",
      "description": "Database connection successful"
    },
    "blobStorage": {
      "status": "Healthy",
      "responseTime": "120ms",
      "description": "Blob storage accessible"
    },
    "sendGrid": {
      "status": "Healthy",
      "description": "SendGrid configuration valid"
    },
    "stripe": {
      "status": "Healthy",
      "description": "Stripe configuration valid"
    }
  },
  "timestamp": "2025-12-20T10:30:00Z"
}
```

**Response (Unhealthy)**: Status code 503
```json
{
  "status": "Unhealthy",
  "checks": {
    "database": {
      "status": "Unhealthy",
      "error": "Connection timeout",
      "description": "Database connection failed"
    },
    "blobStorage": { "status": "Healthy" },
    "sendGrid": { "status": "Healthy" },
    "stripe": { "status": "Healthy" }
  },
  "timestamp": "2025-12-20T10:30:00Z"
}
```

---

## Frontend Documentation

### Routing

| Route | Component | Auth Required | Description |
|-------|-----------|---------------|-------------|
| `/login` | LoginComponent | No | User login |
| `/register` | RegisterComponent | No | User registration |
| `/confirm-email` | ConfirmEmailComponent | No | Email confirmation |
| `/forgot-password` | ForgotPasswordComponent | No | Password reset |
| `/dashboard` | DashboardComponent | Yes | Analytics dashboard |
| `/profile` | ProfileComponent | Yes | Employee profile |
| `/orders` | OrdersComponent | Yes | Order list |
| `/orders/:id` | OrderDetailsComponent | Yes | Order details |
| `/invoices` | InvoicesComponent | Yes | Invoice list |
| `/invoices/:id` | InvoiceDetailComponent | Yes | Invoice details |

### State Management (NgRx)

#### Auth State
```typescript
interface AuthState {
  token: string | null;
  isLoggedIn: boolean;
  user: UserDto | null;
  loading: boolean;
  error: string | null;
}

// Actions
login(email, password)
logout()
setToken(token)
clearAuth()

// Selectors
selectIsLoggedIn
selectCurrentUser
selectAuthToken
selectAuthError
```

#### Employee State
```typescript
interface EmployeeState {
  current: EmployeeDto | null;
  isRegistrationComplete: boolean;
  loading: boolean;
  error: string | null;
}

// Actions
loadCurrentEmployee()
updateEmployee(data)
setRegistrationComplete(status)

// Selectors
selectCurrentEmployee
selectIsRegistrationComplete
selectEmployeeLoading
```

#### Order State
```typescript
interface OrderState {
  list: OrderListItem[];
  selected: OrderDetailDto | null;
  paging: {
    pageNumber: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
  };
  loading: boolean;
  error: string | null;
}

// Actions
loadOrders(params)
loadOrderDetails(id)
takeOrder(orderId, employeeId)
completeOrder(data)
uploadPhoto(data)

// Selectors
selectOrderList
selectSelectedOrder
selectOrderPaging
selectOrderLoading
```

### Services

#### AuthService
```typescript
class AuthService {
  login(email: string, password: string): Observable<LoginResponse>
  logout(): void
  isLoggedIn$: Observable<boolean>
  currentUser$: Observable<UserDto | null>
  getToken(): string | null
  setToken(token: string): void
  clearToken(): void
}
```

#### OrderClient
```typescript
class OrderClient {
  getById(orderId: string): Observable<OrderDetailDto>
  getPagedOrders(params: OrderQueryParams): Observable<PagedResponse<OrderListItem>>
  takeOrder(orderId: string, employeeId: string): Observable<void>
  completeOrder(data: CompleteOrderDto): Observable<void>
  downloadReceipt(orderId: string): Observable<Blob>
  uploadPhoto(data: UploadPhotoDto): Observable<PhotoDto>
  getPhotos(orderId: string): Observable<PhotoDto[]>
  deletePhoto(photoId: string): Observable<void>
}
```

### Components

#### Dashboard Component
```typescript
@Component({
  selector: 'app-dashboard',
  template: `
    <app-date-range-selector (rangeChange)="onDateRangeChange($event)" />
    <app-stats-cards [stats]="stats()" />
    <app-earnings-chart [data]="earnings()" />
    <app-order-distribution-chart [data]="orders()" />
    <app-time-analytics-chart [data]="time()" />
    <app-productivity-gauges [data]="productivity()" />
  `
})
export class DashboardComponent {
  stats = signal<DashboardStats | null>(null);
  earnings = signal<EarningsData[]>([]);
  orders = signal<OrderDistributionData | null>(null);
  time = signal<TimeAnalyticsData[]>([]);
  productivity = signal<ProductivityMetrics | null>(null);

  constructor(private facade: DashboardFacade) {
    this.facade.loadStats(this.dateRange());
  }

  onDateRangeChange(range: DateRange) {
    this.facade.loadStats(range);
  }
}
```

#### Order Details Component
```typescript
@Component({
  selector: 'app-order-details',
  template: `
    <app-order-header [order]="order()" />
    <p-tabView>
      <p-tabPanel header="Details">
        <app-order-customer-info [order]="order()" />
        <app-order-service-details [order]="order()" />
        <app-order-payment-info [order]="order()" />
      </p-tabPanel>
      <p-tabPanel header="Photos">
        <app-order-photos [orderId]="orderId()" />
      </p-tabPanel>
      <p-tabPanel header="History">
        <app-order-status-history [history]="order()?.statusHistory" />
      </p-tabPanel>
    </p-tabView>
    <div class="actions">
      <button (click)="takeOrder()" *ngIf="canTakeOrder()">Take Order</button>
      <button (click)="completeOrder()" *ngIf="canCompleteOrder()">Complete</button>
      <button (click)="downloadReceipt()">Download Receipt</button>
    </div>
  `
})
export class OrderDetailsComponent {
  order = signal<OrderDetailDto | null>(null);
  orderId = input.required<string>();

  constructor(private facade: OrderDetailsFacade) {
    effect(() => {
      this.facade.loadOrder(this.orderId());
    });
  }

  takeOrder() {
    this.facade.takeOrder(this.orderId());
  }

  completeOrder() {
    // Show dialog, collect completion data
    this.facade.completeOrder(data);
  }

  downloadReceipt() {
    this.facade.downloadReceipt(this.orderId());
  }
}
```

---

## Database Schema

### Core Tables

#### Users
```sql
CREATE TABLE users (
  id UUID PRIMARY KEY,
  email VARCHAR(255) NOT NULL UNIQUE,
  first_name VARCHAR(100) NOT NULL,
  last_name VARCHAR(100) NOT NULL,
  phone_number VARCHAR(50),
  birthdate DATE,
  is_email_confirmed BOOLEAN DEFAULT FALSE,
  email_confirmation_code VARCHAR(10),
  email_confirmation_expiration TIMESTAMP,
  password_hash VARCHAR(255),
  password_reset_token VARCHAR(255),
  password_reset_expiration TIMESTAMP,
  preferred_language_id UUID,
  preferred_currency_id UUID,
  authentication_type VARCHAR(50),
  google_id VARCHAR(255),
  profile_photo_url TEXT,
  created_on TIMESTAMP DEFAULT NOW(),
  created_by VARCHAR(255),
  updated_on TIMESTAMP,
  updated_by VARCHAR(255)
);
```

#### Employees
```sql
CREATE TABLE employees (
  id UUID PRIMARY KEY,
  user_id UUID NOT NULL REFERENCES users(id),
  ico VARCHAR(50),
  iban VARCHAR(50),
  nationality VARCHAR(100),
  passport_number VARCHAR(50),
  address_id UUID REFERENCES addresses(id),
  emergency_contact_name VARCHAR(255),
  emergency_contact_phone VARCHAR(50),
  is_active BOOLEAN DEFAULT TRUE,
  contract_status VARCHAR(50),
  hire_date DATE,
  preferred_currency_code VARCHAR(10),
  created_on TIMESTAMP DEFAULT NOW(),
  created_by VARCHAR(255),
  updated_on TIMESTAMP,
  updated_by VARCHAR(255)
);
```

#### Orders
```sql
CREATE TABLE orders (
  id UUID PRIMARY KEY,
  display_order_number VARCHAR(50) NOT NULL UNIQUE,
  confirmation_code VARCHAR(50),
  customer_name VARCHAR(255) NOT NULL,
  customer_email VARCHAR(255) NOT NULL,
  customer_phone VARCHAR(50),
  address_id UUID REFERENCES addresses(id),
  cleaning_date TIMESTAMP NOT NULL,
  estimated_time_minutes INT,
  actual_completion_time_minutes INT,
  number_of_rooms INT NOT NULL,
  number_of_bathrooms INT NOT NULL,
  extras JSONB,
  distance_km DECIMAL(10,2),
  status VARCHAR(50) NOT NULL,
  payment_type VARCHAR(50) NOT NULL,
  payment_status VARCHAR(50) NOT NULL,
  stripe_session_id VARCHAR(255),
  stripe_payment_intent_id VARCHAR(255),
  total_price DECIMAL(18,2) NOT NULL,
  currency_id UUID REFERENCES currencies(id),
  required_employee_count INT,
  max_employee_count INT,
  completion_notes TEXT,
  created_on TIMESTAMP DEFAULT NOW(),
  created_by VARCHAR(255),
  updated_on TIMESTAMP,
  updated_by VARCHAR(255)
);
```

#### Pay Periods
```sql
CREATE TABLE pay_periods (
  id UUID PRIMARY KEY,
  period_label VARCHAR(50) NOT NULL,
  start_date DATE NOT NULL,
  end_date DATE NOT NULL,
  status VARCHAR(50) NOT NULL,
  closed_by VARCHAR(255),
  closed_at TIMESTAMP,
  closed_note TEXT,
  created_on TIMESTAMP DEFAULT NOW(),
  created_by VARCHAR(255),
  updated_on TIMESTAMP,
  updated_by VARCHAR(255)
);
```

#### Employee Invoices
```sql
CREATE TABLE employee_invoices (
  id UUID PRIMARY KEY,
  invoice_number VARCHAR(50) NOT NULL UNIQUE,
  variable_symbol VARCHAR(20) NOT NULL,
  employee_id UUID NOT NULL REFERENCES employees(id),
  pay_period_id UUID NOT NULL REFERENCES pay_periods(id),
  order_count INT NOT NULL,
  sub_total DECIMAL(18,2) NOT NULL,
  currency_id UUID REFERENCES currencies(id),
  bonus_amount DECIMAL(18,2) DEFAULT 0,
  deduction_amount DECIMAL(18,2) DEFAULT 0,
  total_amount DECIMAL(18,2) NOT NULL,
  status VARCHAR(50) NOT NULL,  -- Pending, Approved, Paid, Disputed, Rejected, Cancelled
  pdf_blob_url TEXT,
  approved_by VARCHAR(255),
  approved_at TIMESTAMP,
  paid_by VARCHAR(255),
  paid_at TIMESTAMP,
  notes TEXT,
  -- NEW: Cancellation fields
  is_cancelled BOOLEAN DEFAULT FALSE,
  cancellation_reason VARCHAR(1000),
  cancelled_at TIMESTAMP,
  cancelled_by VARCHAR(255),
  created_on TIMESTAMP DEFAULT NOW(),
  created_by VARCHAR(255),
  updated_on TIMESTAMP,
  updated_by VARCHAR(255)
);
```

#### Order Employee Pay
```sql
CREATE TABLE order_employee_pays (
  id UUID PRIMARY KEY,
  order_id UUID NOT NULL REFERENCES orders(id),
  employee_id UUID NOT NULL REFERENCES employees(id),
  pay_period_id UUID REFERENCES pay_periods(id),
  employee_invoice_id UUID REFERENCES employee_invoices(id),
  base_pay DECIMAL(18,2) NOT NULL,
  extras_pay DECIMAL(18,2) DEFAULT 0,
  expenses_pay DECIMAL(18,2) DEFAULT 0,
  bonus_pay DECIMAL(18,2) DEFAULT 0,
  deduction_pay DECIMAL(18,2) DEFAULT 0,
  total_pay DECIMAL(18,2) NOT NULL,
  currency_id UUID REFERENCES currencies(id),
  created_on TIMESTAMP DEFAULT NOW(),
  created_by VARCHAR(255),
  updated_on TIMESTAMP,
  updated_by VARCHAR(255)
);
```

### Supporting Tables

- **Addresses**: Customer and employee addresses
- **Services**: Cleaning services catalog
- **Packages**: Service bundles
- **Order Services**: Many-to-many order-service relationship
- **Order Packages**: Many-to-many order-package relationship
- **Order Employees**: Employee assignments to orders
- **Order Status Tracks**: Audit trail of status changes
- **Order Photos**: Before/after photos (NEW: with PhotoType enum - BeforeService, AfterService)
- **Order Receipts**: Receipt tracking and PDF generation
- **Employee Pay Configs**: Employee pay rates
- **Languages**: Supported languages
- **Currencies**: Supported currencies with exchange rates
- **Countries**: Countries with configurations
- **Company Info**: Company details for invoices and receipts
- **Invoice Templates**: PDF templates per country/language
- **Receipt Templates**: Receipt PDF templates
- **Country Invoice Configs**: Country-specific invoice rules (VAT, digital signatures)
- **Email Template Translations**: Multi-language email templates (NEW: 5 email types)
- **Disputes** (NEW): Order dispute tracking
  - Fields: `dispute_type`, `status`, `description`, `requested_resolution`
  - Types: Refund, Chargeback, ServiceQuality, BillingError, Other
  - Status: Open, InReview, Resolved, Closed, Escalated
- **Dispute Evidence** (NEW): Supporting documents/photos for disputes

---

## Background Jobs

### Configuration

Jobs configured in `HangfireConfiguration.cs`:

```csharp
public static void ConfigureRecurringJobs()
{
    // Close expired pay periods - runs daily at 2 AM UTC
    RecurringJob.AddOrUpdate<IPayPeriodBackgroundService>(
        "close-expired-pay-periods",
        service => service.CloseExpiredPeriodsAndOpenNewAsync(CancellationToken.None),
        Cron.Daily(2),
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc }
    );

    // Send period end reminders - runs daily at 9 AM UTC
    RecurringJob.AddOrUpdate<IPeriodReminderBackgroundService>(
        "send-period-end-reminders",
        service => service.SendPeriodEndRemindersAsync(CancellationToken.None),
        Cron.Daily(9),
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc }
    );
}
```

### Monitoring

Hangfire Dashboard available at `/hangfire` with:
- Real-time job status
- Failed job retry
- Manual job triggering
- Execution history
- Performance metrics

---

## Email System

### Template Variables

#### Email Confirmation
```javascript
{
  UserName: string,
  VerificationCode: string,
  Subject: string,
  GreetingText: string,
  InstructionText: string,
  ButtonText: string,
  FooterText: string
}
```

#### Password Reset
```javascript
{
  UserName: string,
  ResetPasswordLink: string,
  VerificationCode: string,
  Subject: string,
  // ... other translations
}
```

#### Order Receipt
```javascript
{
  CustomerName: string,
  OrderNumber: string,
  OrderDate: string,
  TotalAmount: string,
  OrderStatusLink: string,
  Subject: string,
  // ... other translations
}
```

#### Period Closed
```javascript
{
  EmployeeName: string,
  PeriodLabel: string,
  StartDate: string,
  EndDate: string,
  ClosedAt: string,
  Subject: string,
  // ... other translations
}
```

#### Period End Reminder
```javascript
{
  EmployeeName: string,
  PeriodLabel: string,
  StartDate: string,
  EndDate: string,
  DaysRemaining: string,
  CountdownTitle: string,
  Subject: string,
  // ... other translations
}
```

---

## Payment Integration

### Stripe Setup

1. **Create Stripe Account** at https://stripe.com

2. **Get API Keys** from Dashboard

3. **Configure Webhook**:
   - URL: `https://yourdomain.com/api/payments/webhook`
   - Events: `checkout.session.completed`

4. **Update appsettings.json**:
   ```json
   {
     "Stripe": {
       "SecretKey": "sk_test_...",
       "PublishableKey": "pk_test_...",
       "WebhookSecret": "whsec_...",
       "SuccessUrl": "https://cleansia.com/orders/success",
       "CancelUrl": "https://cleansia.com/orders/cancel"
     }
   }
   ```

### Payment Flow

1. **Create Checkout Session**:
   ```csharp
   var options = new SessionCreateOptions
   {
       PaymentMethodTypes = new List<string> { "card" },
       LineItems = new List<SessionLineItemOptions>
       {
           new SessionLineItemOptions
           {
               PriceData = new SessionLineItemPriceDataOptions
               {
                   Currency = "czk",
                   ProductData = new SessionLineItemPriceDataProductDataOptions
                   {
                       Name = "Cleaning Service",
                       Description = order.Description
                   },
                   UnitAmount = (long)(order.TotalPrice * 100)
               },
               Quantity = 1
           }
       },
       Mode = "payment",
       SuccessUrl = stripeConfig.SuccessUrl,
       CancelUrl = stripeConfig.CancelUrl,
       Metadata = new Dictionary<string, string>
       {
           { "OrderId", order.Id }
       }
   };

   var session = await sessionService.CreateAsync(options);
   ```

2. **Frontend Redirects to Stripe**:
   ```typescript
   window.location.href = session.url;
   ```

3. **Webhook Handler**:
   ```csharp
   [HttpPost("webhook")]
   public async Task<IActionResult> Webhook()
   {
       var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
       var signature = Request.Headers["Stripe-Signature"];

       var @event = EventUtility.ConstructEvent(
           json,
           signature,
           webhookSecret
       );

       if (@event.Type == Events.CheckoutSessionCompleted)
       {
           var session = @event.Data.Object as Session;
           var orderId = session.Metadata["OrderId"];

           // Update order payment status
           // Generate receipt
           // Send email
       }

       return Ok();
   }
   ```

---

## Security

### Authentication

- **JWT Tokens**: 4-hour expiration
- **Password Hashing**: bcrypt with salt
- **Email Confirmation**: Required for new accounts
- **Password Reset**: Time-limited tokens

### Authorization

Policy-based access control:

```csharp
[Permission(Policy.CanViewPagedOrder)]
public async Task<IActionResult> GetPagedOrders()
{
    // Only Admin and Employee can access
}

[Permission(Policy.CanMarkInvoicePaid)]
public async Task<IActionResult> MarkInvoicePaid()
{
    // Only Admin can access
}
```

### Data Protection

- **HTTPS**: Enforced in production
- **CORS**: Configured for specific origins
- **Input Validation**: FluentValidation on all inputs
- **SQL Injection**: Prevented via EF Core parameterization
- **XSS**: Angular sanitizes all output
- **CSRF**: Not applicable (stateless API)

### Secrets Management

**Development**:
- User Secrets (dotnet user-secrets)
- Environment variables

**Production**:
- Azure Key Vault
- Environment variables
- Kubernetes secrets

---

## Deployment

### Docker Deployment

1. **Build Backend Image**:
   ```dockerfile
   FROM mcr.microsoft.com/dotnet/aspnet:8.0
   WORKDIR /app
   COPY publish/ .
   ENTRYPOINT ["dotnet", "Cleansia.Web.dll"]
   ```

2. **Build Frontend Image**:
   ```dockerfile
   FROM node:18 as build
   WORKDIR /app
   COPY . .
   RUN npm install && npm run build:cleansia-partner

   FROM nginx:alpine
   COPY --from=build /app/dist/apps/cleansia-partner.app /usr/share/nginx/html
   COPY nginx.conf /etc/nginx/nginx.conf
   ```

3. **Docker Compose**:
   ```yaml
   version: '3.8'
   services:
     db:
       image: postgres:14
       environment:
         POSTGRES_DB: cleansia
         POSTGRES_PASSWORD: ${DB_PASSWORD}
       volumes:
         - postgres_data:/var/lib/postgresql/data

     api:
       build: ./src/Cleansia.Web
       ports:
         - "5000:80"
       environment:
         ConnectionStrings__ConnectionString: ${DB_CONNECTION}
         JwtSettings__Secret: ${JWT_SECRET}
         Stripe__SecretKey: ${STRIPE_SECRET}
         SendGrid__ApiKey: ${SENDGRID_KEY}
       depends_on:
         - db

     web:
       build: ./src/Cleansia.App
       ports:
         - "80:80"
       depends_on:
         - api

   volumes:
     postgres_data:
   ```

### Azure Deployment

1. **App Service** for API
2. **Static Web App** for frontend
3. **Azure SQL Database** for PostgreSQL
4. **Azure Blob Storage** for files
5. **Application Insights** for monitoring
6. **Key Vault** for secrets

### Environment Variables

```bash
# Database
ConnectionStrings__ConnectionString=Host=...;Database=...;Username=...;Password=...

# JWT
JwtSettings__Secret=your-secret-key-at-least-32-chars
JwtSettings__TokenExpiration=04:00:00

# Stripe
Stripe__SecretKey=sk_...
Stripe__PublishableKey=pk_...
Stripe__WebhookSecret=whsec_...

# SendGrid
SendGrid__ApiKey=SG...
SendGrid__AddressFrom=noreply@cleansia.com

# Blob Storage
ConnectionStrings__BlobContainerConfigurationConnectionString=DefaultEndpointsProtocol=https;...

# Hangfire
Hangfire__DashboardUsername=admin
Hangfire__DashboardPassword=...
```

---

## Maintenance & Monitoring

### Logging

Structured logging with Serilog:

```csharp
Log.Information("Order {OrderId} created by {CustomerEmail}", orderId, email);
Log.Warning("Invoice generation failed for employee {EmployeeId}", employeeId);
Log.Error(ex, "Payment webhook processing failed");
```

### Monitoring

- **Hangfire Dashboard**: Job execution monitoring
- **Application Insights**: Performance, exceptions, dependencies
- **PostgreSQL Logs**: Query performance, errors
- **Blob Storage Metrics**: Upload/download stats

### Health Checks

```csharp
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

Checks:
- Database connectivity
- Blob storage availability
- SendGrid API
- Stripe API

---

## Support & Contact

For issues or questions:
- **Email**: support@cleansia.com
- **GitHub Issues**: [Repository URL]
- **Documentation**: [Wiki URL]

---

## License

[Your License Here]

---

## Changelog

### Version 1.1.0 (2025-12-20) - Partner App Complete

#### New Features

**Invoice Cancellation**:
- Added invoice cancellation functionality with reason tracking
- Domain model: `EmployeeInvoice.Cancel(reason, cancelledBy)`
- Business rules: Cannot cancel paid or already-cancelled invoices
- New fields: `IsCancelled`, `CancellationReason`, `CancelledAt`, `CancelledBy`
- Status enum: Added `Cancelled = 5`
- API endpoint: `PUT /api/employeepayroll/CancelInvoice`
- Permission: Admin-only via `CanCancelInvoice` policy

**Order Photo Management**:
- Upload before/after photos for orders
- Photo gallery component with carousel view
- Download individual photos
- Delete photos
- API endpoints: `UploadOrderPhoto`, `GetOrderPhotos`, `DeleteOrderPhoto`
- PhotoType enum: BeforeService, AfterService

**Order Receipt Download**:
- Direct download of order receipts as PDF
- API endpoint: `GET /api/order/DownloadOrderReceipt/{orderId}`
- Receipt service for PDF generation and retrieval

**Dispute Management (Backend)**:
- Complete dispute tracking system
- Dispute types: Refund, Chargeback, ServiceQuality, BillingError, Other
- Status tracking: Open, InReview, Resolved, Closed, Escalated
- Evidence upload support
- Customer-only and Admin-only permissions
- Database seed data with 8 sample disputes
- DisputeController with HandleResult pattern
- ⏳ Frontend UI pending for Customer & Admin apps

**Health Monitoring**:
- Comprehensive health check endpoint: `GET /api/health`
- Database connectivity check
- Blob Storage accessibility check with response time
- SendGrid configuration validation
- Stripe configuration validation
- Returns 200 OK (healthy) or 503 (unhealthy)

**Request Logging**:
- Structured logging middleware for all HTTP requests
- Logs: method, path, query string, user ID, email, IP address
- Request/response body logging (truncated)
- Duration tracking in milliseconds
- Log levels based on status code (Error 5xx, Warning 4xx, Info 2xx)
- Skips: health checks, swagger, static files, Hangfire

**Mobile Responsiveness**:
- Fully responsive sidebar with hamburger menu
- Touch-friendly UI for all devices
- Backdrop overlay for mobile sidebar
- Smooth animations (0.3s transitions)
- Responsive breakpoint: 768px
- Hidden collapse button on mobile

**Order Search & Filters**:
- Comprehensive filtering by: name, email, phone, status, dates, prices
- OrderFilter model with full filter support
- GetPagedOrders handler with filter integration

#### Improvements

**Performance Optimizations**:
- Fixed N+1 query in `RegenerateInvoicePdf.cs` (added `.Include(op => op.Order)`)
- Verified all query handlers use proper `.Include()` statements
- Confirmed `.AsSplitQuery()` optimization in complex queries
- Reviewed 15+ query handlers across Orders, Invoices, Dashboard

**DTO Optimization**:
- Lightweight DTOs for list endpoints: `EmployeeInvoiceDto`, `OrderListItem`
- Detailed DTOs for detail endpoints: `EmployeeInvoiceDetailDto`, `OrderItem`
- No over-fetching in paginated endpoints
- Proper separation of concerns

**Error Handling**:
- Robust error handling in CreateOrder with Stripe error recovery
- Invoice PDF generation error handling
- Payment webhook error handling with idempotency

**Security**:
- Strong password policy: 12+ chars, complexity requirements
- Profile completion validation enforcement
- RegistrationCompletionService implemented
- CleansiaRegistrationLockComponent for incomplete profiles
- NgRx store with employee status checking

**Components**:
- Custom `cleansia-button` component for consistency
- Replaces PrimeNG button with standardized API
- Supports all button features: severity, icon, size, rounded, etc.
- Custom size types for better control

#### Database Changes

**employee_invoices table**:
- Added `is_cancelled` BOOLEAN DEFAULT FALSE
- Added `cancellation_reason` VARCHAR(1000)
- Added `cancelled_at` TIMESTAMP
- Added `cancelled_by` VARCHAR(255)
- Updated `status` enum to include Cancelled

**New Tables**:
- `disputes`: Order dispute tracking
- `dispute_evidence`: Supporting documents/photos
- `order_photos`: Before/after photos with PhotoType
- `email_template_translations`: Multi-language email templates

#### Bug Fixes

- Fixed sidebar not displaying on mobile devices (<768px)
- Fixed N+1 query performance issue in invoice PDF generation
- Fixed dispute seed data (8 disputes properly inserted)
- Fixed permission system for disputes (CustomerOnly/AdminOnly)

#### Documentation

- Updated CLEANSIA_PROJECT_DOCUMENTATION.md with all new features
- Updated PROJECT_STATUS.md to 100% complete
- Added comprehensive API documentation for new endpoints
- Added mobile responsiveness section
- Added infrastructure & monitoring section

### Version 1.0.0 (2025-01-15) - Initial Release

#### Core Features

- User authentication (Email/Password + Google OAuth)
- Employee registration with email confirmation
- Order management (creation, tracking, completion)
- Payment processing (Stripe + Cash)
- Employee payroll calculations
- Invoice generation with PDFs
- Email system (SendGrid)
- Background jobs (Hangfire)
- Analytics dashboard
- Multi-language support (Czech/English)

---

**Last Updated**: 2025-12-20
**Version**: 1.1.0
