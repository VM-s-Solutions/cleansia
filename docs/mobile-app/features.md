# Mobile App Features

This page covers the key features of the Cleansia Partner Android app and how they map to the codebase.

## Order Management

The order flow is the core of the app, enabling employees to discover, claim, execute, and complete cleaning orders.

::: info Source Files
- Screens: `src/cleansia_android/.../features/orders/screens/`
- ViewModels: `src/cleansia_android/.../features/orders/viewmodels/`
- Components: `src/cleansia_android/.../features/orders/components/`
:::

### Order List

The `OrdersScreen` displays orders in two view modes (toggled via `ViewModeToggle`):

- **List view** -- standard paginated list with filters
- **Week strip view** -- calendar-based weekly view (`WeekStripView`)

Filters are managed by `OrderFilterManager` and include status, date range, and urgency (`OrderUrgency` component).

### Order Details

`OrderDetailsScreen` shows full order information including:

- Customer contact info (`CustomerContactCard`)
- Order info sections: services, packages, address, date, price (`OrderInfoSections`)
- Workflow progress stepper (`WorkflowStepper`)
- Quick info card with key metrics (`QuickInfoCard`)

### Order Workflow (Swipe-to-Confirm)

The order lifecycle is driven through three action buttons with confirmation UX:

| Action | Transition | Component |
|--------|-----------|-----------|
| **Take Order** | Confirmed -> Taken | `OrderActionButtons` |
| **Start Order** | Taken -> InProgress | `OrderActionButtons` |
| **Complete Order** | InProgress -> Completed | `OrderActionButtons` |

Each action uses a swipe-to-confirm pattern via `OrderBottomSheets` to prevent accidental taps. The `OrderDetailsViewModel` orchestrates the API calls.

```
[Confirmed] --Take--> [Taken] --Start--> [InProgress] --Complete--> [Completed]
```

## Before/After Photos

Employees document their work with categorized photos (before and after cleaning).

::: info Source Files
- `src/cleansia_android/.../features/orders/components/BeforeAfterPhotoSection.kt`
- `src/cleansia_android/.../features/orders/components/PhotoGallery.kt`
- `src/cleansia_android/.../features/orders/components/photo/PhotoCategorySection.kt`
- `src/cleansia_android/.../features/orders/components/photo/PhotoDialogs.kt`
- `src/cleansia_android/.../features/orders/viewmodels/OrderPhotoManager.kt`
:::

### Photo Flow

1. Employee opens the photo section in order details
2. Selects category: **Before** or **After**
3. Takes photo with camera or selects from gallery
4. Photo is uploaded via `UploadPhoto` API endpoint
5. Photos can be viewed in a gallery (`PhotoGallery`) and deleted

The `OrderPhotoManager` manages photo state, upload progress, and handles errors. Photos are displayed in categorized sections (`PhotoCategorySection`) with full-screen preview and deletion dialogs (`PhotoDialogs`).

## Order Timer

A foreground service tracks the duration of active cleaning orders.

::: info Source Files
- Service: `src/cleansia_android/.../core/notifications/OrderTimerService.kt`
- Manager: `src/cleansia_android/.../features/orders/viewmodels/OrderTimerManager.kt`
- UI: `src/cleansia_android/.../features/orders/components/TimerSection.kt`
:::

### How It Works

1. When an employee taps **Start Order**, the `OrderTimerService` foreground service starts
2. The service shows a persistent notification with elapsed time
3. The `TimerSection` component displays the timer in the order details screen
4. `OrderTimerManager` coordinates between the service and the ViewModel
5. When the employee taps **Complete Order**, the timer stops

The service uses `FOREGROUND_SERVICE_SPECIAL_USE` permission and runs as a `specialUse` foreground service type, ensuring it persists even when the app is backgrounded.

::: tip
The foreground service is required on Android 14+ to keep the timer running when the app is in the background. The notification keeps the user informed and prevents the system from killing the process.
:::

## Report Issue / Add Note

Employees can add notes or report issues during order execution via `OrderBottomSheets`:

- **Add Note** -- free-text note visible to admins and the assigned employee
- **Report Issue** -- structured issue report for problems encountered during cleaning

The `EmployeeNotesIssuesSection` component displays existing notes and issues on the order details screen.

## Dashboard Analytics

The dashboard is the app's home screen, showing key metrics and performance data.

::: info Source Files
- Screen: `src/cleansia_android/.../features/dashboard/screens/`
- ViewModel: `src/cleansia_android/.../features/dashboard/viewmodels/`
- Analytics components: `src/cleansia_android/.../features/dashboard/components/analytics/`
:::

The dashboard displays:

- Active/upcoming orders count
- Completed orders summary
- Earnings overview
- Performance analytics charts

Data is fetched from the `DashboardRepository` and displayed using custom analytics components.

## Invoices

Employees can view their pay period invoices.

::: info Source Files
- Screens: `src/cleansia_android/.../features/invoices/screens/`
- ViewModel: `src/cleansia_android/.../features/invoices/viewmodels/`
- Components: `src/cleansia_android/.../features/invoices/components/`
:::

Features:
- Paginated invoice list with status indicators
- Invoice detail view with line items
- Cached locally via Room (`CachedInvoice` entity) for offline viewing

## Authentication

::: info Source Files
- Screens: `src/cleansia_android/.../features/auth/screens/`
- ViewModel: `src/cleansia_android/.../features/auth/viewmodels/`
- Components: `src/cleansia_android/.../features/auth/components/`
:::

- Email/password login
- Employee registration
- Email confirmation (with deep link support)
- Forgot password flow
- Session expiration detection (401 triggers automatic logout with dialog)

## Onboarding

First-launch onboarding flow with Lottie animations.

::: info Source Files
- Screens: `src/cleansia_android/.../features/onboarding/screens/`
- ViewModel: `src/cleansia_android/.../features/onboarding/viewmodels/`
:::

## Profile Management

::: info Source Files
- Screens: `src/cleansia_android/.../features/profile/screens/`
- Domain model: `src/cleansia_android/.../domain/models/profile/`
:::

- View and edit employee profile
- Profile data cached via Room (`CachedProfile`)

## Other Features

| Feature | Location | Description |
|---------|----------|-------------|
| Account management | `features/account/` | Account settings, delete account |
| Search | `features/search/` | Global search across orders |
| Settings | `features/settings/` | App preferences, language, theme |
| Notifications | `features/notifications/` | Push notification handling |
