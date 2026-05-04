# Address Domain Unification — Phase C (Customer Web)

**Status:** Ready for execution
**Depends on:** Phase A (complete) + NSwag regen (complete per owner)

## What's wrong today

- **Profile page** (`libs/cleansia-customer-features/profile/src/lib/profile/profile.component.ts`) persists addresses in `localStorage` under key `cleansia_saved_addresses`. The NSwag client has all the endpoints; they're just not called.
- **Order wizard facade** (`libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.facade.ts`) reads the SAME localStorage blob into a `savedAddresses` signal. Selecting one copies the fields into the inline form — it never sends `savedAddressId`.
- **`Label` field missing** from the web — backend requires it, web interface has `{id, street, city, zip, country, isDefault}` with no `label`.
- **Lat/lng on web** — we won't add Mapbox to the web form in Phase C. Backend geocodes when no hints are provided; web leaves them null.
- **No service wrapper** — profile + order-wizard both call `customerClient.savedAddress.*` directly. Phase C introduces a tiny `SavedAddressStore` (signals-based, not NgRx — matches the facade pattern used elsewhere) so both surfaces share cached state.

---

## Task Specs

### TASK-C1: Add `SavedAddressStore` — shared signal-based cache

```yaml
task: Shared saved-address state with API-backed signal store
id: TASK-C1
type: feature
priority: high
specialist: frontend
app: customer
estimated_complexity: medium
recommended_model: sonnet

context: |
  Profile and order-wizard both need the same list. Don't put it in
  NgRx (overkill for 5 addresses) and don't duplicate it in each
  facade. A simple service with a writable signal works — Angular's
  DI gives us a singleton.

  Pattern: service owns `addresses = signal<SavedAddressDto[]>([])`
  + `loaded = signal(false)` + `loading = signal(false)`. All
  mutations go through the service so both surfaces stay in sync.

files_to_create:
  - path: libs/data-access/customer/src/lib/saved-addresses/saved-address.store.ts
    change: |
      @Injectable({ providedIn: 'root' })
      export class SavedAddressStore {
        private readonly client = inject(CustomerClient); // whatever the generated client is called
        readonly addresses = signal<SavedAddressDto[]>([]);
        readonly loading = signal(false);
        readonly loaded = signal(false);

        async refresh(): Promise<void> {
          this.loading.set(true);
          try {
            const list = await firstValueFrom(this.client.savedAddress.getMine());
            this.addresses.set(list ?? []);
            this.loaded.set(true);
          } catch (err) {
            // Snackbar via existing service (match what admin/customer error interceptor uses)
            this.snackbar.showError('profile.addresses.load_failed');
          } finally {
            this.loading.set(false);
          }
        }

        async add(command: AddSavedAddressCommand): Promise<SavedAddressDto | null> {
          // POST /Add, on success push into list, set-default-demotion done server-side
          // so we refresh() to get the demoted peers.
        }

        async update(command: UpdateSavedAddressCommand): Promise<SavedAddressDto | null>
        async setDefault(savedAddressId: string): Promise<boolean>
        async delete(id: string): Promise<boolean>

        get defaultAddress(): SavedAddressDto | undefined {
          return this.addresses().find(a => a.isDefault);
        }
      }

      Use inject() syntax. Use firstValueFrom for the Promise-based
      API to keep components simple. All mutation methods return a
      nullable/boolean to signal success without leaking the full
      HttpErrorResponse; errors go straight to the snackbar.

  - path: libs/data-access/customer/src/lib/saved-addresses/index.ts
    change: |
      export * from './saved-address.store';

  - path: libs/data-access/customer/src/index.ts
    change: |
      Add: export * from './lib/saved-addresses';
      (or whatever the existing barrel convention is — read the file first)

files_to_modify:
  - path: apps/cleansia.app/src/assets/i18n/en.json
    change: |
      Under the pages.profile.addresses section (around line 570), add:
        "load_failed": "Couldn't load your saved addresses. Please try again."
        "save_failed": "Couldn't save this address. Please try again."
        "delete_failed": "Couldn't remove this address. Please try again."
        "default_failed": "Couldn't set this as your default. Please try again."

  - path: apps/cleansia.app/src/assets/i18n/cs.json
    change: Same 4 keys, Czech translations.

  - path: apps/cleansia.app/src/assets/i18n/sk.json
    change: Same 4 keys, Slovak translations.

  - path: apps/cleansia.app/src/assets/i18n/uk.json
    change: Same 4 keys, Ukrainian translations.

  - path: apps/cleansia.app/src/assets/i18n/ru.json
    change: Same 4 keys, Russian translations.

dependencies: []
verification:
  - `npx nx build cleansia.app` passes
  - Unit-test the store if there's an existing pattern; otherwise
    skip (the consumer components are the real test).
```

