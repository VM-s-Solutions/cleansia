# Customer safe-area audit — 2026-09-16

This is a source audit of every customer route, tab, sheet and full-screen cover, plus the shared
containers they use. It includes loading, error, empty and loaded branches. Source review establishes
where content can escape its viewport; it does not establish rendered behavior on a device.

## Findings and changes

| Surface | Finding | Change |
|---|---|---|
| ProfileTab | The entire ScrollView ignored the top safe area; padding protected only the initial hero | Keep the ScrollView inside its safe viewport, remove manual top-inset plumbing, extend only the hero/background |
| SubscribePlusScreen | Offer content could scroll into the status-bar area; reduced states used separate manual inset handling | Safe viewport for every state, background-only extension, no duplicate content inset |
| OrderDetailView / shared SnapSheet | The edge-following mascot extended above the safe viewport at expanded/dragged positions | Clip its full-height overlay to the safe viewport while preserving its position relative to the sheet and controls |

The map backdrop remains full bleed. No navigation flow, generated API member, text or membership
behavior changes. The decision follows SwiftUI's documented [safe-area expansion](https://developer.apple.com/documentation/swiftui/view/ignoressafearea(_:edges:)) and [clipping](https://developer.apple.com/documentation/swiftui/view/clipped(antialiased:)) behavior.

## Verification and remaining device checks

`ContentSafeAreaBindingTests` checks the actual profile and Plus sources after removing only explicitly
allowed decorative background extensions. `SnapSheetOrnamentTests` guards the production ornament's
clipped viewport; its anchor/offset tests remain. Reintroducing the original content escape must fail
the corresponding guard. These are source regression tests, not screenshot or geometry tests.

SwiftFormat 0.60.1 passed per touched file. XCTest and SwiftLint are unrun locally on Windows; CI is
pending for this change. No local simulator, device or Mac result is claimed.

Runtime checks remain: iOS 16 notched iPhone and a Dynamic Island iPhone; portrait and landscape;
default and larger Dynamic Type; profile scrolled past the hero; all Plus loading/error/empty/offer
states; expanded and dragged order sheet; full-screen photos and their close button; booking and
address-picker headers; camera and PDF presentation. Confirm that visible controls remain below the
top safe boundary and that clipping does not hide a required control.

## Screen coverage

Customer:
- Root/splash/auth: CustomerRootView, SplashGateView, SignInView, SignUpView, ForgotPasswordView, EmailVerifyView, ProfileOnboardingView. CenteredAuthScroll uses safe GeometryReader/ScrollView; only colors/gradients ignore edges. Busy/authenticating overlays ignore only their dimming background; centered message controls remain safe.
- Main tabs: HomeTab (address top bar, market chip, notifications trigger), OrdersTab (fixed heading plus list states), RewardsTab (including loading/error/empty), RewardsActivityScreen: ordinary safe-area containers; background-only escape. ProfileTab is repaired above.
- Booking modal: BookingSheetView/BookingSheetContent and all Services/WhenWhere/Confirm steps; fixed header and footer are in a safe VStack, scrollable step bodies inherit it. BookingSuccessView keeps safe content. PackageDetailsSheet, PromoCodeSheet, ReferralCodeSheet/CodeSheetShell, PreferredCleanerSheet use safe content roots and background-only escape.
- Address flows: AddressManagerView list/review/add, BookingSavedAddressChooserView, BookingAddressReviewPane, BookingAddressPickerView. The chooser is a fullScreenCover; mapContent ignores only the map sibling, while its VStack/topBar stays safe. The map's center pin is intentional map decoration. No entire overlay-root escape.
- Membership/recurring: MembershipManagementCard and MembershipSuccessScreen safe; SubscribePlusScreen is repaired above. RecurringBookingsScreen including Plus/lapsed gate, create/edit CreateRecurringScreen, its address-manager sheet are safe roots.
- Orders: OrderDetailView loading/error and inner compact-header/scroll/footer safe; The shared SnapSheet ornament is repaired above. CancelOrderSheet and SubmitReviewSheet are safe modal roots with background-only escapes. OrderPhotosScreen ordinary safe gallery; FullscreenPager extends only image pager/background, with its close button a separate safe sibling. Dot pager display is media chrome near the bottom, outside this top-status-bar task.
- Disputes: DisputesListView, CreateDisputeView, DisputeDetailView/thread/reply/footer safe. FullscreenSingleImage's black background extends, close button is a safe sibling. Camera/library and QuickLook sheets are UIKit-owned controllers, audited separately below.
- Profile/settings: EditProfileView, CustomerDevicesView, NotificationsView, SecurityView, LanguagePickerView, MarketPickerView, AppearancePickerView, HelpSupportView, DeleteAccountView safe. Bottom-inset delete CTA does not alter top safety. Avatar camera/library presentation is UIKit-owned.

