# Profile Completion — required-field collection during onboarding

**Status:** Ready for execution (after product decisions flagged below)
**Related:** Triggered by mobile booking submit rejecting users with empty phone; the quick fix in BookingViewModel deep-links them to Edit Profile after failure. This spec prevents them from getting there in the first place.

## Why this exists

Backend `CreateOrder.Command` validator requires `CustomerName`, `CustomerEmail`, `CustomerPhone` as non-empty strings. Backend `User` entity allows all three to be blank on registration — but then any booking attempt 400s.

Today on mobile:
- Sign-up collects `firstName`, `lastName`, `email`, `password`. No phone.
- Edit Profile screen has a phone input.
- Users can skip it forever and never know until they try to book.

On web the order wizard has a contact step with inline name/email/phone fields that pre-fill from the user's profile — users can edit inline at booking time. Mobile has no such step; it reads directly from `currentUser`.

**This spec solves it properly**, two parts:

1. **Make phone required at registration** (single-step sign-up becomes two-step on mobile).
2. **Post-sign-in profile-completeness check** — if a pre-existing user lacks phone, prompt them to add it before they can do anything that requires it.

## Product decisions needed before executing

### Decision 1: what's the minimum required profile shape?

Candidates for "required before the user can book":
- **A.** First name, last name, email, phone — matches backend `CreateOrder` validation. No more, no less.
- **B.** A + preferred language (we already have a language picker, but it's optional).
- **C.** A + at least one saved address (because bookings need one).

**Recommendation: A.** Keeps onboarding short. Saved-address prompt is context-specific (booking flow asks for an address inline if none saved) — handling it in onboarding feels intrusive.

### Decision 2: when does the prompt fire for existing users?

- **A.** On every app launch if profile incomplete — annoying.
- **B.** When the user tries an action that requires it — lazy. (This is the current quick-fix behavior: booking submit fails → deep-link to Edit Profile.)
- **C.** Once, after sign-in, with a full-screen "Complete your profile" step before MainShell. Skippable with "Remind me later" that just closes it; required-path if they attempt a gated action.

**Recommendation: C with "remind me later" escape** — nudge hard but don't hard-block. Hard-blocking for phone is too aggressive for users who just want to browse services or check pricing without committing.

### Decision 3: ~~guest-checkout overlap~~ — resolved 2026-04-24

Mobile is sign-in-required by product policy. Guest checkout is not applicable. This spec is the full answer for mobile: every user is signed-in, so profile completeness is the thing that matters.

---

## Tasks (pending product sign-off on decisions 1-2)

### TASK-PC1: Add phone field to mobile registration

```yaml
task: Extend sign-up form to collect phone before creating account
id: TASK-PC1
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  SignUpScreen today collects firstName, lastName, email, password.
  Add a phone field. Validate format client-side (same regex as
  web wizard: /^[+]?[\d\s()-]{6,20}$/). Pass phoneNumber through
  AuthRepository.register(). Backend register endpoint already
  accepts phoneNumber on UserRegister command.

files_to_modify:
  - src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/auth/SignUpScreen.kt
  - src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/auth/AuthViewModel.kt (extend register signature)
  - src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/auth/AuthApi.kt + AuthRepository.kt
  - strings.xml (5 locales) — phone label + validation error

dependencies: []
verification:
  - Register with empty phone → inline validation error
  - Register with valid phone → backend creates user with phone set
  - Log in to that user → Profile tab shows phone, booking submit doesn't trip the phone-missing check
```

### TASK-PC2: `isProfileComplete` signal on UserRepository

```yaml
task: Expose computed profile-completeness on UserRepository
id: TASK-PC2
type: feature
priority: medium
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  Add a computed StateFlow<Boolean> that's true iff currentUser has
  non-blank firstName + lastName + email + phoneNumber. Multiple
  consumers (booking submit, optional onboarding prompt, maybe a
  Profile tab badge) can observe this without duplicating the check.

  Implementation: combine() over currentUser; derive the boolean.

files_to_modify:
  - core/user/UserRepository.kt — add `val isProfileComplete: StateFlow<Boolean>`
  - BookingViewModel.submit() — replace the ad-hoc `phoneNumber.isNullOrBlank()` check with `!userRepository.isProfileComplete.value`, widen the snackbar message to cover any missing field.

dependencies: []
verification:
  - Sign in as a user with no phone → isProfileComplete = false
  - Fill phone via Edit Profile → flips to true
  - Booking submit respects the same gate
```

### TASK-PC3: Post-sign-in "Complete your profile" screen (optional per decision 2)

```yaml
task: Full-screen profile-completion nudge between sign-in and MainShell
id: TASK-PC3
type: feature
priority: low
specialist: mobile
app: customer-android
estimated_complexity: medium
recommended_model: sonnet

context: |
  If decision 2 lands on Option C: after successful sign-in, if
  isProfileComplete == false, navigate to a new CompleteProfileScreen
  instead of MainShell. Screen collects only missing fields. "Remind
  me later" closes it and lets user into the app; we track
  "completion_nudge_shown_at" in DataStore to show again ~24h later
  or on next gated action.

  Reuses EditProfileScreen's field components and save flow; just
  presents them in a sign-in-continuation context with different copy.

files_to_create:
  - features/profile/CompleteProfileScreen.kt
  - features/profile/CompleteProfileViewModel.kt

files_to_modify:
  - CleansiaNavHost.kt — add Routes.CompleteProfile, redirect post-signin
  - Routes.kt — CompleteProfile route constant
  - AppSettingsRepository.kt — `lastCompletionNudgeShownAt` preference
  - strings.xml (5 locales)

dependencies:
  - TASK-PC2 (needs isProfileComplete signal)
verification:
  - Sign in as incomplete user → lands on CompleteProfileScreen
  - Fill fields and save → lands on MainShell
  - "Remind me later" → lands on MainShell
  - 24h later or on booking submit → prompt again (or gate-fail helpfully — depends on decision 2)
```

### TASK-PC4: Relax quick-fix to use isProfileComplete (cleanup)

```yaml
task: Replace the phone-specific check in BookingViewModel with isProfileComplete
id: TASK-PC4
type: refactor
priority: low
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  The quick-fix added a `user.phoneNumber.isNullOrBlank()` check
  specifically for phone. Once PC2 ships, replace with the general
  isProfileComplete check. The snackbar message becomes
  "Please complete your profile before booking" instead of the
  phone-specific one. The deep-link to Edit Profile still works.

files_to_modify:
  - features/booking/BookingViewModel.kt — swap the check
  - strings.xml (5 locales) — `error_booking_profile_incomplete` generic string; deprecate phone-specific one (can stay as dead weight; no caller).

dependencies:
  - TASK-PC2
```

---

## Execution order

1. **Decisions 1-3 with product** — don't start without sign-off.
2. ~~If guest-checkout lands first~~ — moot; guest-checkout scrapped for mobile.
3. **Otherwise**: PC1 → PC2 → PC3 → PC4.

## Out of scope

- Address completeness enforcement — bookings that need an address can prompt inline (already do).
- Payment-method setup ahead of time — Stripe flow is on-demand.
- Avatar / profile photo — not required for bookings.
- Phone verification via SMS — separate spec if we ever want that.