### TASK-C2: Migrate profile page from localStorage to `SavedAddressStore`

```yaml
task: Rip out localStorage address code in profile.component.ts, use store
id: TASK-C2
type: refactor
priority: high
specialist: frontend
app: customer
estimated_complexity: medium
recommended_model: sonnet

context: |
  profile.component.ts has ~110 lines of localStorage handling
  (lines 310-424 per the audit). Delete all of it. Inject
  SavedAddressStore. Replace local `addresses` signal with
  `store.addresses`. Replace save/edit/delete/setDefault to call the
  store. Form gains a `label` input (required, max 50). Empty state
  + dialog scaffolding stay as-is.

files_to_modify:
  - path: libs/cleansia-customer-features/profile/src/lib/profile/profile.component.ts
    change: |
      1. Remove the local `SavedAddress` interface (lines 35-42).
         Import SavedAddressDto from the store barrel instead.
      2. Remove the localStorage key constant (line 311) and the
         loadAddresses() / saveAddresses() methods (lines 313-328).
      3. Delete every call to those methods.
      4. Replace the component's local `addresses` signal with:
           store = inject(SavedAddressStore);
           addresses = this.store.addresses;
      5. In `ngOnInit` (or whatever init hook exists), call
         `this.store.refresh()` if `!this.store.loaded()`.
      6. Add `label` to the reactive form (FormBuilder):
           label: ['', [Validators.required, Validators.maxLength(50)]]
         Place the label input at the top of the dialog form
         (matches mobile UX — label before street).
      7. Rewrite saveAddress() (lines 355-385):
           - If editing: store.update({ savedAddressId: editing.id, ... })
           - If adding: store.add({ ... setAsDefault: form.isDefault })
           - On failure: store already showed snackbar; just keep dialog open
           - On success: close dialog
      8. Rewrite deleteAddress() — call store.delete(id).
      9. Rewrite setDefaultAddress() — call store.setDefault(id).
      10. Map SavedAddressDto → view model:
          - display 'label' + 'street, zipCode city' + country.
          - 'isDefault' stays the same.

  - path: libs/cleansia-customer-features/profile/src/lib/profile/profile.component.html
    change: |
      Template updates:
        - Add a {{ address.label }} line above the street line
          (larger font, bold — matches mobile's "Home" / "Work" style).
        - Add a label input in the dialog form. Use translation key
          pages.profile.address_label (add to i18n).
        - If the component uses a dedicated edit dialog template,
          update it there.

  - path: apps/cleansia.app/src/assets/i18n/en.json
    change: |
      Add under pages.profile section:
        "address_label": "Label (e.g. Home, Work)"
        "address_label_required": "Please name this address"

  - path: apps/cleansia.app/src/assets/i18n/cs.json
    change: Same 2 keys, Czech.
  - path: apps/cleansia.app/src/assets/i18n/sk.json
    change: Same 2 keys, Slovak.
  - path: apps/cleansia.app/src/assets/i18n/uk.json
    change: Same 2 keys, Ukrainian.
  - path: apps/cleansia.app/src/assets/i18n/ru.json
    change: Same 2 keys, Russian.

  - path: libs/cleansia-customer-features/profile/src/lib/profile/profile.component.scss
    change: |
      If the current template doesn't show a label line, add the
      typography for .saved-address-label (Poppins, bold, slightly
      larger than street). Otherwise no change.

dependencies:
  - TASK-C1
verification:
  - `npx nx build cleansia.app`
  - Manual: sign in, go to profile → Addresses tab. If you had
    localStorage addresses from before, they'll vanish — that's
    expected. Add one via the new form with label field. Edit it.
    Delete it. Set default. Refresh the page — addresses persist
    (via backend, not localStorage). Confirm DevTools Network tab
    shows /api/SavedAddress/* calls.
```