Shared containers: SnapSheet geometry/content are safe with the ornament now clipped at the safe viewport; backdrop extension is intentional. CleansiaDialog ignores only the scrim. WordmarkSplashView ignores only its gradient. GlobalSnackbarHost is bottom-aligned; no top escape. CameraOrLibraryPicker returns UIImagePickerController and QuickLookPreview returns QLPreviewController with no overridden safeAreaInsets, edgesForExtendedLayout, or custom top controls; iOS owns the navigation/camera chrome. MapKitMapProvider is the intentional edge-to-edge media layer.

## View-file inventory

| File | View declarations | Source classification |
|---|---|---|
| CleansiaCustomer/Sources/Components/BusyMascotOverlay.swift | BusyMascotOverlay | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/CustomerRootView.swift | CustomerRootView | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Addresses/AddressManagerView.swift | AddressManagerView, AddressManagerHeader, AddressListPane, RenameAlertButtons, SavedAddressRow, AddressReviewPane | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Auth/AuthHeaderImage.swift | AuthHeaderImage | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Auth/CenteredAuthScroll.swift | CenteredAuthScroll | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Auth/EmailVerifyView.swift | EmailVerifyView, EmailVerifyContent | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Auth/ForgotPasswordView.swift | ForgotPasswordView, ForgotPasswordContent | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Auth/SignInView.swift | SignInView, SignInContent | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Auth/SignUpView.swift | SignUpView, SignUpContent | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Auth/Social/AppleIDButton.swift | AppleIDButton | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Auth/Social/AuthAuthenticatingOverlay.swift | AuthAuthenticatingOverlay | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Auth/Social/SocialSignInSection.swift | SocialSignInSection, GoogleSignInButton | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Booking/BookingSheetView.swift | BookingSheetView, BookingSheetContent | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Booking/BookingSuccessView.swift | BookingSuccessView | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Booking/Confirm/CancellationPolicyCard.swift | CancellationPolicyCard, PolicyTier, TrustBadges, TrustBadge | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Booking/Confirm/CodeSheetShell.swift | CodeSheetShell | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Booking/Confirm/ConfirmExtrasComponents.swift | InstructionsField, PreferredCleanerPicker, PreferredCleanerSheet, CleanerRow | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Booking/Confirm/ConfirmStep.swift | ConfirmStep | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Booking/Confirm/ConfirmStepComponents.swift | ExtrasCard, ExtraRow, SummaryCard, AmountRow, LabeledInfoRow, PaymentOption, CodeEntryRow | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Booking/Confirm/PromoCodeSheet.swift | PromoCodeSheet | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Booking/Confirm/ReferralCodeSheet.swift | ReferralCodeSheet | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Booking/Steps/PackageDetailsSheet.swift | PackageDetailsSheet, IncludedServiceRow | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Booking/Steps/ServicesStep.swift | ServicesStep, CatalogContentView, PropertyRow, CatalogContentPreview | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Booking/Steps/ServicesStepComponents.swift | PropertyStepper, CategoryChip, ServiceRow, PackageCard, SelectionBadge, SectionHeader, EmptyResults, CatalogMessageView | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Booking/WhenWhere/AddressPicker/BookingAddressPickerView.swift | BookingAddressPickerView, CenterPin, FloatingCircleButton, SearchField, SearchDropdown, SearchStateRow, SearchResultRow, ConfirmCard | Map-only full bleed; separate safe controls |
| CleansiaCustomer/Sources/Features/Booking/WhenWhere/AddressPicker/BookingAddressReviewPane.swift | BookingAddressReviewPane | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Booking/WhenWhere/AddressPicker/BookingSavedAddressChooser.swift | BookingSavedAddressChooserView, SavedAddressListPane, ChooserAddressRow | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Booking/WhenWhere/WhenWhereStep.swift | WhenWhereStep, SelectAddressRow, DayChipView, ExpressWaiverNote, TimeSlotRow, SectionLabel | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Disputes/CreateDisputeView.swift | CreateDisputeView | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Disputes/DisputeDetailContent.swift | DisputeThread, DisputeHeaderCard, DisputeMessageBubble, EvidenceRow, ReplyInputBar, DisputeDetailErrorView, FullscreenSingleImage | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Disputes/DisputeDetailView.swift | DisputeDetailView | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Disputes/DisputesListView.swift | DisputesListView, DisputesListContent, DisputeRowCard, DisputeStatusPill, DisputesLoadingView, DisputesErrorView, DisputesEmptyView | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Home/HomeSecondarySections.swift | RecentBookingsSection, RecentBookingRow, MilestoneProgressCard, HomeSkeleton | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Home/HomeSectionViews.swift | HomeSectionTitle, TrustStrip, OrderAgainCard, RecurringSchedulesSection, RecurringScheduleRow, PopularPackagesSection, PopularPackageCard | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Home/HomeTab.swift | HomeTab, AddressTopBar, MarketChip | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Home/NotificationsInboxSheet.swift | NotificationsInboxSheet, NotificationsInboxList, NotificationFeedRowCard, NotificationsInboxErrorView, NotificationsInboxEmptyView | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Home/UpsellCarousel.swift | UpsellCarousel, UpsellSlideCard | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Membership/MembershipManagementCard.swift | MembershipManagementCard, InactiveCard, ActiveCard, PerkPill | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Membership/MembershipSuccessScreen.swift | MembershipSuccessScreen, PerkRow | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Membership/SubscribePlusScreen.swift | SubscribePlusScreen, HeroTopRow, HeroBlock, PlanSwitcher, SocialProofTile, PerksSection, PerkTile, StickyCtaBar | Repaired: offer content stays inside the safe viewport |
| CleansiaCustomer/Sources/Features/Orders/CancellationFeeCard.swift | CancellationFeeCard, ExpressWaiverWarning, FeeCardRow | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Orders/CancelOrderSheet.swift | CancelOrderSheet, ReasonChips, FlexibleReasonGrid, NotesField | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Orders/OrderComponents.swift | OrderCardSurface, OrderSectionHeaderRow, OrderInfoRow, OrderStatusPill | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Orders/OrderDetailContent.swift | OrderDetailContent, OrderDetailCompactHeader, CustomerOrderTrackerHero | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Orders/OrderDetailDetailsCards.swift | CleaningDetailsCard, ExtrasFlow, OrderServicesCard, OrderPackagesCard, OrderInstructionsCard, AssignedCleanersCard, CleanerRow, TimeChip, FlexibleChips | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Orders/OrderDetailHeroAndAddress.swift | OrderAddressCard | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Orders/OrderDetailMap.swift | OrderDetailMapBackdrop | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Orders/OrderDetailPhotos.swift | OrderPhotosSection, PhotoCountPill, PhotoThumb | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Orders/OrderDetailSummary.swift | OrderPriceBreakdownCard, OrderHeroFactsStrip, OrderDiscountChip | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Orders/OrderDetailTimelineAndReview.swift | OrderTimelineCard, TimelineRow, OrderReviewCard, StarsRow, OrderReceiptCard | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Orders/OrderDetailView.swift | OrderDetailView, ConfirmRecurringFooter, OrderDetailActionsFooter, OrderDetailErrorView | Safe content; shared ornament clipped as described above; UIKit receipt sheet |
| CleansiaCustomer/Sources/Features/Orders/OrdersTab.swift | OrdersTab, OrdersListContent, FilterChipsRow, OrderFilterChip, OrderListCard, OrdersLoadingView, OrdersErrorView, OrdersEmptyView | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Orders/OrderStatusHero.swift | OrderStatusHero | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Orders/Photos/FullscreenPager.swift | FullscreenPager | Image/background full bleed; separate safe close button |
| CleansiaCustomer/Sources/Features/Orders/Photos/OrderPhotosScreen.swift | OrderPhotosScreen | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Orders/PreferredOfferCard.swift | PreferredOfferCard | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Orders/SubmitReviewSheet.swift | SubmitReviewSheet, StarPicker | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Profile/CustomerDevicesView.swift | CustomerDevicesView, DevicesContent, DeviceCard, CurrentDeviceChip, DevicesErrorState | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Profile/DeleteAccountView.swift | DeleteAccountView | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Profile/EditProfileView.swift | EditProfileView, AvatarField, BookingHintBanner | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Profile/HelpSupportView.swift | HelpSupportView, FaqRow | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Profile/NotificationsView.swift | NotificationsView, NotificationToggleRow | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Profile/PreferencePickerViews.swift | LanguagePickerView, MarketPickerView, AppearancePickerView, PreferencePickerList, PreferenceRow | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Profile/ProfileOnboardingView.swift | ProfileOnboardingView, ProfileOnboardingContent, ProfileOnboardingPreviewHost | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Profile/ProfileTab.swift | ProfileTab, ProfileRow, ProfileHeader, ProfileStatsCard, HeroGradient, TierBadge, EditProfileChip, DeleteAccountRow | Repaired: profile content stays inside the safe viewport |
| CleansiaCustomer/Sources/Features/Profile/RecurringEntryRow.swift | RecurringEntryRow | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Profile/SecurityView.swift | SecurityView | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Recurring/CreateRecurringScreen.swift | CreateRecurringScreen, SectionLabel, AppliesNotice, FrequencySection, DayOfWeekSection, DayChip, TimeSection, AddressSection, AddAddressRow, PropertySizeSection, ServicesSection, PaymentSection, StartsSection, SelectableRow | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Recurring/RecurringBookingsScreen.swift | RecurringBookingsScreen, TemplateList, LapsedPlusNotice, PlusGate, RecurringEmptyState, TemplateCard, CardAction | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Rewards/RewardsActivityScreen.swift | RewardsActivityScreen, RewardsActivityList | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Rewards/RewardsCards.swift | TierLadderCard, TierLadderRow, TierStatusBadge, InviteFriendsCard, ActivityPreviewCard | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Rewards/RewardsComponents.swift | RewardsActivityRow, RewardsStateMessage | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Rewards/RewardsTab.swift | RewardsTab, RewardsContentView, TierHeroCard, ProgressCard, CurrentPerksCard, RewardsCard | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Shell/CustomerBottomBar.swift | BookFab | Inherits safe host; no direct top-content escape |
| CleansiaCustomer/Sources/Features/Shell/CustomerShellView.swift | CustomerShellView | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Shell/PlaceholderTabView.swift | PlaceholderTabView | Background/media/UIKit-only escape; controls inherit safe host |
| CleansiaCustomer/Sources/Features/Splash/SplashGateView.swift | SplashGateView | Inherits safe host; no direct top-content escape |
