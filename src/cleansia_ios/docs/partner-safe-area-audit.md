# Partner safe-area audit — 2026-09-16

This is a source audit of every partner route, tab, sheet and full-screen cover, plus the shared
containers they use. It includes loading, error, empty and loaded branches. Source review establishes
where content can escape its viewport; it does not establish rendered behavior on a device.

## Findings and changes

| Surface | Finding | Change |
|---|---|---|
| ProfileHubContent | The entire ScrollView ignored the top safe area; padding protected only the initial hero | Keep the ScrollView inside its safe viewport, remove manual top-inset plumbing, extend only the hero/background |
| OrderDetailView / shared SnapSheet | The edge-following mascot extended above the safe viewport at expanded/dragged positions | Clip its full-height overlay to the safe viewport while preserving its position relative to the sheet and controls |
| ApproximateAreaBackdrop | A legend centered from physical screen top could overlap the status bar when the sheet was expanded | Publish the measured safe top from SnapSheet; fit the whole legend between that boundary and the sheet, falling back to the decorative background when it cannot fit |

No guessed status-bar height or hardcoded sheet anchor is used. The map backdrop remains full bleed.
`ViewThatFits` is available on iOS 16 and selects a child by the proposed space; see [Apple's documentation](https://developer.apple.com/documentation/swiftui/viewthatfits).

## Verification and remaining device checks

`ContentSafeAreaBindingTests` guards the actual profile source and the approximate legend's safe strip
and fitting fallback. `SnapSheetOrnamentTests` guards the production ornament's clipped viewport; its
anchor/offset tests remain. Reintroducing the original layout must fail the corresponding guard.
These are source regression tests, not screenshot or geometry tests.

SwiftFormat 0.60.1 passed per touched file. CI passed for `b5470030`, including strict SwiftLint,
Core's 720 tests and the partner's 856 tests. XCTest and SwiftLint were not run locally on Windows.
The simulator/device acceptance matrix below remains pending.

Runtime checks remain: iOS 16 notched iPhone and a Dynamic Island iPhone; portrait and landscape;
default and larger Dynamic Type; profile scrolled past the hero; map/approximate-map order detail at
each anchor and during dragging; loading/error/empty states; registration lock and all profile
sections; address-picker header; camera and PDF presentation. Confirm that visible controls remain
below the top safe boundary and that the approximate legend appears only when it fits fully.

## Screen coverage

Partner:
- Root/splash/auth/onboarding: PartnerRootView, SplashGateView incl unreachable/skeleton, OnboardingView incl language/Skip header, LoginView, RegisterView, ForgotPasswordView, ConfirmEmailView safe. RegistrationLockView owns its safe NavigationStack and routes to the same section forms.
- Dashboard: DashboardView/content/skeleton/error, greeting/notification bell, pending-offer shortcuts, notification sheet and job-radius modal safe. Only background expands.
- Jobs: OrdersRootView/OrdersListView with available/active/history panes, PendingOffersView and decline/refusal overlays safe. OrderDetailView normal content safe, with the shared ornament and approximate legend repaired above. Checklist, scope, customer/payment, timer, notes/issues, photos and action footer inherit the safe sheet content. TextEntrySheet safe NavigationStack/form. Camera/library sheet uses UIKit-owned controls.
- Earnings: EarningsView, PeriodPayView, InvoicesListView, InvoiceDetailView and their loading/error/empty/content branches safe. Invoice PDF uses UIKit QuickLook.
- Profile: ProfileHubContent is repaired above. PersonalSectionView, AddressSectionView, EmergencySectionView, IdentificationSectionView, BankSectionView, DocumentsSectionView, JobRadiusSectionView use SectionScaffold safe ScrollViews or safe custom content. AddressPickerView extends only the map sibling, keeping top search/back controls in a safe VStack. LanguagePickerView, ThemePickerView, DevicesView, DeleteAccountView safe. ProfileAvatarField camera/library modal is UIKit-owned.

Shared containers: SnapSheet geometry/content are safe with the ornament now clipped at the safe viewport; backdrop extension is intentional. CleansiaDialog ignores only the scrim. WordmarkSplashView ignores only its gradient. GlobalSnackbarHost is bottom-aligned; no top escape. CameraOrLibraryPicker returns UIImagePickerController and QuickLookPreview returns QLPreviewController with no overridden safeAreaInsets, edgesForExtendedLayout, or custom top controls; iOS owns the navigation/camera chrome. MapKitMapProvider is the intentional edge-to-edge media layer.

## View-file inventory

| File | View declarations | Source classification |
|---|---|---|
| CleansiaPartner/Sources/Features/Auth/ConfirmEmailView.swift | ConfirmEmailView, ConfirmEmailContent | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/Auth/ForgotPasswordView.swift | ForgotPasswordView, ForgotPasswordContent | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/Auth/LoginView.swift | LoginView, LoginContent, PreviewStateWrapper | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/Auth/RegisterView.swift | RegisterView, RegisterContent | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/Dashboard/DashboardCards.swift | WeeklyEarningsCard, PayPeriodCard, LastMonthCard, MetricColumn, RatingColumn, MonthDeltaChip, IconHalo | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Dashboard/DashboardView.swift | DashboardView, DashboardErrorView, DashboardContent, GreetingBar, NotificationBell, HeroCard, HeroRowCard | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/Dashboard/ShortcutsSection.swift | ShortcutsSection, ShortcutTile | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Devices/DevicesView.swift | DevicesView, DevicesContent, DeviceCard, CurrentDeviceChip, DevicesErrorState | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/Earnings/EarningsContent.swift | EarningsContent, HeadlineEarningsCard, BreakdownGrid, BreakdownRow, PayPeriodCardView, InvoicesEntryCard, EarningsDivider | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Earnings/EarningsView.swift | EarningsView, EarningsErrorView | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/Earnings/InvoiceDetailContent.swift | InvoiceDetailContent, HeroCard, BreakdownCard, PeriodCard, ReferencesCard, NotesCard, NoteBlock, MoneyRow, DateRow, CopyableField | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Earnings/InvoiceDetailView.swift | InvoiceDetailView, InvoiceDetailErrorView | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/Earnings/InvoicesListContent.swift | InvoicesListContent, InvoicesSummaryCard, InvoiceCard, MetaLabel | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Earnings/InvoicesListView.swift | InvoicesListView, InvoicesErrorView | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/Earnings/InvoiceStatusBadge.swift | InvoiceStatusBadge | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Earnings/PeriodPayContent.swift | PeriodPayContent, HeroCard, BreakdownCard, JobsCard, JobRow, MoneyRow | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Earnings/PeriodPayView.swift | PeriodPayView, PeriodPayErrorView | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/Notifications/NotificationsInboxSheet.swift | NotificationsInboxSheet, NotificationsInboxList, NotificationFeedRowCard, NotificationsInboxErrorView, NotificationsInboxEmptyView | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/Onboarding/OnboardingView.swift | OnboardingView, OnboardingContent, OnboardingLanguageMenu, OnboardingPageView, PageIndicator | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/Orders/CleaningChecklistView.swift | CleaningChecklistView, ChecklistRow | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Orders/NotesAndIssuesSection.swift | NotesAndIssuesSection, EntryRow, TextEntrySheet | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/Orders/OrderDetailCards.swift | OrderSectionCard, AccessCard, CustomerCard, ContactChip, ScopeCard, ScopeLine, FromCustomerNotesCard, PaymentCard, PaymentStatusPill, CopyInstructionButton | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Orders/OrderDetailContent.swift | OrderDetailContent, OrderDetailCompactHeader, OrderStatusPill, OrderTrackerHero, OrderMetadataRow | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Orders/OrderDetailView.swift | OrderDetailView, ApproximateAreaBackdrop, OrderDetailErrorView | Safe content; ornament clipping and backdrop legend fitting repaired as described above |
| CleansiaPartner/Sources/Features/Orders/OrdersListComponents.swift | InProgressBanner, OrdersSearchField, OrderChipsRow, ScopeChip, DecisionBadge, CompactOrderRow, CompactOrderRowContent, OrdersEmptyState | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Orders/OrdersListContent.swift | OrdersPaneView, AvailablePane, LocationPromptRow, AvailableSummaryRow, AvailableOrderRow, TakeButton, ActivePane, ActiveOrderRow, HistoryPane, PeriodFilterRow, HistorySummaryRow, SummaryStat | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Orders/OrdersListView.swift | OrdersRootView, OrdersListView, OrdersErrorView | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/Orders/OrderTimerCard.swift | OrderTimerCard | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Orders/PendingOfferComponents.swift | ReservedForYouRow, OfferRefusalDialog, OfferDeclineDialog | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Orders/PendingOffersCard.swift | PendingOffersCard, PendingOffersCardContent | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Orders/PendingOffersView.swift | PendingOffersView, PendingOffersContent, OffersErrorView, PendingOfferCard | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/Orders/PhotosSection.swift | PhotosSection, PhotoRailsContent, PhotoRail, AddPhotoTile, PhotoTile | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/Orders/StatusTimelineView.swift | StatusTimelineView, TimelineRow | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Orders/StickyActionFooter.swift | StickyActionFooter, CompleteBlockedHint | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Profile/Address/AddressSectionView.swift | AddressSectionView, AddressSummaryCard, ServiceAreaRow, WhyWeNeedThisCard, WhyRow | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Profile/AddressPicker/AddressPickerView.swift | AddressPickerView, CenterPin, FloatingCircleButton, SearchField, SearchDropdown, SearchStateRow, SearchResultRow, ConfirmCard | Map-only full bleed; separate safe controls |
| CleansiaPartner/Sources/Features/Profile/Bank/BankSectionView.swift | BankSectionView, BankFormFields, BankFormFieldsPreviewHost | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Profile/DeleteAccountView.swift | DeleteAccountView | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/Profile/Documents/DocumentsSectionView.swift | DocumentsSectionView, DocumentsErrorState, DocumentRow, RequirementsCard | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Profile/Emergency/EmergencySectionView.swift | EmergencySectionView | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Profile/Identification/IdentificationSectionView.swift | IdentificationSectionView, EntityTypePicker | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Profile/JobRadius/JobRadiusControl.swift | JobRadiusControl | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Profile/JobRadius/JobRadiusPromptCard.swift | JobRadiusPromptCard | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Profile/JobRadius/JobRadiusSectionView.swift | JobRadiusSectionView | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Profile/LanguagePickerView.swift | LanguagePickerView, ThemePickerView, PreferencePickerList, PreferenceRow | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/Profile/OnboardingChainHeader.swift | OnboardingChainHeader, StepPill, StepDot | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Profile/Personal/PersonalSectionView.swift | PersonalSectionView | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Profile/ProfileAvatarField.swift | ProfileAvatarField, PendingAvatarBar | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/Profile/ProfileHubContent.swift | ProfileHubContent, ProfileHero, ContractStatusChip, ProfileSectionRow, DeleteAccountRow, LogoutRow | Repaired: profile content stays inside the safe viewport |
| CleansiaPartner/Sources/Features/Profile/ProfileView.swift | ProfileView, ErrorContent | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/Profile/SaveSectionButton.swift | SaveSectionButton | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Profile/SectionScaffold.swift | SectionScaffold, SectionErrorRetry | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/RegistrationLock/RegistrationLockView.swift | RegistrationLockView, RegistrationLockContent, LanguageRow, LockHero, ProgressBanner, ErrorBanner, StepRow, SignOutButton | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/Shell/PartnerShellView.swift | PartnerShellView | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/Features/Shell/PlaceholderTabView.swift | PlaceholderDestination | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaPartner/Sources/Features/Splash/SplashGateView.swift | SplashGateView, RegistrationLockSkeleton, SplashUnreachableView | Inherits safe host; no direct top-content escape |
| CleansiaPartner/Sources/PartnerRootView.swift | PartnerRootView | Inherits safe host; no direct top-content escape |