### TASK-C3: Migrate order-wizard to `SavedAddressStore` + send `savedAddressId`

```yaml
task: Order wizard reads saved addresses from store + POSTs savedAddressId
id: TASK-C3
type: refactor
priority: high
specialist: frontend
app: customer
estimated_complexity: medium
recommended_model: sonnet

context: |
  The wizard already has a "pick saved address" section in the
  template (component.ts lines 273-308). Two gaps:
    1. The savedAddresses signal is populated from localStorage.
       Replace with store.addresses().
    2. When submitting the order, if a saved address is picked,
       send savedAddressId and OMIT customerAddress. For one-off
       address entries, send inline customerAddress and OMIT
       savedAddressId. Backend enforces XOR.

files_to_modify:
  - path: libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.facade.ts
    change: |
      1. Remove the localStorage read block (line 119) and the local
         savedAddresses signal declaration (line 52).
      2. Inject SavedAddressStore. Add:
           private store = inject(SavedAddressStore);
           savedAddresses = this.store.addresses;
      3. On facade init (wherever the constructor/ngOnInit equivalent
         is), call `this.store.refresh()` if not loaded AND the user
         is authenticated. For guest users, savedAddresses stays
         empty — wizard falls back to inline form only.
      4. Add a `selectedSavedAddressId = signal<string | null>(null)`
         signal. `selectSavedAddress(id)` (line 151) sets it AND
         pre-fills the form (as today, for display). Selecting
         "new address" or editing the form resets it to null.
      5. Modify the order-submission method (wherever it builds
         CreateOrderCommand):
           - If selectedSavedAddressId() != null:
               customerAddress: undefined,
               savedAddressId: selectedSavedAddressId(),
           - Else (inline):
               customerAddress: { street, city, zipCode, state: null, countryId },
               savedAddressId: undefined,

  - path: libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.component.ts
    change: |
      1. isSavedAddressSelected() / isAddressSelected(addr)
         (lines 288-301) — update to compare `selectedSavedAddressId`
         against addr.id instead of comparing field values.
         Simpler, more reliable.
      2. saveCurrentAddressToList() (line 316) — instead of pushing
         into localStorage, call facade.store.add({...}) with a label
         prompt. UX decision: before saving, open an inline input /
         dialog asking for the label. Keep it minimal — a small
         inline input that appears below "Save this address" checkbox
         when checked, with placeholder "Home, Office, Parents..."
      3. clearAddress() (line 303) — also set
         facade.selectedSavedAddressId.set(null).

  - path: libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.component.html
    change: |
      1. If a "saved address" section exists (around lines 273-308 per
         audit), update the iteration to use facade.savedAddresses()
         from the store.
      2. Add an inline label input for "save this address for next time"
         checkbox. Conditional: only visible when checkbox is checked.
         Required if checkbox is checked.

  - path: libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.models.ts
    change: |
      If there's a local Address interface here shadowing the DTO,
      remove it in favor of SavedAddressDto from the store barrel.

  - path: libs/shared/assets/src/styles/pages/cleansia-customer/order-wizard.component.scss
    change: |
      If a .saved-address-pick class exists and renders the address
      list, add a .saved-address-pick__label style (consistent with
      profile page).

dependencies:
  - TASK-C1
  - TASK-C2  # so both surfaces land together
verification:
  - `npx nx build cleansia.app`
  - Manual E2E:
     1. Sign in. Add an address in profile. Open order wizard.
     2. Address step shows your saved addresses. Select one.
        Proceed. Submit. Network tab: CreateOrderCommand has
        savedAddressId set, customerAddress is omitted (or null).
     3. Back out, pick "new address" instead. Fill inline form.
        Check "save this address" — label input appears. Submit.
        Network: customerAddress has inline values,
        savedAddressId is null. Profile now shows the new address.
     4. Back out, don't save. Submit a one-off. Network: inline
        customerAddress. Profile list unchanged.
```

### TASK-C4: Delete dead localStorage helpers + any stray `cleansia_saved_addresses` references

```yaml
task: Cleanup dead localStorage code and references
id: TASK-C4
type: refactor
priority: low
specialist: frontend
app: customer
estimated_complexity: small
recommended_model: haiku

context: |
  After TASK-C2 + TASK-C3, grep for 'cleansia_saved_addresses' and
  'localStorage.getItem' in libs/cleansia-customer-features/ —
  delete any leftover helpers or tests. Also check shared/utils for
  dead migration code.

files_to_modify:
  - path: any file containing 'cleansia_saved_addresses' string literal
    change: delete the reference (it's dead code post-migration)

dependencies:
  - TASK-C2
  - TASK-C3
verification:
  - grep result: zero matches for 'cleansia_saved_addresses'
  - `npx nx build cleansia.app` passes
```

### TASK-C5: Trigger `store.refresh()` on sign-in

```yaml
task: Load addresses when user authenticates
id: TASK-C5
type: feature
priority: medium
specialist: frontend
app: customer
estimated_complexity: small
recommended_model: sonnet

context: |
  Profile page calls refresh() on its own ngOnInit, but if a user
  lands on /order-wizard first without visiting profile, the store
  is empty. Hook refresh() into the auth-state change so it
  preloads after sign-in.

files_to_modify:
  - path: libs/core/customer-services/src/lib/services/customer-auth.service.ts
    change: |
      After successful sign-in (in the method that handles the
      login response), dispatch store.refresh(). Either inject
      SavedAddressStore here (simpler) or fire an event that
      SavedAddressStore listens for.

      Simplest: inject the store and call refresh() after
      token storage. Guard with try/catch — failing refresh
      should not block sign-in.

dependencies:
  - TASK-C1
verification:
  - Sign out, sign back in, navigate straight to /order-wizard.
    Network: /api/SavedAddress/GetMine called automatically.
    Saved addresses render in wizard without visiting profile.
```

---

## Execution order

1. **TASK-C1** (store + i18n error keys) — foundation, everything depends on it.
2. **TASK-C2** (profile migration) + **TASK-C3** (wizard migration) — parallel, both depend only on C1.
3. **TASK-C4** (cleanup) — after C2+C3.
4. **TASK-C5** (auth hook) — can run parallel to C4.

Estimated total token usage: ~50k.

---

## Backward compatibility

**Users with existing `cleansia_saved_addresses` localStorage entries will lose them** — those addresses live ONLY on the client, never hit the backend. Pragma for pre-launch: acceptable. Post-launch: we'd write a one-time migration (read localStorage, POST each to /Add, clear storage). Skip for now — the audit shows this app is pre-production and users aren't in the double-digits yet.

If you want the migration for safety, add a TASK-C6 that runs once on first mount: reads the old key, POSTs each entry with label defaulted to "Saved N", clears the key. Low priority.

## Out of scope

- **Admin web** — admin doesn't manage customer addresses.
- **Partner web** — partner sees order addresses read-only.
- **Map picker on web** — not adding Mapbox to the web form in Phase C; backend geocodes. If you want parity with mobile later, that's a separate Phase D.

---

## Follow-up TODOs (from TASK-C4)

- ~~**NSwag codegen bug on `CreateOrderCommand.customerAddress`**~~ — **RESOLVED** by [nswag-customer-address-nullability.md](./nswag-customer-address-nullability.md). A targeted `ISchemaFilter` on both the Customer and Mobile APIs wraps the `$ref` in `allOf` + `nullable: true`. Post-regen the property types as `customerAddress!: CustomerAddress | undefined` and the facade's `undefined as unknown as AddressDto` cast has been dropped. Side effect: `CustomerAddress` is now a synthetic subclass of `AddressDto` — construct via `new CustomerAddress(...)` at the submit site.
